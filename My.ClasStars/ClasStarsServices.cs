using SharedTypes;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogonServiceRequestTypes.Enums;
using SharedTypes.ServiceRequestTypes;

namespace My.ClasStars;

public interface IClasStarsServices
{
    Task<List<EmployeeInfoDetailed>> GetTeachersInformation();
    Task<List<ContactInfoShort>> GetStudentList(int organizationId, int pageSize = 0, ContactInfoShort startingContact = null);
    Task<GetUsageOverviewResponse> GetSummary(GetUsageOverviewRequest request);
    Task<bool> RemoveContactPicture(int contactID);
    Task<bool> SaveContactPicture(int contactID, byte[] contactPicture);
}

public class ClasStarsServices : IClasStarsServices
{
    private readonly IInvokeServices _invokeServices;

    public ClasStarsServices(IInvokeServices invokeServices)
    {
        _invokeServices = invokeServices;
    }

    public async Task<List<EmployeeInfoDetailed>> GetTeachersInformation()
    {
        return await _invokeServices.InvokeGetAsync<List<EmployeeInfoDetailed>>(ServiceEndpoint.SchoolInfo,
            ServiceAction.GetTeachersInformation);
    }

    public async Task<List<ContactInfoShort>> GetStudentList(int organizationId, int pageSize = 0, ContactInfoShort startingContact = null)
    {
        var request = new GetContactListRequest
        {
            PageSize = pageSize,
            StartingAtLastName = startingContact?.LastName,
            OrganizationId = organizationId,
            IncludeStaff = false
        };

#if DEBUG
 
#endif
        var contacts = await _invokeServices.InvokePostAsync<GetContactListRequest, List<ContactInfoShort>>(
            ServiceEndpoint.ContactInfo, ServiceAction.GetContactList, request);

        return contacts;
    }

    public async Task<GetUsageOverviewResponse> GetSummary(GetUsageOverviewRequest request)
    {
        GetUsageOverviewResponse result;
        result = await _invokeServices.InvokePostAsync<GetUsageOverviewRequest, GetUsageOverviewResponse>(ServiceEndpoint.Reports, ServiceAction.GetUsageOverview, request);
        result.ActivityCounts ??= new();
        result.ActivityCounts.PositiveEvents ??= new();
        result.ActivityCounts.NegativeEvents ??= new();
        result.ActivityCounts.AcademicEvents ??= new();
        result.ActivityCounts.AcademicNegativeEvents ??= new();
        return result;
    }

   
    public async Task<bool> RemoveContactPicture(int contactID)
    {
        return await _invokeServices.InvokeGetAsync<bool>(ServiceEndpoint.ContactInfo, ServiceAction.RemoveContactPicture,
            new Dictionary<string, string>() { { "contactId", contactID.ToString() } });
    }

    public async Task<bool> SaveContactPicture(int contactID, byte[] contactPicture)
    {
        var savePictureRequest = new SaveContactPictureRequest
        {
            ContactID = contactID,
            ContactImage = contactPicture
        };

        var result = await _invokeServices.InvokePostAsync<SaveContactPictureRequest, byte[]>(ServiceEndpoint.ContactInfo, ServiceAction.SaveContactPicture, savePictureRequest);
        return result != null;
    }
}