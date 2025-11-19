using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using My.ClasStars.Helpers;
using My.ClasStars.Models;
using My.ClasStars.Resources;
using SharedTypes;
using Syncfusion.Blazor.Data;
using Syncfusion.Blazor.ImageEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

#pragma warning disable CA1416

namespace My.ClasStars.Components
{
    public partial class ImageEditorModal
    {
        // --- DI ---
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected Microsoft.AspNetCore.Hosting.IWebHostEnvironment WebHostEnvironment { get; set; }
        [Inject] protected IClasStarsServices ClasStarsServices { get; set; }

        // --- Parameters / callbacks ---
        [Parameter] public bool Visible { get; set; }
        [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
        [Parameter] public List<ImageDetail> ImageURI { get; set; } = new();
        [Parameter] public EventCallback<List<ImageDetail>> ImageURIChanged { get; set; }
        [Parameter] public EventCallback<(int ContactId, byte[] Picture,bool IsDeleted)> ContactPictureUpdated { get; set; }

        // --- Editor reference and toolbar config ---
        public SfImageEditor ImageEditorRef;
        public List<ImageEditorToolbarItemModel> customToolbarItem = new();

        // --- UI state ---
        private bool isProcessing = false;
        private bool _actionInProgress = false; // guard double-click
        private TaskCompletionSource<bool> _editorReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Camera / load / delete / toolbar classes
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

        // Local small locks
        private readonly object _saveLock = new();

        protected override Task OnInitializedAsync()
        {
            wwwRootPath = WebHostEnvironment.WebRootPath;
            return base.OnInitializedAsync();
        }

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            // Resolve the TCS when editor ref is set
            if (ImageEditorRef != null && !_editorReadyTcs.Task.IsCompleted)
            {
                _editorReadyTcs.SetResult(true);
            }
            return base.OnAfterRenderAsync(firstRender);
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

        // -----------------------
        // Image loading (component's hidden InputFile)
        // -----------------------
        private async Task LoadFiles(InputFileChangeEventArgs e)
        {
            StatusMessage = "";
            isSquareError = string.Empty;
            isProcessing = true;
            StateHasChanged();

            try
            {
                var maxFiles = isLoadImageButtonClicked ? 1 : 100;
                IReadOnlyList<IBrowserFile> files;
                if (maxFiles == 1 && e.FileCount > 1)
                {
                    await JSRuntime.InvokeVoidAsync("alert", "You can only select one image at a time.");
                    return;
                }
                else
                {
                    files = e.GetMultipleFiles(maxFiles);
                    // Note: clearing the shared ImageURI here keeps behavior consistent with parent bulk load.
                    ImageURI.Clear();
                }

                foreach (var file in files)
                {
                    using var stream = file.OpenReadStream(1024 * 1024 * 30);
                    using MemoryStream ms = new();
                    await stream.CopyToAsync(ms);

                    var format = GetImageFormatFromStream(ms);
                    var isSquare = IsSquareImage(ms, out isSquareError);

                    // If editor load flow (single file)
                    var imgUrl = $"data:image/{(format == EditorImageFormat.Png ? "png" : "jpeg")};base64,{Convert.ToBase64String(ms.ToArray())}";

                    if (isLoadImageButtonClicked)
                    {
                        // open directly in editor
                        await OpenForImage(new ImageDetail { ImgId = Guid.NewGuid(), ImageName = file.Name, ImageUrl = imgUrl });
                    }
                    else
                    {
                        var ImgDetail = new ImageDetail
                        {
                            ImgId = Guid.NewGuid(),
                            ImageName = file.Name
                        };

                        if (isSquare != null)
                        {
                            ImgDetail.ImageUrl = imgUrl;
                            ImgDetail.IsSqu = isSquare;
                            ImgDetail.IsVis = true;
                        }
                        else
                        {
                            ImgDetail.ImageUrl = "";
                            ImgDetail.IsSqu = null;
                            ImgDetail.IsVis = false;
                        }

                        ImageURI.Add(ImgDetail);
                    }
                }

                // notify parent of changes
                if (ImageURIChanged.HasDelegate)
                    await ImageURIChanged.InvokeAsync(ImageURI);
            }
            catch (Exception ex)
            {
                StatusMessage = "LoadFiles failed: " + ex.Message;
            }
            finally
            {
                isProcessing = false;
                isLoadImageButtonClicked = false;
                StateHasChanged();
            }
        }

        // -----------------------
        // Image helpers
        // -----------------------
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

        // -----------------------
        // Drag helpers (simplified)
        // -----------------------
        public void DragStart(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ImageDetail iDetail)
        {
            try
            {
                // store source in JS clipboard or local field if needed (parent expects data uri in DragDrop)
                // In this component we don't need to keep DraggedImageSrc permanently.
            }
            catch (Exception er)
            {
                StatusMessage = "Exception! '" + er.Message + "'";
            }
        }

        public void DragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e) { /* intentionally empty */ }

