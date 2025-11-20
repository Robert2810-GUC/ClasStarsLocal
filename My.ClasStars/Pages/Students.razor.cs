
#pragma warning disable CA1416

namespace My.ClasStars.Pages
{
    public partial class Students : ComponentBase
    {

        InputFile fileInputSingle;
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected IWebHostEnvironment WebHostEnvironment { get; set; }
        [Inject] protected IClasStarsServices ClasStarsServices { get; set; }
        [Inject] protected SchoolListService SchoolServices { get; set; }

        private ImageEditorModal editorModal;
        private bool isEditorVisible = false;

        string ShowHide = "HideRightPnl";

        bool isLoadImageButtonClicked = false;
        string refreshClass = "disableImg";

        private string DraggedImageSrc { get; set; }
        string wwwRootPath = "";
        string StatusMessage = "";
        private bool isProcessingFiles = false;

        private List<ContactInfoShort> _contacts;
        public List<ContactInfoModel> _contactModels;
        public string refreshPath = "";

        [Parameter]
        public List<ImageDetail> ImageURI { get; set; } = new();

        private List<ContactInfoModel> _displayContactModels = new();

        string value = "";

        private bool filterWithImage = true;
        private bool filterWithoutImage = true;

        public static string isSquareError = "";

        public List<ImageEditorToolbarItemModel> customToolbarItem = new List<ImageEditorToolbarItemModel>();

        private readonly object _saveLock = new();

        protected async override Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            wwwRootPath = WebHostEnvironment.WebRootPath;
            SchoolServices.Initialized = false;

            _contactModels = new();
            if (AppInfo.UserInfo != null)
            {
                _contacts = await ClasStarsServices.GetStudentList(AppInfo.UserInfo.Organization.ID);
                foreach (var contactInfoShort in _contacts)
                {
                    _contactModels.Add(new ContactInfoModel(contactInfoShort, ClasStarsServices));
                }

                InitializeDisplayList();

                if (HomePage.IsAdmin)
                {
                    ShowHide = "ShowRightPnl";
                }
                else
                {
                    ShowHide = "HideRightPnl";
                }
            }
            else
            {
                throw new ArgumentNullException(StringsResource.UserNotFound);
            }

            refreshPath = Path.Combine(wwwRootPath, "\\images\\refresh.png");
        }

