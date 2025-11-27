#pragma warning disable CA1416

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.JSInterop;
using My.ClasStars.Components;
using My.ClasStars.Models;
using System.Drawing;
using System.Drawing.Imaging;

namespace My.ClasStars.Pages
{
    public enum FileNamePart
    {
        None,
        PersonId,
        FirstName,
        LastName
    }

    public partial class Students : ComponentBase
    {
        // Injected services
        [Inject] protected IJSRuntime JSRuntime { get; set; }
        [Inject] protected IWebHostEnvironment WebHostEnvironment { get; set; }
        [Inject] protected IClasStarsServices ClasStarsServices { get; set; }
        [Inject] protected SchoolListService SchoolServices { get; set; }

        // File input for single-load scenario
        protected InputFile fileInputSingle;

        private ImageEditorModal editorModal;
        private bool isEditorVisible = false;

        string ShowHide = "HideRightPnl";

        bool isLoadImageButtonClicked = false;
        string refreshClass = "disableImg";

        private string DraggedImageSrc { get; set; }
        string wwwRootPath = "";
        protected string StatusMessage = "";
        private bool isProcessingFiles = false;
        private bool isSavingPicture = false;

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

        // File name format state
        protected FileNamePart formatPart1 = FileNamePart.PersonId;   // required
        protected FileNamePart formatPart2 = FileNamePart.FirstName;
        protected FileNamePart formatPart3 = FileNamePart.LastName;

        protected bool isFormatPopupVisible = false;

        // Assign popup state
        protected bool isAssignPopupVisible = false;
        protected ContactInfoModel _selectedContactForAssign;
        protected ImageDetail _selectedImageForAssign;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            wwwRootPath = WebHostEnvironment.WebRootPath;
            SchoolServices.Initialized = false;

            _contactModels = new List<ContactInfoModel>();
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

        protected void ApplyFilters()
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

        protected async Task LoadFiles(InputFileChangeEventArgs e)
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
                    await JSRuntime.InvokeVoidAsync("alert", StringsResource.Common_SingleImageLimit);
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
                        await editorModal.OpenForImage(new ImageDetail
                        {
                            ImgId = Guid.NewGuid(),
                            ImageName = file.Name,
                            ImageUrl = imgUrl
                        });
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

                        // Make sure these properties exist in ImageDetail:
                        // public bool IsMatched { get; set; }
                        // public int? MatchedContactId { get; set; }
                        ImgDetail.IsMatched = false;
                        ImgDetail.MatchedContactId = null;

                        ImageURI.Add(ImgDetail);
                    }