        public async Task DragDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ContactInfoModel contact)
        {
            // Minimal behavior kept for compatibility. The Students page handles drag-drop to set contact picture.
            await JSRuntime.InvokeVoidAsync("alert", "DragDrop inside modal is not supported in this component.");
        }

        // -----------------------
        // Modal lifecycle / helpers
        // -----------------------
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

        // -----------------------
        // Camera / load / delete / toolbar commands
        // -----------------------
        public async Task PerformCamera()
        {
            camera = "disableImg";
            try
            {
                await JSRuntime.InvokeVoidAsync("ShowCamera", 1);
            }
            catch
            {
                // ignore
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
                // trigger hidden input inside modal; JS fallback triggers page-level input
                await JSRuntime.InvokeVoidAsync("imageEditorModal_triggerFile", "custom-modal");
            }
            catch { /* ignore */ }

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
                // create 1x1 transparent png fallback
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
                // ignore
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

                // ask the browser for the canvas size -> use JS interop
                var rect = await JSRuntime.InvokeAsync<object>("imageEditorModal_getCanvasRect", "custom-modal");
                int canvasWidth = 0, canvasHeight = 0;

                if (rect != null)
                {
                    // rect is a JS object with width/height; use JSON roundtrip
                    var jObj = Newtonsoft.Json.Linq.JObject.FromObject(rect);
                    canvasWidth = jObj.Value<int?>("width") ?? 0;
                    canvasHeight = jObj.Value<int?>("height") ?? 0;
                }

                // If we couldn't get canvas size, fall back to canvas-relative -1 selection
                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    // old behavior (Syncfusion will try to make a reasonable selection)
                    if (ImageEditorRef != null)
                        await ImageEditorRef.SelectAsync("Square", -1, -1, -1, -1);
                }
                else
                {
                    // compute a centered square selection, 50% of the smaller dimension (tweakable)
                    int minSide = Math.Min(canvasWidth, canvasHeight);
                    int selSize = (int)Math.Round(minSide * 0.5); // 50% of smaller side
                    int selX = (canvasWidth - selSize) / 2;
                    int selY = (canvasHeight - selSize) / 2;

                    // Syncfusion SelectAsync expects pixel coords relative to the image canvas area
                    if (ImageEditorRef != null)
                        await ImageEditorRef.SelectAsync("Square", selX, selY, selSize, selSize);

                    // small delay + force resize so selection gets rendered and handlers bound
                    await Task.Delay(30);
                    try { await JSRuntime.InvokeVoidAsync("imageEditorModal_dispatchResize"); } catch { }
                    await Task.Delay(30);
                }
            }
            catch (Exception ex)
            {
                // keep UX graceful if something fails
                Console.WriteLine("PerformCrop exception: " + ex.Message);
                try
                {
                    if (ImageEditorRef != null)
                        await ImageEditorRef.SelectAsync("Square", -1, -1, -1, -1);
                }
                catch { }
            }

            // set toolbar/btn states so UI reflects selection mode
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

        // -----------------------
        // Save edited / contact image
        // -----------------------
        public async Task SaveEditedImage()
        {
            // fast guard
            if (_actionInProgress) return;

            // lightweight lock to avoid double click/race
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

                // ---------- Delete contact picture flow ----------
                if (isDeleteClicked && selectedContactId > 0)
                {
                    await ClasStarsServices.RemoveContactPicture(selectedContactId);
                    isDeleteClicked = false;
                    StatusMessage = "Picture successfully deleted";
                    if (ContactPictureUpdated.HasDelegate)
                        await ContactPictureUpdated.InvokeAsync((selectedContactId, editedImageData, true));

                    await CloseModalAfterSave();
                    return;
                }

                // ---------- Save contact picture flow ----------
                if (selectedContactId > 0)
                {
                    using var mem = new MemoryStream(editedImageData);
                    bool? IsSquare = IsSquareImage(mem, out isSquareError);
                    if (IsSquare == true)
                    {
                        await ClasStarsServices.SaveContactPicture(selectedContactId, editedImageData);
                        StatusMessage = "Picture successfully updated.";
                        if (ContactPictureUpdated.HasDelegate)
                            await ContactPictureUpdated.InvokeAsync((selectedContactId, editedImageData, false));

                        await CloseModalAfterSave();
                    }
                    else
                    {
                        await JSRuntime.InvokeVoidAsync("alert", "Failed! Picture is not square.");
                    }
                    return;
                }

                // ---------- Non-contact image (update ImageURI list) ----------
                // selectedGuid identifies which ImageDetail we are editing
                if (selectedGuid != Guid.Empty && ImageURI != null)
                {
                    // find the item
                    var item = ImageURI.FirstOrDefault(x => x.ImgId == selectedGuid);
                    if (item != null)
                    {
                        // compute data URI
                        // determine format (png/jpeg) from data — reuse helper or assume jpeg if unknown
                        using var msCheck = new MemoryStream(editedImageData);
                        var fmt = GetImageFormatFromStream(msCheck);
                        var ext = fmt == EditorImageFormat.Png ? "png" : "jpeg";
                        var dataUri = $"data:image/{ext};base64,{Convert.ToBase64String(editedImageData)}";

                        // update square status
                        using var msForSquare = new MemoryStream(editedImageData);
                        bool? isSquare = IsSquareImage(msForSquare, out isSquareError);

                        item.ImageUrl = dataUri;
                        item.IsSqu = isSquare;
                        item.IsVis = true;

                        // notify parent/listeners
                        if (ImageURIChanged.HasDelegate)
                        {
                            // pass the same list reference (parent updates a reference to it)
                            await ImageURIChanged.InvokeAsync(ImageURI);
                        }

                        // give feedback
                        StatusMessage = "Image updated.";
                        await CloseModalAfterSave();
                        return;
                    }
                    else
                    {
                        // fallback: behave as if a new image was created
                        StatusMessage = "Warning: edited image item not found in list.";
                        await CloseModalAfterSave();
                        return;
                    }
                }

                // If reached here: nothing to update (shouldn't happen)
                StatusMessage = "Nothing to save.";
            }
            catch (Exception ex)
            {
                await JSRuntime.InvokeVoidAsync("alert", "Failed! Something went wrong.\n" + ex.Message);
            }
            finally
            {
                isProcessing = false;
                _actionInProgress = false;

                // reset UI buttons
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

        // -----------------------
        // Camera capture
        // -----------------------
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
                // ignore
            }
            StateHasChanged();
        }

        // -----------------------
        // Public helpers for parent
        // -----------------------
        public async Task OpenForImage(ImageDetail imgg)
        {
            if (imgg == null) throw new ArgumentNullException(nameof(imgg));
            //if (Visible && selectedGuid == imgg.ImgId) return;

            StatusMessage = string.Empty;
            IsContact = false;
            selectedContactId = 0;
            FName = Path.GetFileNameWithoutExtension(imgg.ImageName);

            Visible = true;
            await VisibleChanged.InvokeAsync(Visible);

            // ensures modal DOM appears before JS runs
            await InvokeAsync(StateHasChanged);
            await Task.Delay(50);   // ❗ required for first click

            try { await JSRuntime.InvokeVoidAsync("openModal"); } catch { }

            // give modal time to fully attach elements
            await Task.Delay(50);
            await EnsureEditorReadyAsync(5000);

            selectedGuid = imgg.ImgId;
            try
            {
                await Task.Delay(150);
                await ImageEditorRef.OpenAsync(imgg.ImageUrl);
                // small delay and force a window resize to ensure Syncfusion renders the image
                await Task.Delay(60);
                try { await JSRuntime.InvokeVoidAsync("imageEditorModal_dispatchResize"); } catch { }
                await Task.Delay(50);
            }
            catch
            {
                // ignore open errors
            }

            cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "disableImg";
            StateHasChanged();
        }

        public async Task OpenForContact(ContactInfoModel contact)
        {
            if (contact == null) throw new ArgumentNullException(nameof(contact));
            //if (Visible && IsContact && selectedContactId == contact.ContactID) return;

            StatusMessage = string.Empty;
            isLoadImageButtonClicked = false;
            IsContact = true;
            FName = contact.FirstName + " " + contact.LastName;

            Visible = true;
            await VisibleChanged.InvokeAsync(Visible);

            // ensures modal DOM appears before JS runs
            await InvokeAsync(StateHasChanged);
            await Task.Delay(50);   // ❗ required for first click

            try { await JSRuntime.InvokeVoidAsync("openModal"); } catch { }

            // give modal time to fully attach elements
            await Task.Delay(50);
            await EnsureEditorReadyAsync(5000);


            string blankPath = PathCombine(wwwRootPath, "Images", "EditorBlank1.png");
            byte[] imageBytes;
            try
            {
                imageBytes = (contact.ContactPicture != null && contact.ContactPicture.Length > 0) ? contact.ContactPicture : null; //await File.ReadAllBytesAsync(blankPath);
            }
            catch
            {
                imageBytes = null; //Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=");
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

                        // Ensure rendering by forcing a small resize event after open
                        await Task.Delay(60);
                        try { await JSRuntime.InvokeVoidAsync("imageEditorModal_dispatchResize"); } catch { }
                        await Task.Delay(50);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            else
            {
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
}

