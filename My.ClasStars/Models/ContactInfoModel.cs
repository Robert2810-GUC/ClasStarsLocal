using SharedTypes;
using System.Threading.Tasks;

namespace My.ClasStars.Models;

public class ContactInfoModel : ExternalProviderIdentifier
{
    private readonly IClasStarsServices _classServices;

    public ContactInfoModel()
    {

    }

    public ContactInfoModel(ContactInfoShort contactInfo, IClasStarsServices invokeServices)
    {
        _classServices = invokeServices;
        ContactID = contactInfo.ContactID;
        LastName = contactInfo.LastName.Trim();
        FirstName = contactInfo.FirstName.Trim();
        ContactPicture = contactInfo.ContactImage;
        FileAs = contactInfo.FileAs;
        PersonID = contactInfo.PersonID;
    }
    public int ContactID { get; set; }


    public string FileAs { get; set; }
    public string? PersonID { get; set; }

    private string _lastName;
    public string LastName
    {
        get => _lastName;
        set
        {
            SetField(ref _lastName, value);
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private byte[] _contactPicture;
    public byte[] ContactPicture
    {
        get => _contactPicture;
        set => SetField(ref _contactPicture, value);
        //if (value == null)
        //    ContactPictureImageSource = null;
        //else
        //    ContactPictureImageSource = _imageSourceConverter.ConvertFrom(value);
        //OnPropertyChanged(nameof(ContactPictureImageSource));
    }

    //private ImageSource _contactPictureImageSource;
    //public ImageSource ContactPictureImageSource
    //{
    //    get =>
    //        //if (_contactPictureImageSource == null)
    //        //{
    //        //    _contactPictureImageSource =
    //        //        ImageSource.FromStream(() => new MemoryStream(Resources.personunavailableicon));
    //        //}
    //        _contactPictureImageSource;
    //    private set => SetProperty(ref _contactPictureImageSource, value);
    //}


    private string _firstName;
    public string FirstName
    {
        get => _firstName;
        set
        {
            SetField(ref _firstName, value);
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public async Task SavePicture()
    {
        try
        {
            _ = await _classServices.SaveContactPicture(ContactID, ContactPicture);
        }
        catch (System.Exception)
        {
            //ErrorMessage = ex.Message;
        }
    }

    public async void RemovePicture()
    {
        try
        {
            _ = await _classServices.RemoveContactPicture(ContactID);
            ContactPicture = null;
        }
        catch (System.Exception)
        {
           // ErrorMessage = ex.Message;
        }
    }

    public string DisplayName => LastName + ", " + FirstName;

    public string Initials => (string.IsNullOrEmpty(FirstName) ? "?" : FirstName[0]) +
        (string.IsNullOrEmpty(LastName) ? "?" : LastName[0].ToString());
}