                    ms.Close();
                    await stream.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(StringsResource.Common_GenericErrorWithDetail, ex.Message);
            }
            finally
            {
                isProcessingFiles = false;

                // Run matching AFTER files are loaded
                EvaluateImageMatches();

                StateHasChanged();
            }
        }

        public bool? IsSquareImage(MemoryStream stream, out string err)
        {
            err = "";
            bool? result;
            try
            {
                var image = Image.FromStream(stream);
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

        protected void DragStart(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ImageDetail iDetail)
        {
            try
            {
                DraggedImageSrc = iDetail.ImageUrl;
            }
            catch (Exception er)
            {
                StatusMessage = string.Format(StringsResource.Common_ExceptionWithDetail, er.Message);
            }
        }

        protected void DragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e) { }

        protected async Task DragDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ContactInfoModel contact)
        {
            try
            {
                isSavingPicture = true;
                StateHasChanged();

                ContactInfoModel cim = _contactModels.Find(c => c.ContactID == contact.ContactID);
                if (cim == null)
                {
                    StatusMessage = StringsResource.Students_Status_ContactNotFound;
                    return;
                }

                Regex regex = new Regex(@"^[\w/\:.-]+;base64,");
                var payload = string.IsNullOrEmpty(DraggedImageSrc) ? string.Empty : regex.Replace(DraggedImageSrc, string.Empty);

                if (string.IsNullOrEmpty(payload))
                {
                    StatusMessage = StringsResource.Students_Status_NoImageData;
                    return;
                }

                byte[] bytesData = Convert.FromBase64String(payload);
                using MemoryStream memoryStream = new(bytesData);
                bool? IsSquare = IsSquareImage(memoryStream, out isSquareError);
                if (IsSquare == true)
                {
                    cim.ContactPicture = bytesData;
                    await ClasStarsServices.SaveContactPicture(contact.ContactID, bytesData);
                    StatusMessage = StringsResource.Common_StatusPictureUpdated;

                    ApplyFilters();
                }
                else
                {
                    StatusMessage = StringsResource.Students_Status_NotSquare;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(StringsResource.Students_Status_SaveError, ex.Message);
            }
            finally
            {
                isSavingPicture = false;
                StateHasChanged();
            }
        }

        protected async Task LoadImage(ImageDetail imgg)
        {
            if (editorModal == null)
            {
                StatusMessage = StringsResource.Students_Status_EditorUnavailable;
                return;
            }
            await editorModal.OpenForImage(imgg);
        }

        protected async Task LoadContact(ContactInfoModel contact)
        {
            if (editorModal == null)
            {
                StatusMessage = StringsResource.Students_Status_EditorUnavailable;
                return;
            }
            await editorModal.OpenForContact(contact);
        }

        protected void GetFilterList()
        {
            ApplyFilters();
        }

        protected void FilterList(string studentname)
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

        protected Task OnImageUriChanged(List<ImageDetail> newList)
        {
            ImageURI = newList;
            StateHasChanged();
            return Task.CompletedTask;
        }

        protected Task OnContactPictureUpdated((int ContactId, byte[] Picture, bool IsDeleted) info)
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
            StatusMessage = StringsResource.Common_StatusPictureUpdated;

            ApplyFilters();

            return Task.CompletedTask;
        }

        protected void All()
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

        // ===== File Name Format helpers =====

        protected string GetCurrentFormatPreview()
        {
            string PartToString(FileNamePart p) => p switch
            {
                FileNamePart.PersonId => "PersonID",
                FileNamePart.FirstName => "FirstName",
                FileNamePart.LastName => "LastName",
                _ => "None"
            };

            var parts = new List<string>();
            parts.Add(PartToString(formatPart1));
            if (formatPart2 != FileNamePart.None) parts.Add(PartToString(formatPart2));
            if (formatPart3 != FileNamePart.None) parts.Add(PartToString(formatPart3));

            return string.Join("-", parts);
        }

        protected void SaveFileNameFormat()
        {
            // First part cannot be None
            if (formatPart1 == FileNamePart.None)
            {
                formatPart1 = FileNamePart.PersonId;
            }

            isFormatPopupVisible = false;

            // Only run matching when format is changed (and images exist)
            if (ImageURI != null && ImageURI.Count > 0)
            {
                EvaluateImageMatches();
            }
        }

        private void EvaluateImageMatches()
        {
            if (ImageURI == null || ImageURI.Count == 0 || _contactModels == null || _contactModels.Count == 0)
                return;

            // Reset matches
            foreach (var img in ImageURI)
            {
                img.IsMatched = false;
                img.MatchedContactId = null;
            }

            // Check only visible images (as per requirement)
            var visibleImages = ImageURI.Where(i => i.IsVis).ToList();

            foreach (var img in visibleImages)
            {
                if (string.IsNullOrWhiteSpace(img.ImageName))
                    continue;

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(img.ImageName);
                var match = FindContactForFileName(fileNameWithoutExt);

                if (match != null)
                {
                    img.IsMatched = true;
                    img.MatchedContactId = match.ContactID;
                }
            }

            // Sort: matched visible images should appear first in ImageURI order
            ImageURI = ImageURI
                .OrderByDescending(i => i.IsVis && i.IsMatched)
                .ThenBy(i => i.ImageName)
                .ToList();
        }

        private ContactInfoModel FindContactForFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // Split on any non-alphanumeric chars: handles -, _, space, etc.
            var tokens = Regex.Split(fileName, "[^A-Za-z0-9]+")
                              .Where(t => !string.IsNullOrWhiteSpace(t))
                              .ToList();

            if (tokens.Count == 0)
                return null;

            string personIdToken = null;
            string firstNameToken = null;
            string lastNameToken = null;

            var parts = new[] { formatPart1, formatPart2, formatPart3 };
            int tokenIndex = 0;

            for (int i = 0; i < parts.Length && tokenIndex < tokens.Count; i++)
            {
                var part = parts[i];
                var token = tokens[tokenIndex];

                if (part == FileNamePart.None)
                {
                    tokenIndex++;
                    continue;
                }

                switch (part)
                {
                    case FileNamePart.PersonId:
                        personIdToken = token;
                        break;
                    case FileNamePart.FirstName:
                        firstNameToken = token;
                        break;
                    case FileNamePart.LastName:
                        lastNameToken = token;
                        break;
                }

                tokenIndex++;
            }

            foreach (var c in _contactModels)
            {
                bool ok = true;

                if (!string.IsNullOrEmpty(personIdToken))
                {
                    var personIdStr = c.PersonID?.ToString() ?? string.Empty;
                    if (!personIdStr.Equals(personIdToken, StringComparison.OrdinalIgnoreCase))
                        ok = false;
                }

                if (ok && !string.IsNullOrEmpty(firstNameToken))
                {
                    if (!string.Equals(c.FirstName ?? string.Empty, firstNameToken, StringComparison.OrdinalIgnoreCase))
                        ok = false;
                }

                if (ok && !string.IsNullOrEmpty(lastNameToken))
                {
                    if (!string.Equals(c.LastName ?? string.Empty, lastNameToken, StringComparison.OrdinalIgnoreCase))
                        ok = false;
                }

                if (ok)
                    return c;
            }

            return null;
        }

        // ===== Image & Student click handlers =====

        protected void OnImageClicked(ImageDetail img)
        {
            StatusMessage = "";

            if (img == null || !img.IsMatched || !img.MatchedContactId.HasValue)
                return;

            var contact = _contactModels.FirstOrDefault(c => c.ContactID == img.MatchedContactId.Value);
            if (contact == null)
                return;

            _selectedContactForAssign = contact;
            _selectedImageForAssign = img;
            isAssignPopupVisible = true;
        }

        protected void OnStudentClicked(ContactInfoModel student)
        {
            StatusMessage = "";

            if (student == null)
                return;

            var img = ImageURI.FirstOrDefault(i => i.IsMatched && i.MatchedContactId == student.ContactID && i.IsVis);
            if (img == null)
                return;

            _selectedContactForAssign = student;
            _selectedImageForAssign = img;
            isAssignPopupVisible = true;
        }

        protected void CancelAssign()
        {
            isAssignPopupVisible = false;
            _selectedContactForAssign = null;
            _selectedImageForAssign = null;
        }

        protected async Task ConfirmAssign()
        {
            if (_selectedContactForAssign == null || _selectedImageForAssign == null)
                return;

            try
            {
                // Enforce square image before saving
                if (!_selectedImageForAssign.IsSqu.HasValue || !_selectedImageForAssign.IsSqu.Value)
                {
                    StatusMessage = StringsResource.Students_Status_NotSquare;

                    // Close popup and open editor so user can crop
                    isAssignPopupVisible = false;
                    StateHasChanged();

                    if (editorModal != null)
                    {
                        await editorModal.OpenForImage(_selectedImageForAssign);
                    }

                    return;
                }

                isSavingPicture = true;
                StateHasChanged();

                Regex regex = new Regex(@"^[\w/\:.-]+;base64,");
                var payload = string.IsNullOrEmpty(_selectedImageForAssign.ImageUrl)
                    ? string.Empty
                    : regex.Replace(_selectedImageForAssign.ImageUrl, string.Empty);

                if (string.IsNullOrEmpty(payload))
                {
                    StatusMessage = StringsResource.Students_Status_NoImageData;
                    return;
                }

                byte[] bytesData = Convert.FromBase64String(payload);

                _selectedContactForAssign.ContactPicture = bytesData;
                await ClasStarsServices.SaveContactPicture(_selectedContactForAssign.ContactID, bytesData);
                StatusMessage = StringsResource.Common_StatusPictureUpdated;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(StringsResource.Students_Status_SaveError, ex.Message);
            }
            finally
            {
                isSavingPicture = false;
                isAssignPopupVisible = false;
                _selectedContactForAssign = null;
                _selectedImageForAssign = null;
                StateHasChanged();
            }
        }
    }
}
