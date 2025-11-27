#pragma warning disable CA1416

namespace My.ClasStars.Components
{
    public partial class ImageEditorModal
    {
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected Microsoft.AspNetCore.Hosting.IWebHostEnvironment WebHostEnvironment { get; set; }
        [Inject] protected IClasStarsServices ClasStarsServices { get; set; }

        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
        [Parameter] public List<ImageDetail> ImageURI { get; set; } = new();
        [Parameter] public EventCallback<List<ImageDetail>> ImageURIChanged { get; set; }
        [Parameter] public EventCallback<ImageDetail> ImageEdited { get; set; }
        [Parameter] public EventCallback<(int ContactId, byte[] Picture,bool IsDeleted)> ContactPictureUpdated { get; set; }

        public SfImageEditor ImageEditorRef;
        public List<ImageEditorToolbarItemModel> customToolbarItem = new();

        private bool isProcessing = false;
        private bool _actionInProgress = false;   
        private TaskCompletionSource<bool> _editorReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        string camera = "enablerImg";
        string cameraVisiblility = "cameraSzeHidden";
        bool isLoadImageButtonClicked = false;
        bool isDeleteClicked = false;
        string cropClass = "enablerImg";
        string acceptClass = "disableImg";
        string rotateClass = "disableImg";
        string saveClass = "disableImg";
        string undoClass = "disableImg";
        string refreshClass = "disableImg";

        InputFile fileInputSingle;
        string wwwRootPath = "";
        int selectedContactId = 0;
        string StatusMessage = "";
        Guid selectedGuid;
        public string FName = "";
        public bool IsContact = false;
        public static string isSquareError = "";

        private DotNetObjectReference<ImageEditorModal> _dotNetRef;


        private readonly object _saveLock = new();

