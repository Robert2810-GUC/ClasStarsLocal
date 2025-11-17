using System.Collections.Generic;
using System.Threading.Tasks;
using SharedTypes;
using My.ClasStars.Models;
using My.ClasStars.Resources;
using System;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Forms;
using System.Net.Http;
using System.Linq;
using System.IO;
using Syncfusion.Blazor.ImageEditor;
using System.Drawing;
using System.Text.RegularExpressions;

#pragma warning disable CA1416

namespace My.ClasStars.Pages;

public partial class Students
{
    public enum ImageFormat
    {
        Unknown,
        Jpeg,
        Png,
        // Add more formats as needed
    }

    // Right Panel
    string ShowHide = "HideRightPnl";

    // Camera Button and Div
    string camera = "enablerImg";
    string cameraVisiblility = "cameraSzeHidden";

    // Load and Delete Picture
    bool isLoadImageButtonClicked = false;
    bool isDeleteClicked = false;

    // Functionality Buttons
    string cropClass = "enablerImg";
    string acceptClass = "disableImg";
    string rotateClass = "disableImg";
    string saveClass = "disableImg";
    string undoClass = "disableImg";
    string refreshClass = "disableImg";

    InputFile fileInputSingle;
    private string DraggedImageSrc { get; set; }
    // private bool _isDragging = false;
    string wwwRootPath = "";
    int selectedContactId = 0;
    string StatusMessage = "";
    Guid selectedGuid;
    string FName = "";
    bool IsContact = false;
    /////////////////////////////////
    private List<ContactInfoShort> _contacts;
    public static List<ContactInfoModel> _contactModels;
    public static string refreshPath = "";
    protected async override Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        wwwRootPath = WebHostEnvironment.WebRootPath;
        SchoolServices.Initialized = false;
        _contactModels = new();
        if (AppInfo.UserInfo != null)
        {
            _contacts = await ClasStarsServices.GetStudentList(AppInfo.UserInfo.Organization.ID);
            //_contacts = await ClasStarsServices.GetStudentList(SchoolServices.SelectedSchoolIds[0]);
            foreach (var contactInfoShort in _contacts)
            {
                _contactModels.Add(new ContactInfoModel(contactInfoShort, ClasStarsServices));
            }

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

    public List<ImageEditorToolbarItemModel> customToolbarItem = new List<ImageEditorToolbarItemModel>();

    SfImageEditor ImageEditorRef;
    List<ImageDetail> ImageURI = new();
    string loadImg = "";
    public static string isSquareError = "";
    private async Task LoadFiles(InputFileChangeEventArgs e)
    {
        StatusMessage = "";
        isSquareError = string.Empty;
        using var content = new MultipartFormDataContent();
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
                ImageURI.Clear();
            }
            foreach (var file in files)
            {
                var stream = file.OpenReadStream(1024 * 1024 * 30);
                using MemoryStream ms = new();
                await stream.CopyToAsync(ms);
                ImageFormat format = GetImageFormat(ms);
                var isSquare = IsSquareImage(ms, out isSquareError);

                if (format == ImageFormat.Unknown)
                {
                    if (!isLoadImageButtonClicked)
                        continue;
                    else
                        return;
                }

                var imgUrl = $"data:image/{format.ToString().ToLower()};base64,{Convert.ToBase64String(ms.ToArray())}";

                if (isLoadImageButtonClicked)
                {
                    loadImg = imgUrl;
                    await ImageEditorRef.OpenAsync(loadImg);
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
                stream.Close();
                await stream.DisposeAsync();
            }
        }
        catch (Exception)
        {
            // ignored
        }
        StateHasChanged();
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
        // Create a byte array to store the first few bytes of the image data
        byte[] header = new byte[8];

        // Copy the first few bytes from the MemoryStream to the byte array
        ms.Position = 0;
        _ = ms.Read(header, 0, header.Length);
        ms.Position = 0;

        // Check for JPEG format
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return ImageFormat.Jpeg;
        }

