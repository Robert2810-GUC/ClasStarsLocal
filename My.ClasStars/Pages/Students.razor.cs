#pragma warning disable CA1416

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using Microsoft.JSInterop;
using My.ClasStars.Components;
using My.ClasStars.Models;
using Syncfusion.Blazor.Notifications;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        [Inject] protected ToastService ToastService { get; set; }

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
        private bool ShowPageOverlay => isSavingPicture || isProcessingFiles;
        private bool _isStudentListFiltered = false;
        private string _activeFilterLabel = string.Empty;
        private HashSet<int> _filteredContactIds = new();
        private bool enableLooseMatching = true;

        private List<ContactInfoShort> _contacts;
        public List<ContactInfoModel> _contactModels;
        public string refreshPath = "";

        [Parameter]
        public List<ImageDetail> ImageURI { get; set; } = new();

        private List<ContactInfoModel> _displayContactModels = new();
        private List<ContactTokens> _contactTokens = new();
        private Dictionary<int, ContactInfoModel> _contactLookup = new();

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
        protected List<ImageDetail> _matchedImagesForAssign = new();
        private Guid? _selectedMatchOptionId;

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

                BuildContactTokens();

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

        private void BuildContactTokens()
        {
            if (_contactModels == null)
            {
                _contactTokens = new List<ContactTokens>();
                _contactLookup = new Dictionary<int, ContactInfoModel>();
                return;
            }

            _contactLookup = _contactModels.ToDictionary(c => c.ContactID);

            _contactTokens = _contactModels
                .Select(c => new ContactTokens
                {
                    ID = c.ContactID,
                    FirstName = c.FirstName?.Trim() ?? string.Empty,
                    LastName = c.LastName?.Trim() ?? string.Empty,
                    PersonID = c.PersonID?.Trim() ?? string.Empty
                })
                .ToList();
        }

        private void NotifyStatus(string message, ToastLevel level = ToastLevel.Info, int duration = 4000)
        {
            StatusMessage = message;
            ToastService?.ShowToast(message, level, duration);
        }

        protected void ApplyFilters()
        {
            var search = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            bool bothSame = (filterWithImage && filterWithoutImage) || (!filterWithImage && !filterWithoutImage);

            IEnumerable<ContactInfoModel> query = _contactModels;

            if (_isStudentListFiltered && _filteredContactIds.Count > 0)
            {
                query = query.Where(c => _filteredContactIds.Contains(c.ContactID));
            }

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
                NotifyStatus(string.Format(StringsResource.Common_GenericErrorWithDetail, ex.Message), ToastLevel.Error);
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
                    NotifyStatus(StringsResource.Students_Status_ContactNotFound, ToastLevel.Error);
                    return;
                }

                Regex regex = new Regex(@"^[\w/\:.-]+;base64,");
                var payload = string.IsNullOrEmpty(DraggedImageSrc) ? string.Empty : regex.Replace(DraggedImageSrc, string.Empty);

                if (string.IsNullOrEmpty(payload))
                {
                    NotifyStatus(StringsResource.Students_Status_NoImageData, ToastLevel.Error);
                    return;
                }

                byte[] bytesData = Convert.FromBase64String(payload);
                using MemoryStream memoryStream = new(bytesData);
                bool? IsSquare = IsSquareImage(memoryStream, out isSquareError);
                if (IsSquare == true)
                {
                    cim.ContactPicture = bytesData;
                    await ClasStarsServices.SaveContactPicture(contact.ContactID, bytesData);
                    NotifyStatus(StringsResource.Common_StatusPictureUpdated, ToastLevel.Success);

                    ApplyFilters();
                }
                else
                {
                    NotifyStatus(StringsResource.Students_Status_NotSquare, ToastLevel.Error);
                }
            }
            catch (Exception ex)
            {
                NotifyStatus(string.Format(StringsResource.Students_Status_SaveError, ex.Message), ToastLevel.Error);
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
                NotifyStatus(StringsResource.Students_Status_EditorUnavailable, ToastLevel.Error);
                return;
            }
            await editorModal.OpenForImage(imgg);
        }

        protected async Task LoadContact(ContactInfoModel contact)
        {
            if (editorModal == null)
            {
                NotifyStatus(StringsResource.Students_Status_EditorUnavailable, ToastLevel.Error);
                return;
            }
            await editorModal.OpenForContact(contact);
        }

        protected void GetFilterList()
        {
            ApplyFilters();
        }


        protected void testToast()
        {
            ToastService?.ShowToast("message testing", ToastLevel.Success);
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

        protected void ClearStudentFilter()
        {
            _filteredContactIds.Clear();
            _isStudentListFiltered = false;
            _activeFilterLabel = string.Empty;
            ApplyFilters();
        }

        private void FilterStudentsForImage(ImageDetail img)
        {
            if (img == null || string.IsNullOrWhiteSpace(img.ImageName))
            {
                ClearStudentFilter();
                return;
            }

            var tokens = ParseTokensFromFileName(Path.GetFileNameWithoutExtension(img.ImageName));
            if (tokens == null)
            {
                ClearStudentFilter();
                return;
            }

            var matches = GetMatchesForTokens(tokens)
                .Where(m => m.IsMatch || m.Score >= 0.45)
                .OrderByDescending(m => m.Score)
                .Take(20)
                .ToList();

            _filteredContactIds = matches.Select(m => m.Contact.ContactID).ToHashSet();
            _isStudentListFiltered = _filteredContactIds.Count > 0;
            _activeFilterLabel = Path.GetFileName(img.ImageName);
            ApplyFilters();
        }

        private void FilterImagesForStudent(ContactInfoModel student, bool openAssignPopup)
        {
            if (student == null || ImageURI == null)
                return;

            EvaluateImageMatches();

            var matchedImages = ImageURI.Where(i => i.IsMatched && i.MatchedContactId == student.ContactID).ToList();

            if (matchedImages.Count == 0)
            {
                _selectedContactForAssign = null;
                _selectedImageForAssign = null;
                _matchedImagesForAssign = new();
                return;
            }

            foreach (var img in ImageURI)
            {
                img.IsVis = matchedImages.Contains(img);
            }

            refreshClass = ImageURI.Count > matchedImages.Count ? "enablerImg" : "disableImg";

            if (openAssignPopup)
            {
                OpenAssignPopup(student, matchedImages, adjustVisibleImages: false);
            }
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
            NotifyStatus(StringsResource.Common_StatusPictureUpdated, ToastLevel.Success);

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

        protected string GetCurrentFormatExample()
        {
            string PartToExample(FileNamePart p) => p switch
            {
                FileNamePart.PersonId => "12345",
                FileNamePart.FirstName => "First Name",
                FileNamePart.LastName => "Last Name",
                _ => string.Empty
            };

            var parts = new List<string>();
            parts.Add(PartToExample(formatPart1));
            if (formatPart2 != FileNamePart.None) parts.Add(PartToExample(formatPart2));
            if (formatPart3 != FileNamePart.None) parts.Add(PartToExample(formatPart3));

            return string.Join("-", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
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

            var visibleImages = ImageURI.Where(i => i.IsVis).ToList();

            foreach (var img in visibleImages)
            {
                if (string.IsNullOrWhiteSpace(img.ImageName))
                    continue;

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(img.ImageName);
                var tokens = ParseTokensFromFileName(fileNameWithoutExt);
                if (tokens == null)
                    continue;

                var match = GetBestContactMatch(tokens);

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

        private ContactTokens ParseTokensFromFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var tokenMatches = Regex.Matches(fileName, "[\\p{L}\\p{Nd}]+");
            var tokens = tokenMatches.Select(m => m.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

            if (tokens.Count == 0)
                return null;

            var formatParts = new[] { formatPart1, formatPart2, formatPart3 };
            var lastIndex = Array.FindLastIndex(formatParts, f => f != FileNamePart.None);

            if (lastIndex < 0)
                return null;

            int tokenIndex = 0;
            ContactTokens parsed = new();

            for (int i = 0; i < formatParts.Length && tokenIndex < tokens.Count; i++)
            {
                var part = formatParts[i];
                if (part == FileNamePart.None)
                    continue;

                bool isLastPart = i == lastIndex;
                string tokenValue = isLastPart
                    ? string.Join(" ", tokens.Skip(tokenIndex))
                    : tokens[tokenIndex];

                if (!isLastPart)
                {
                    tokenIndex++;
                }
                else
                {
                    tokenIndex = tokens.Count;
                }

                switch (part)
                {
                    case FileNamePart.PersonId:
                        parsed.PersonID = tokenValue;
                        break;
                    case FileNamePart.FirstName:
                        parsed.FirstName = tokenValue;
                        break;
                    case FileNamePart.LastName:
                        parsed.LastName = tokenValue;
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(parsed.PersonID)
                && string.IsNullOrWhiteSpace(parsed.FirstName)
                && string.IsNullOrWhiteSpace(parsed.LastName))
            {
                return null;
            }

            return parsed;
        }

        private ContactInfoModel GetBestContactMatch(ContactTokens tokens)
        {
            if (tokens == null || _contactTokens == null || _contactTokens.Count == 0)
                return null;

            var matches = GetMatchesForTokens(tokens)
                .Where(m => m.IsMatch)
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Contact.LastName)
                .ToList();

            return matches.FirstOrDefault()?.Contact;
        }

        private IEnumerable<ContactMatchResult> GetMatchesForTokens(ContactTokens tokens)
        {
            foreach (var candidate in _contactTokens)
            {
                var score = CalculateMatchScore(candidate, tokens);
                if (!_contactLookup.TryGetValue(candidate.ID, out var contact))
                    continue;

                yield return new ContactMatchResult
                {
                    Contact = contact,
                    Score = score.Score,
                    IsMatch = score.IsMatch
                };
            }
        }

        private MatchScore CalculateMatchScore(ContactTokens candidate, ContactTokens tokens)
        {
            double score = 0;
            int checks = 0;
            bool personMatch = false;

            if (!string.IsNullOrWhiteSpace(tokens.PersonID))
            {
                checks++;

                if (!string.IsNullOrWhiteSpace(candidate.PersonID)
                    && candidate.PersonID.Equals(tokens.PersonID, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1.5;
                    personMatch = true;
                }
                else
                {
                    return new MatchScore(0, false);
                }
            }

            if (!string.IsNullOrWhiteSpace(tokens.FirstName))
            {
                checks++;
                score += CalculateNameSimilarity(candidate.FirstName, tokens.FirstName);
            }

            if (!string.IsNullOrWhiteSpace(tokens.LastName))
            {
                checks++;
                score += CalculateNameSimilarity(candidate.LastName, tokens.LastName);
            }

            if (checks == 0)
            {
                return new MatchScore(0, false);
            }

            double normalized = score / checks;
            double threshold = enableLooseMatching ? 0.55 : 0.9;
            bool isMatch = personMatch || normalized >= threshold;

            return new MatchScore(normalized, isMatch);
        }

        private double CalculateNameSimilarity(string expected, string token)
        {
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(token))
                return 0;

            if (string.Equals(expected, token, StringComparison.OrdinalIgnoreCase))
                return 1;

            if (!enableLooseMatching)
                return 0;

            var a = expected.ToLowerInvariant();
            var b = token.ToLowerInvariant();
            var distance = GetLevenshteinDistance(a, b);
            var maxLength = Math.Max(a.Length, b.Length);

            return maxLength == 0 ? 0 : 1.0 - (double)distance / maxLength;
        }

        private int GetLevenshteinDistance(string source, string target)
        {
            int n = source.Length;
            int m = target.Length;
            int[,] d = new int[n + 1, m + 1];

            if (n == 0) return m;
            if (m == 0) return n;

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        private class ContactMatchResult
        {
            public ContactInfoModel Contact { get; set; }
            public double Score { get; set; }
            public bool IsMatch { get; set; }
        }

        private record MatchScore(double Score, bool IsMatch);

        // ===== Image & Student click handlers =====

        protected IEnumerable<ImageDetail> GetMatchedImagesForContact(int contactId)
        {
            if (ImageURI == null || ImageURI.Count == 0)
            {
                return Enumerable.Empty<ImageDetail>();
            }

            return ImageURI.Where(i => i.IsMatched && i.MatchedContactId == contactId);
        }

        protected void OnImageClicked(ImageDetail img)
        {
            StatusMessage = "";

            if (img == null)
                return;

            FilterStudentsForImage(img);
        }

        protected void OnStudentClicked(ContactInfoModel student)
        {
            StatusMessage = "";

            if (student == null)
                return;

            FilterImagesForStudent(student, openAssignPopup: false);
        }

        protected void OnStudentMatchIconClicked(ContactInfoModel student)
        {
            StatusMessage = "";

            if (student == null)
                return;

            FilterImagesForStudent(student, openAssignPopup: true);
        }

        protected void OnMatchIconClicked(ImageDetail img)
        {
            StatusMessage = "";

            if (img == null || !img.IsMatched || !img.MatchedContactId.HasValue)
                return;

            if (!_contactLookup.TryGetValue(img.MatchedContactId.Value, out var contact))
                return;

            EvaluateImageMatches();
            var matchedImages = ImageURI.Where(i => i.IsMatched && i.MatchedContactId == contact.ContactID).ToList();
            OpenAssignPopup(contact, matchedImages, adjustVisibleImages: false);
        }

        private void OpenAssignPopup(ContactInfoModel student, List<ImageDetail> matchedImages, bool adjustVisibleImages)
        {
            if (student == null || matchedImages == null)
                return;

            if (matchedImages.Count == 0)
            {
                _selectedContactForAssign = null;
                _selectedImageForAssign = null;
                _matchedImagesForAssign = new();
                _selectedMatchOptionId = null;
                isAssignPopupVisible = false;
                return;
            }

            if (adjustVisibleImages)
            {
                foreach (var img in ImageURI)
                {
                    img.IsVis = matchedImages.Contains(img);
                }

                refreshClass = ImageURI.Count > matchedImages.Count ? "enablerImg" : "disableImg";
            }

            _selectedContactForAssign = student;
            _matchedImagesForAssign = matchedImages;
            _selectedMatchOptionId = matchedImages.First().ImgId;
            _selectedImageForAssign = matchedImages.First();
            isAssignPopupVisible = true;
            StateHasChanged();
        }

        protected void OnMatchOptionSelected(Guid optionId)
        {
            _selectedMatchOptionId = optionId;
            _selectedImageForAssign = _matchedImagesForAssign.FirstOrDefault(m => m.ImgId == optionId);
            StateHasChanged();
        }

        protected void CancelAssign()
        {
            isAssignPopupVisible = false;
            _selectedContactForAssign = null;
            _selectedImageForAssign = null;
            _matchedImagesForAssign = new();
            _selectedMatchOptionId = null;
        }

        protected async Task ConfirmAssign()
        {
            if (_selectedContactForAssign == null || _selectedImageForAssign == null)
                return;

            try
            {
                if (_selectedMatchOptionId.HasValue)
                {
                    _selectedImageForAssign = _matchedImagesForAssign.FirstOrDefault(m => m.ImgId == _selectedMatchOptionId.Value);
                }

                if (_selectedImageForAssign == null)
                {
                    NotifyStatus(StringsResource.Students_Status_NoImageData, ToastLevel.Error);
                    return;
                }

                // Enforce square image before saving
                if (!_selectedImageForAssign.IsSqu.HasValue || !_selectedImageForAssign.IsSqu.Value)
                {
                    NotifyStatus(StringsResource.Students_Status_NotSquare, ToastLevel.Error);

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
                    NotifyStatus(StringsResource.Students_Status_NoImageData, ToastLevel.Error);
                    return;
                }

                byte[] bytesData = Convert.FromBase64String(payload);

                _selectedContactForAssign.ContactPicture = bytesData;
                await ClasStarsServices.SaveContactPicture(_selectedContactForAssign.ContactID, bytesData);
                NotifyStatus(StringsResource.Common_StatusPictureUpdated, ToastLevel.Success);

                ApplyFilters();
            }
            catch (Exception ex)
            {
                NotifyStatus(string.Format(StringsResource.Students_Status_SaveError, ex.Message), ToastLevel.Error);
            }
            finally
            {
                isSavingPicture = false;
                isAssignPopupVisible = false;
                _selectedContactForAssign = null;
                _selectedImageForAssign = null;
                _matchedImagesForAssign = new();
                _selectedMatchOptionId = null;
                StateHasChanged();
            }
        }
    }
}