        protected override Task OnInitializedAsync()
        {
            wwwRootPath = WebHostEnvironment.WebRootPath;
            return base.OnInitializedAsync();
        }


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ImageEditorRef != null && !_editorReadyTcs.Task.IsCompleted)
        {
            _editorReadyTcs.SetResult(true);
        }
        await base.OnAfterRenderAsync(firstRender);
    }


        [JSInvokable]
        public async Task OnSfEditorFileSelected(string fileName, bool isSquare)
         {
            try
            {
                if (!string.IsNullOrEmpty(fileName))
                {
                    FName = Path.GetFileNameWithoutExtension(fileName);
                }

                SetToolbarForSquare(isSquare);

                if (isSquare)
                    StatusMessage = StringsResource.ImageEditor_SelectedImageSquare;
                else
                    StatusMessage = StringsResource.ImageEditor_SelectedImageNotSquare;

                await InvokeAsync(StateHasChanged);
            }
            catch (Exception ex)
            {
                Console.WriteLine("OnSfEditorFileSelected Exception: " + ex.Message);
            }
        }
        private async Task EnsureEditorReadyAsync(int timeoutMs = 5000)
        {
            if (ImageEditorRef != null) return;
            var task = _editorReadyTcs.Task;
            if (await Task.WhenAny(task, Task.Delay(timeoutMs)) != task)
            {
                throw new InvalidOperationException("SfImageEditor reference not available after render. Check modal markup and Syncfusion scripts.");
            }
        }

        public enum EditorImageFormat { Unknown, Jpeg, Png }

        public EditorImageFormat GetImageFormatFromStream(MemoryStream ms)
        {
            try
            {
                byte[] header = new byte[8];
                ms.Position = 0;
                _ = ms.Read(header, 0, header.Length);
                ms.Position = 0;

                if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
                    return EditorImageFormat.Jpeg;

                if (header.Length >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                    && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
                    return EditorImageFormat.Png;

                return EditorImageFormat.Unknown;
            }
            catch
            {
                return EditorImageFormat.Unknown;
            }
        }

        public bool? IsSquareImage(MemoryStream stream, out string err)
        {
            err = "";
            try
            {
                using var image = System.Drawing.Image.FromStream(stream);
                return Math.Abs(image.Width - image.Height) <= 2;
            }
            catch (Exception e)
            {
                err = e.Message;
                return null;
            }
        }

        public void DragStart(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ImageDetail iDetail)
        {
            try
            {
            }
            catch (Exception er)
            {
                StatusMessage = string.Format(StringsResource.Common_ExceptionWithDetail, er.Message);
            }
        }

        public void DragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e) {     }

        public async Task DragDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ContactInfoModel contact)
        {
            await JSRuntime.InvokeVoidAsync("alert", StringsResource.ImageEditor_DragDropNotSupported);
        }

        public async Task CloseModelPopup()
        {
            if (_actionInProgress) return;
            _actionInProgress = true;
            try
            {
                isLoadImageButtonClicked = false;
                isDeleteClicked = false;
                cameraVisiblility = "cameraSzeHidden";
                Visible = false;
                await VisibleChanged.InvokeAsync(Visible);
                try { await JSRuntime.InvokeVoidAsync("closeModal"); } catch { }
                try { await JSRuntime.InvokeVoidAsync("imageEditorModal_detachGlobalInputChangeListener"); } catch { }
                _dotNetRef?.Dispose();
                _dotNetRef = null;


            }
            finally
            {
                _actionInProgress = false;
                StateHasChanged();
            }
        }

        private static string PathCombine(string? root, params string[] parts)
        {
            if (string.IsNullOrEmpty(root)) return string.Join("/", parts);
            return System.IO.Path.Combine(root.TrimEnd('\\', '/'), System.IO.Path.Combine(parts)).Replace("\\", "/");
        }

        public async Task PerformCamera()
        {
            camera = "disableImg";
            try
            {
                await JSRuntime.InvokeVoidAsync("ShowCamera", 1);
            }
            catch
            {
            }

            await Task.Delay(2000);
            cameraVisiblility = cameraVisiblility == "cameraSzeHidden" ? "cameraSzeVisible" : "cameraSzeHidden";
            camera = "enablerImg";

            cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "disableImg";

            isLoadImageButtonClicked = false;
            isDeleteClicked = false;
            StateHasChanged();
        }

        public async Task PerformLoad()
        {
            cameraVisiblility = "cameraSzeHidden";
            try { await JSRuntime.InvokeVoidAsync("ShowCamera", 0); } catch { }

            try
            {
                await JSRuntime.InvokeVoidAsync("imageEditorModal_triggerFile", "custom-modal");
            }
            catch {    }

            isDeleteClicked = false;
            isLoadImageButtonClicked = true;

            cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "disableImg";
            StateHasChanged();
        }

        public async Task PerformDelete()
        {
            cameraVisiblility = "cameraSzeHidden";
            try { await JSRuntime.InvokeVoidAsync("ShowCamera", 0); } catch { }
            string BlankImageFilePath = PathCombine(wwwRootPath, "Images", "EditorBlank1.png");

            byte[] imageBytes = Array.Empty<byte>();
            try
            {
                imageBytes = File.ReadAllBytes(BlankImageFilePath);
            }
            catch
            {
                imageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=");
            }

            string base64String = $"data:image/png;base64,{Convert.ToBase64String(imageBytes)}";

            isLoadImageButtonClicked = false;
            isDeleteClicked = true;

            try
            {
                await EnsureEditorReadyAsync();
                await ImageEditorRef.OpenAsync(base64String);
            }
            catch
            {
            }

            cropClass = "disableImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "enablerImg";
            StateHasChanged();
        }

        public async Task PerformCrop()
        {
            isDeleteClicked = false;

            try
            {
                await EnsureEditorReadyAsync();

                var rect = await JSRuntime.InvokeAsync<object>("imageEditorModal_getCanvasRect", "custom-modal");
                int canvasWidth = 0, canvasHeight = 0;

                if (rect != null)
                {
                    var jObj = Newtonsoft.Json.Linq.JObject.FromObject(rect);
                    canvasWidth = jObj.Value<int?>("width") ?? 0;
                    canvasHeight = jObj.Value<int?>("height") ?? 0;
                }

                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    if (ImageEditorRef != null)
                        await ImageEditorRef.SelectAsync("Square", -1, -1, -1, -1);
                }
                else
                {
                    int minSide = Math.Min(canvasWidth, canvasHeight);
                    int selSize = (int)Math.Round(minSide * 0.5);     
                    int selX = (canvasWidth - selSize) / 2;
                    int selY = (canvasHeight - selSize) / 2;

                    if (ImageEditorRef != null)
                        await ImageEditorRef.SelectAsync("Square", selX, selY, selSize, selSize);

                    await Task.Delay(30);
                    try { await JSRuntime.InvokeVoidAsync("imageEditorModal_dispatchResize"); } catch { }
                    await Task.Delay(30);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PerformCrop exception: " + ex.Message);
                try
                {
                    if (ImageEditorRef != null)
                        await ImageEditorRef.SelectAsync("Square", -1, -1, -1, -1);
                }
                catch { }
            }

            acceptClass = "enablerImg";
            rotateClass = "enablerImg";
            undoClass = "disableImg";
            saveClass = "disableImg";
            StateHasChanged();
        }


        public async Task PerformRotate()
        {
            isDeleteClicked = false;
            try
            {
                await EnsureEditorReadyAsync();
                if (ImageEditorRef != null)
                    await ImageEditorRef.RotateAsync(90);
            }
            catch { }

            cropClass = "enablerImg";
            acceptClass = "enablerImg";
            saveClass = "disableImg";
            undoClass = "disableImg";
            StateHasChanged();
        }

        public async Task PerformUndo()
        {
            isDeleteClicked = false;
            try
            {
                await EnsureEditorReadyAsync();
                if (ImageEditorRef != null)
                    await ImageEditorRef.UndoAsync();
            }
            catch { }

            cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "disableImg";
            StateHasChanged();
        }

        public async Task PerformAccept()
        {
            isDeleteClicked = false;
            try
            {
                await EnsureEditorReadyAsync();
                if (ImageEditorRef != null)
                    await ImageEditorRef.CropAsync();
            }
            catch { }

            cropClass = "enablerImg";
            acceptClass = "disableImg";
            rotateClass = "disableImg";
            saveClass = "enablerImg";
            undoClass = "enablerImg";
            StateHasChanged();
        }

        public async Task SaveEditedImage()
        {
            if (_actionInProgress) return;

            lock (_saveLock)
            {
                if (_actionInProgress) return;
                _actionInProgress = true;
            }

            isProcessing = true;
            StateHasChanged();

            try
            {
                await EnsureEditorReadyAsync();
                byte[] editedImageData = await ImageEditorRef.GetImageDataAsync();

                if (isDeleteClicked && selectedContactId > 0)
                {
                    await ClasStarsServices.RemoveContactPicture(selectedContactId);
                    isDeleteClicked = false;
                    StatusMessage = StringsResource.ImageEditor_PictureDeleted;
                    if (ContactPictureUpdated.HasDelegate)
                        await ContactPictureUpdated.InvokeAsync((selectedContactId, editedImageData, true));

                    await CloseModalAfterSave();
                    return;
                }

                if (selectedContactId > 0)
                {
                    using var mem = new MemoryStream(editedImageData);
                    bool? IsSquare = IsSquareImage(mem, out isSquareError);
                    if (IsSquare == true)
                    {
                        await ClasStarsServices.SaveContactPicture(selectedContactId, editedImageData);
                        StatusMessage = StringsResource.ImageEditor_PictureUpdated;
                        if (ContactPictureUpdated.HasDelegate)
                            await ContactPictureUpdated.InvokeAsync((selectedContactId, editedImageData, false));

                        await CloseModalAfterSave();
                    }
                    else
                    {
                        await JSRuntime.InvokeVoidAsync("alert", StringsResource.ImageEditor_PictureNotSquareAlert);
                    }
                    return;
                }

                if (selectedGuid != Guid.Empty && ImageURI != null)
                {
                    var item = ImageURI.FirstOrDefault(x => x.ImgId == selectedGuid);
                    if (item != null)
                    {
                        using var msCheck = new MemoryStream(editedImageData);
                        var fmt = GetImageFormatFromStream(msCheck);
                        var ext = fmt == EditorImageFormat.Png ? "png" : "jpeg";
                        var dataUri = $"data:image/{ext};base64,{Convert.ToBase64String(editedImageData)}";

                        using var msForSquare = new MemoryStream(editedImageData);
                        bool? isSquare = IsSquareImage(msForSquare, out isSquareError);

                        item.ImageUrl = dataUri;
                        item.IsSqu = isSquare;
                        item.IsVis = true;

                        if (ImageURIChanged.HasDelegate)
                        {
                            await ImageURIChanged.InvokeAsync(ImageURI);
                        }

                        if (ImageEdited.HasDelegate)
                        {
                            await ImageEdited.InvokeAsync(item);
                        }

                        StatusMessage = StringsResource.ImageEditor_ImageUpdated;
                        await CloseModalAfterSave();
                        return;
                    }
                    else
                    {
                        StatusMessage = StringsResource.ImageEditor_ImageItemMissing;
                        await CloseModalAfterSave();
                        return;
                    }
                }

                StatusMessage = StringsResource.ImageEditor_NothingToSave;
            }
            catch (Exception ex)
            {
                await JSRuntime.InvokeVoidAsync("alert", string.Format(StringsResource.ImageEditor_SaveError, ex.Message));
            }
            finally
            {
                isProcessing = false;
                _actionInProgress = false;

                cropClass = "enablerImg";
                rotateClass = "disableImg";
                acceptClass = "disableImg";
                saveClass = "disableImg";
                undoClass = "disableImg";
                isLoadImageButtonClicked = false;
                StateHasChanged();
            }
        }

        private async Task CloseModalAfterSave()
        {
            Visible = false;
            await VisibleChanged.InvokeAsync(Visible);
            try { await JSRuntime.InvokeVoidAsync("closeModal"); } catch { }
            StateHasChanged();
        }

        public async Task Capture()
        {
            try
            {
                string capturedImg = await JSRuntime.InvokeAsync<string>("take_snapshot");
                if (string.IsNullOrEmpty(capturedImg))
                {
                    cameraVisiblility = "cameraSzeHidden";
                }
                else
                {
                    cropClass = "enablerImg";
                    rotateClass = "disableImg";
                    acceptClass = "disableImg";
                    undoClass = "disableImg";
                    saveClass = "disableImg";
                    await EnsureEditorReadyAsync();
                    await ImageEditorRef.OpenAsync(capturedImg);
                }
            }
            catch
            {
            }
            StateHasChanged();
        }

        public async Task OpenForImage(ImageDetail imgg)
        {
            if (imgg == null) throw new ArgumentNullException(nameof(imgg));
            StatusMessage = string.Empty;
            IsContact = false;
            selectedContactId = 0;
            FName = Path.GetFileNameWithoutExtension(imgg.ImageName);

            Visible = true;
            await VisibleChanged.InvokeAsync(Visible);

            await InvokeAsync(StateHasChanged);
            await Task.Delay(50);        

            try { await JSRuntime.InvokeVoidAsync("openModal"); } catch { }

            await Task.Delay(50);
            await EnsureEditorReadyAsync(5000);

            if (_dotNetRef == null) _dotNetRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("imageEditorModal_attachGlobalInputChangeListener", "custom-modal", _dotNetRef);



            selectedGuid = imgg.ImgId;
            try
            {
                await Task.Delay(150);
                await ImageEditorRef.OpenAsync(imgg.ImageUrl);
                await Task.Delay(60);
                try { await JSRuntime.InvokeVoidAsync("imageEditorModal_dispatchResize"); } catch { }
                await Task.Delay(50);
            }
            catch
            {
            }

            cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "disableImg";
            StateHasChanged();
        }

        private void SetToolbarForSquare(bool isSquare)
        {
            if (isSquare)
            {
                saveClass = "enablerImg";
                cropClass = "enablerImg";
                acceptClass = "disableImg";
                rotateClass = "disableImg";
                undoClass = "disableImg";
            }
            else
            {
                saveClass = "disableImg";
                cropClass = "enablerImg";
                acceptClass = "disableImg";
                rotateClass = "disableImg";
                undoClass = "disableImg";
            }
        }


        public async Task OpenForContact(ContactInfoModel contact)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));
            StatusMessage = string.Empty;
            isLoadImageButtonClicked = false;
            IsContact = true;
            FName = contact.FirstName + " " + contact.LastName;

            Visible = true;
            await VisibleChanged.InvokeAsync(Visible);

            await InvokeAsync(StateHasChanged);
            await Task.Delay(50);        

            try { await JSRuntime.InvokeVoidAsync("openModal"); } catch { }

            await Task.Delay(50);
            await EnsureEditorReadyAsync(5000);

            if (_dotNetRef == null) _dotNetRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("imageEditorModal_attachGlobalInputChangeListener", "custom-modal", _dotNetRef);



            string blankPath = PathCombine(wwwRootPath, "Images", "EditorBlank1.png");
            byte[] imageBytes;
            try
            {
                imageBytes = (contact.ContactPicture != null && contact.ContactPicture.Length > 0) ? contact.ContactPicture : await File.ReadAllBytesAsync(blankPath);
            }
            catch
            {
                imageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=");
            }
            if (imageBytes != null)
            {
                using (var ms = new MemoryStream(imageBytes))
                {
                    var fmt = GetImageFormatFromStream(ms);
                    var ext = fmt == EditorImageFormat.Png ? "png" : (fmt == EditorImageFormat.Jpeg ? "jpeg" : "png");
                    var dataUri =  $"data:image/{ext};base64,{Convert.ToBase64String(imageBytes)}";
                    selectedContactId = contact.ContactID;
                    try
                    {
                        await Task.Delay(150);
                        await ImageEditorRef.OpenAsync(dataUri);

                        await Task.Delay(60);
                        try { await JSRuntime.InvokeVoidAsync("imageEditorModal_dispatchResize"); } catch { }
                        await Task.Delay(50);

                    }
                    catch
                    {
                    }
                }
            }
            else
            {
                selectedContactId = contact.ContactID;
                await Task.Delay(150);
                await ImageEditorRef.OpenAsync(null);
                await Task.Delay(60);
                try { await JSRuntime.InvokeVoidAsync("imageEditorModal_dispatchResize"); } catch { }
                await Task.Delay(50);
            }


                cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "disableImg";
            StateHasChanged();
        }



    }

}

public class ImageDetail
{
    public Guid ImgId { get; set; }
    public string ImageName { get; set; }
    public string ImageUrl { get; set; }
    public bool? IsSqu { get; set; }
    public bool IsVis { get; set; }
    public bool IsMatched { get; set; }
    public int? MatchedContactId { get; set; }
}