        private void InitializeDisplayList()
        {
            if (_contactModels == null) _contactModels = new List<ContactInfoModel>();
            _displayContactModels = _contactModels.ToList();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            var search = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            bool bothSame = (filterWithImage && filterWithoutImage) || (!filterWithImage && !filterWithoutImage);

            IEnumerable<ContactInfoModel> query = _contactModels;

            if (!bothSame)
            {
                if (filterWithImage && !filterWithoutImage)
                {
                    query = query.Where(c => c.ContactPicture != null && c.ContactPicture.Length > 0);
                }
                else if (!filterWithImage && filterWithoutImage)
                {
                    query = query.Where(c => c.ContactPicture == null || c.ContactPicture.Length == 0);
                }
            }
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c =>
                    (!string.IsNullOrEmpty(c.ExternalSourceID) &&
                     c.ExternalSourceID.Equals(search, StringComparison.OrdinalIgnoreCase))
                    ||
                    (c.FirstName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    ||
                    (c.LastName?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    ||
                    (c.FileAs?.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                );
            }

            _displayContactModels = query.ToList();
            InvokeAsync(StateHasChanged);
        }



        private async Task LoadFiles(InputFileChangeEventArgs e)
        {
            StatusMessage = "";
            isSquareError = string.Empty;
            isProcessingFiles = true;                 
            StateHasChanged();

            try
            {
                var maxFiles = isLoadImageButtonClicked ? 1 : 100;
                IReadOnlyList<IBrowserFile> files;
                if (maxFiles == 1 && e.FileCount > 1)
                {
                    await JSRuntime.InvokeVoidAsync("alert", "You can only select one image at a time.");
                    isProcessingFiles = false;
                    StateHasChanged();
                    return;
                }
                else
                {
                    files = e.GetMultipleFiles(maxFiles);
                    if (!isLoadImageButtonClicked)       
                    {
                        ImageURI.Clear();
                    }
                }

                foreach (var file in files)
                {
                    var stream = file.OpenReadStream(1024 * 1024 * 30);
                    using MemoryStream ms = new();
                    await stream.CopyToAsync(ms);

                    var format = ImageHelpers.GetImageFormatFromStream(ms);    
                    var isSquare = ImageHelpers.IsSquareImage(ms, out isSquareError);

                    var imgUrl = $"data:image/{format.ToString().ToLower()};base64,{Convert.ToBase64String(ms.ToArray())}";

                    if (isLoadImageButtonClicked)
                    {
                        await editorModal.OpenForImage(new ImageDetail { ImgId = Guid.NewGuid(), ImageName = file.Name, ImageUrl = imgUrl });
                    }
                    else
                    {
                        ImageDetail ImgDetail = new()
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

                    ms.Close();
                    await stream.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Something went wrong: " + ex.Message;
            }
            finally
            {
                isProcessingFiles = false;           
                StateHasChanged();
            }
        }

        public bool? IsSquareImage(MemoryStream stream, out string err)
        {
            err = "";
            bool? result;
            try
            {
                var image = System.Drawing.Image.FromStream(stream);
                result = IsDifferenceUpTo2(image.Width, image.Height);
            }
            catch (Exception eet)
            {
                err = eet.Message;
                result = null;
            }
            return result;
        }

        public static bool IsDifferenceUpTo2(int number1, int number2)
        {
            int difference1 = Math.Abs(number1 - number2);
            int difference2 = Math.Abs(number2 - number1);
            return difference1 <= 2 || difference2 <= 2;
        }

        public static ImageFormat GetImageFormat(MemoryStream ms)
        {
            byte[] header = new byte[8];
            ms.Position = 0;
            _ = ms.Read(header, 0, header.Length);
            ms.Position = 0;

            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return ImageFormat.Jpeg;
            if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A) return ImageFormat.Png;
            return ImageFormat.Jpeg;
        }

        private void DragStart(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ImageDetail iDetail)
        {
            try
            {
                DraggedImageSrc = iDetail.ImageUrl;
            }
            catch (Exception er)
            {
                StatusMessage = "Exception! '" + er.Message + "'";
            }
        }

        private void DragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e) { }

        private async void DragDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ContactInfoModel contact)
        {
            try
            {
                ContactInfoModel cim = _contactModels.Find(c => c.ContactID == contact.ContactID);
                if (cim == null)
                {
                    StatusMessage = "Contact not found.";
                    StateHasChanged();
                    return;
                }

                Regex regex = new Regex(@"^[\w/\:.-]+;base64,");
                var payload = string.IsNullOrEmpty(DraggedImageSrc) ? string.Empty : regex.Replace(DraggedImageSrc, string.Empty);

                if (string.IsNullOrEmpty(payload))
                {
                    StatusMessage = "No image data found to drop.";
                    StateHasChanged();
                    return;
                }

                byte[] bytesData = Convert.FromBase64String(payload);
                using MemoryStream memoryStream = new MemoryStream(bytesData);
                bool? IsSquare = IsSquareImage(memoryStream, out isSquareError);
                if (IsSquare == true)
                {
                    cim.ContactPicture = bytesData;
                    await ClasStarsServices.SaveContactPicture(contact.ContactID, bytesData);
                    StatusMessage = "Picture successfully updated.";

                    ApplyFilters();
                }
                else
                {
                    StatusMessage = "Failed! Picture is not square.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Something went wrong. " + ex.Message;
            }
            StateHasChanged();
        }

        private async Task LoadImage(ImageDetail imgg)
        {
            if (editorModal == null)
            {
                StatusMessage = "Editor modal reference not available.";
                return;
            }
            await editorModal.OpenForImage(imgg);
        }

        private async Task LoadContact(ContactInfoModel contact)
        {
            if (editorModal == null)
            {
                StatusMessage = "Editor modal reference not available.";
                return;
            }
            await editorModal.OpenForContact(contact);
        }

        private void GetFilterList()
        {
            ApplyFilters();
        }

       

        private void FilterList(string studentname)
        {
            StatusMessage = "";
            int VisibleItemCoucnt = (from c in ImageURI where c.IsVis == true select c).ToList().Count;
            if (ImageURI.Count > 0 && ImageURI.Count > VisibleItemCoucnt)
            {
                refreshClass = "enablerImg";
            }
            else
            {
                refreshClass = "disableImg";
            }
            StateHasChanged();
        }

        private Task OnImageUriChanged(List<ImageDetail> newList)
        {
            ImageURI = newList;
            StateHasChanged();
            return Task.CompletedTask;
        }

        private Task OnContactPictureUpdated((int ContactId, byte[] Picture, bool IsDeleted) info)
        {
            var (contactId, picture, IsDeleted) = info;
            var item = _contactModels.FirstOrDefault(c => c.ContactID == contactId);
            if (item != null)
            {
                if (!IsDeleted)
                {
                    item.ContactPicture = picture;
                }
                else
                {
                    item.ContactPicture = null;
                }
            }
            StatusMessage = "Picture successfully updated.";

            ApplyFilters();

            return Task.CompletedTask;
        }

        private void All()
        {
            StatusMessage = "";
            foreach (ImageDetail detail in ImageURI)
            {
                detail.IsVis = true;
            }
            int VisibleItemCoucnt = (from c in ImageURI where c.IsVis == true select c).ToList().Count;
            if (ImageURI.Count > 0 && ImageURI.Count > VisibleItemCoucnt)
            {
                refreshClass = "enablerImg";
            }
            else
            {
                refreshClass = "disableImg";
            }
            StateHasChanged();
        }

    }

    
}


