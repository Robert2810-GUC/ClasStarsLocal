using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using My.ClasStars.Components;
using My.ClasStars.Helpers;
using My.ClasStars.Models;
using My.ClasStars.Resources;
using Newtonsoft.Json.Linq;
using SharedTypes;
using Syncfusion.Blazor.ImageEditor;
using Syncfusion.Blazor.RichTextEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

#pragma warning disable CA1416

namespace My.ClasStars.Pages;

public partial class Students
{

    private bool isProcessingFiles = false;

    private ImageEditorModal editorModal;
    private bool isEditorVisible = false;

    // Right Panel
    string ShowHide = "HideRightPnl";

    // Camera Button and Div (kept because some page-level code might reference them)
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
    string wwwRootPath = "";
    int selectedContactId = 0;
    string StatusMessage = "";
    Guid selectedGuid;
    string FName = "";
    bool IsContact = false;
    /////////////////////////////////
    private List<ContactInfoShort> _contacts;
    public List<ContactInfoModel> _contactModels;
    public string refreshPath = "";
    protected async override Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        wwwRootPath = WebHostEnvironment.WebRootPath;
        SchoolServices.Initialized = false;
    _contact_models_init:
        _contactModels = new();
        if (AppInfo.UserInfo != null)
        {
            _contacts = await ClasStarsServices.GetStudentList(AppInfo.UserInfo.Organization.ID);
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

    SfImageEditor ImageEditorRef; // note: page no longer holds the editor ref, editor lives in component
    [Parameter]
    public List<ImageDetail> ImageURI { get; set; } = new();
    string loadImg = "";
    public static string isSquareError = "";


    // LoadFiles updated to call component when "open in editor" flow is used
    private async Task LoadFiles(InputFileChangeEventArgs e)
    {
        StatusMessage = "";
        isSquareError = string.Empty;
        isProcessingFiles = true;             // show loader in parent
        StateHasChanged();

        using var content = new MultipartFormDataContent();
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
                // when starting to add multiple images (bulk import)
                if (!isLoadImageButtonClicked) // keep existing modal-open single image unaffected
                {
                    ImageURI.Clear();
                }
            }

            foreach (var file in files)
            {
                var stream = file.OpenReadStream(1024 * 1024 * 30);
                using MemoryStream ms = new();
                await stream.CopyToAsync(ms);

                var format = ImageHelpers.GetImageFormatFromStream(ms); // see helper below
                var isSquare = ImageHelpers.IsSquareImage(ms, out isSquareError);

                var imgUrl = $"data:image/{format.ToString().ToLower()};base64,{Convert.ToBase64String(ms.ToArray())}";

                if (isLoadImageButtonClicked)
                {
                    loadImg = imgUrl;
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
                stream.Close();
                await stream.DisposeAsync();
            }
            isProcessingFiles = false;

            // notify modal/parent if needed (parent already updates itself)
            StateHasChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = "Something went wrong: " + ex.Message;
        }
        finally
        {
            isProcessingFiles = false;         // hide loader
            StateHasChanged();
        }
    }

    // Keep the helper functions and other logic unchanged (IsSquareImage, GetImageFormat, etc.)

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
        // force UI refresh
        StatusMessage = "Picture successfully updated.";
        StateHasChanged();

        return Task.CompletedTask;
    }


    private void DragOver(Microsoft.AspNetCore.Components.Web.DragEventArgs e) { }
    private async void DragDrop(Microsoft.AspNetCore.Components.Web.DragEventArgs _, ContactInfoModel contact)
    {
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

    private async Task LoadImage(ImageDetail imgg)
    {
        // replaced prior openModal+OpenAsync with component call
        await editorModal.OpenForImage(imgg);
    }

    private async Task LoadContact(ContactInfoModel contact)
    {
        // replaced prior openModal + ImageEditorRef.OpenAsync with component call
        await editorModal.OpenForContact(contact);
    }

    // The rest of your helper methods (FilterList, All, etc.) remain exactly the same as before
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

    private Task OnImageUriChanged(List<ImageDetail> newList)
    {
        // Keep reference & force re-render
        ImageURI = newList;
        StateHasChanged();
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