        // Check for PNG format
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
        {
            return ImageFormat.Png;
        }

        // If the format is not recognized, return Unknown
        return ImageFormat.Unknown;
    }

    string value = "";
    private void GetFilterList()
    {
        if (_contacts != null)
        {
            RemoveContactModelItems();
            foreach (var contactInfoShort in _contacts)
            {
                if ((contactInfoShort.ExternalSourceId is not null && contactInfoShort.ExternalSourceId.Equals(value))
                    || contactInfoShort.FirstName.ToLower().Contains(value.ToLower()) 
                    || contactInfoShort.LastName.ToLower().Contains(value.ToLower()))
                {
                    _contactModels.Add(new ContactInfoModel(contactInfoShort, ClasStarsServices));
                }
            }
            StateHasChanged();
        }
    }

    private void RemoveContactModelItems()
    {
        for (int a = _contactModels.Count - 1; a >= 0; a--)
        {
            _contactModels.RemoveAt(a);
        }
    }

    //Drag & Drop to Student
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
    private void DragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e)
    {
        //_isDragging = true;
    }
    private async void DragDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ContactInfoModel contact)
    {
        // _isDragging = true;
        try
        {
            ContactInfoModel cim = _contactModels.Find(c => c.ContactID == contact.ContactID);
            Regex regex = new Regex(@"^[\w/\:.-]+;base64,");
            DraggedImageSrc = regex.Replace(DraggedImageSrc, string.Empty);

            byte[] bytesData = Convert.FromBase64String(DraggedImageSrc);
            MemoryStream memoryStream = new MemoryStream(bytesData);
            bool? IsSquare = IsSquareImage(memoryStream, out isSquareError);
            if (IsSquare == true)
            {
                cim.ContactPicture = bytesData;

                await ClasStarsServices.SaveContactPicture(contact.ContactID, bytesData);
                StatusMessage = "Picture successfully updated.";
            }
            else
            {
                StatusMessage = "Failed! Picture is not square.";
            }
        }
        catch (Exception)
        {
            StatusMessage = "Something went wrong.";
        }
        StateHasChanged();
    }

    // Close the Editor Popup Window
    private async void CloseModelPopup()
    {
        isLoadImageButtonClicked = false;
        isDeleteClicked = false;
        cameraVisiblility = "cameraSzeHidden";
        await JSRuntime.InvokeVoidAsync("closeModal");
    }

    private async Task PerformCamera()
    {
        camera = "disableImg";
        try
        {
            await JSRuntime.InvokeVoidAsync("ShowCamera", 1);
        }
        catch (Exception)
        {
            // ignored
        }

        await Task.Delay(2000);
        if (cameraVisiblility == "cameraSzeHidden")
        {
            cameraVisiblility = "cameraSzeVisible";
        }
        else
        {
            cameraVisiblility = "cameraSzeHidden";
        }

        camera = "enablerImg";

        cropClass = "enablerImg";
        rotateClass = "disableImg";
        acceptClass = "disableImg";
        undoClass = "disableImg";
        saveClass = "disableImg";

        isLoadImageButtonClicked = false;
        isDeleteClicked = false;
    }

    private async Task PerformLoad()
    {
        cameraVisiblility = "cameraSzeHidden";

        await JSRuntime.InvokeVoidAsync("ShowCamera", 0);

        await JSRuntime.InvokeVoidAsync("triggerClick", fileInputSingle);

        isDeleteClicked = false;
        isLoadImageButtonClicked = true;

        cropClass = "enablerImg";
        rotateClass = "disableImg";
        acceptClass = "disableImg";
        undoClass = "disableImg";
        saveClass = "disableImg";
    }

    private async Task PerformDelete()
    {
        cameraVisiblility = "cameraSzeHidden";
        await JSRuntime.InvokeVoidAsync("ShowCamera", 0);
        string BlankImageFilePath = wwwRootPath + "\\Images\\EditorBlank1.png";

        byte[] imageBytes = File.ReadAllBytes(BlankImageFilePath);
        string base64String = $"data:image/png;base64,{Convert.ToBase64String(imageBytes.ToArray())}";

        isLoadImageButtonClicked = false;
        isDeleteClicked = true;

        await ImageEditorRef.OpenAsync(base64String);

        cropClass = "disableImg";
        rotateClass = "disableImg";
        acceptClass = "disableImg";
        undoClass = "disableImg";
        saveClass = "enablerImg";
    }

    private async Task PerformCrop()
    {
        isDeleteClicked = false;
        if (ImageEditorRef != null)
        {
            await ImageEditorRef.SelectAsync("Square", -1, -1, -1, -1);
        }
        acceptClass = "enablerImg";
        rotateClass = "enablerImg";
        undoClass = "disableImg";
        saveClass = "disableImg";

        StateHasChanged();
    }

    private async Task PerformRotate()
    {
        isDeleteClicked = false;
        if (ImageEditorRef != null)
        {
            await ImageEditorRef.RotateAsync(90);
        }

        cropClass = "enablerImg";
        acceptClass = "enablerImg";
        saveClass = "disableImg";
        undoClass = "disableImg";
    }

    private async Task PerformUndo()
    {
        isDeleteClicked = false;
        if (ImageEditorRef != null)
        {
            await ImageEditorRef.UndoAsync();
        }
        cropClass = "enablerImg";
        rotateClass = "disableImg";
        acceptClass = "disableImg";
        undoClass = "disableImg";
        saveClass = "disableImg";
    }

    private async Task PerformAccept()
    {
        isDeleteClicked = false;
        if (ImageEditorRef != null)
        {
            await ImageEditorRef.CropAsync();
        }

        cropClass = "enablerImg";
        acceptClass = "disableImg";
        rotateClass = "disableImg";
        saveClass = "enablerImg";
        undoClass = "enablerImg";
    }

    private async Task SaveEditedImage()
    {
        try
        {
            byte[] editedImageData = await ImageEditorRef.GetImageDataAsync();
            if (isDeleteClicked && selectedContactId > 0)
            {
                await ClasStarsServices.RemoveContactPicture(selectedContactId);
                isDeleteClicked = false;
                foreach (ContactInfoModel cimodel in _contactModels)
                {
                    if (cimodel.ContactID == selectedContactId)
                    {
                        cimodel.ContactPicture = null;
                        selectedContactId = 0;

                        StatusMessage = "Picture successfully deleted";
                        await JSRuntime.InvokeVoidAsync("closeModal");
                    }
                }
            }
            else if (selectedContactId > 0)
            {
                foreach (ContactInfoModel cimodel in _contactModels)
                {
                    if (cimodel.ContactID == selectedContactId)
                    {
                        MemoryStream memoryStream = new MemoryStream(editedImageData);
                        bool? IsSquare = IsSquareImage(memoryStream, out isSquareError);
                        if (IsSquare == true)
                        {
                            await ClasStarsServices.SaveContactPicture(cimodel.ContactID, editedImageData);
                            cimodel.ContactPicture = editedImageData;
                            selectedContactId = 0;

                            StatusMessage = "Picture successfully updated.";
                            await JSRuntime.InvokeVoidAsync("closeModal");
                        }
                        else
                        {
                            await JSRuntime.InvokeVoidAsync("alert", "Failed! Picture is not square.");
                        }
                    }
                }
            }
            else
            {
                try
                {
                    string filePath = wwwRootPath + "\\Images\\Edited\\Edited.png";

                    File.WriteAllBytes(filePath, editedImageData);
                    foreach (ImageDetail img in ImageURI)
                    {
                        if (img.ImgId == selectedGuid)
                        {
                            var fileData = await File.ReadAllBytesAsync(filePath);
                            MemoryStream memoryStream = new(fileData);
                            var IsSquare = IsSquareImage(memoryStream, out isSquareError);
                            if (IsSquare == true)
                            {
                                img.ImageUrl = $"data:image/png;base64,{Convert.ToBase64String(fileData.ToArray())}";
                                img.IsSqu = true;

                                StatusMessage = "Picture successfully edited.";
                                await JSRuntime.InvokeVoidAsync("closeModal");
                            }
                            else
                            {
                                bool confirmSave = await JSRuntime.InvokeAsync<bool>("confirm", "Image is not square yet. \nClick OK to save the image else click Cancel to continue editing!");
                                if (confirmSave)
                                {
                                    img.ImageUrl = $"data:image/png;base64,{Convert.ToBase64String(fileData.ToArray())}";
                                    img.IsSqu = IsSquare;

                                    StatusMessage = "Picture successfully edited.";
                                    await JSRuntime.InvokeVoidAsync("closeModal");
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
            cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            saveClass = "disableImg";
            undoClass = "disableImg";
            isLoadImageButtonClicked = false;
        }
        catch (Exception rrty)
        {
            await JSRuntime.InvokeVoidAsync("alert", "Failed! Something went wrong.\n" + rrty.Message + "");
        }
        StateHasChanged();
    }

    private void FilterList(string studentname)
    {
        StatusMessage = "";
        foreach (ImageDetail detail in ImageURI)
        {
            if (detail.ImageName.Contains(studentname))
            {
                detail.IsVis = true;
            }
            else
            {
                detail.IsVis = false;
            }
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

    private async Task LoadImage(ImageDetail imgg)
    {
        StatusMessage = "";
        IsContact = false;
        selectedContactId = 0;
        FName = Path.GetFileNameWithoutExtension(imgg.ImageName);
        await JSRuntime.InvokeVoidAsync("openModal");
        //if (ImageEditorRef == null)
        //{
        //    ImageEditorRef = new();
        //}
        selectedGuid = imgg.ImgId;
        try
        {
            await ImageEditorRef.OpenAsync(imgg.ImageUrl);
        }
        catch (Exception)
        {
            // ignored
        }

        cropClass = "enablerImg";
        rotateClass = "disableImg";
        acceptClass = "disableImg";
        undoClass = "disableImg";
        saveClass = "disableImg";

        StateHasChanged();
    }

    private async Task LoadContact(ContactInfoModel contact)
    {
        if (HomePage.IsAdmin)
        {
            StatusMessage = "";

            string BlankImageFilePath = wwwRootPath + "\\Images\\EditorBlank1.png";
            isLoadImageButtonClicked = false;
            IsContact = true;
            FName = contact.FirstName + " " + contact.LastName;
            await JSRuntime.InvokeVoidAsync("openModal");

            byte[] imageBytes;
            if (contact.ContactPicture != null)
                imageBytes = contact.ContactPicture;
            else
                imageBytes = await File.ReadAllBytesAsync(BlankImageFilePath);

            using (var stream = new MemoryStream(imageBytes))
            {
                var format = GetImageFormat(stream);
                var contactImage = $"data:image/{format.ToString().ToLower()};base64,{Convert.ToBase64String(imageBytes)}";
                selectedContactId = contact.ContactID;
                await ImageEditorRef.OpenAsync(contactImage);
            }

            cropClass = "enablerImg";
            rotateClass = "disableImg";
            acceptClass = "disableImg";
            undoClass = "disableImg";
            saveClass = "disableImg";
            StateHasChanged();
        }
    }

    public async void Capture()
    {
        try
        {
            string capturedImg = await JSRuntime.InvokeAsync<string>("take_snapshot");
            if (capturedImg == "")
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
                await ImageEditorRef.OpenAsync(capturedImg);
            }
        }
        catch (Exception)
        {
            // Handle exception
        }
        StateHasChanged();
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
