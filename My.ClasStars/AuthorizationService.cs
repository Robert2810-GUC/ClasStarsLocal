using LogonServiceRequestTypes.Enums;
using LogonServiceRequestTypes.Exceptions;
using Microsoft.Extensions.Options;
using My.ClasStars.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedTypes;
using SharedTypes.ServiceRequestTypes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace My.ClasStars;

public class AuthorizationService : IAuthorizationService
{
    private readonly AuthOptions _authOptions;
    private readonly IInvokeServices _svc;

    public AuthorizationService(IInvokeServices invokeServices, IOptions<AuthOptions> authOptions)
    {
        _svc = invokeServices;
        _authOptions = authOptions.Value;
    }

    public async Task<string> GetToken()
    {
        if (string.IsNullOrWhiteSpace(_authOptions.ClasstarsAuthSecret))
        {
            throw new InvalidOperationException("Classtars authorization secret is not configured.");
        }

        return await _svc.GetToken(_authOptions.ClasstarsAuthSecret);
    }

    public async Task<LoggedInUserInfo> CheckUserAuthorized(string email, string provider, bool activeUsersOnly,
        int organizationId = 0, bool autoCreateUser = false, bool isEnterpriseUser = false, bool failOnEnterpriseConflict = false)
    {
        if (!Guid.TryParse(_authOptions.ClasstarsAuthSecret, out var authorizationSecret))
        {
            throw new InvalidOperationException("Classtars authorization secret is not configured correctly.");
        }

        var userInfo = new CheckUserAuthorizedRequest()
        {
            SecurityKey = email,
            AuthorizationSecret = authorizationSecret,
            Provider = provider,
            ActiveUsersOnly = activeUsersOnly,
            IsEnterpriseAuthorized = isEnterpriseUser,
            IsSchoolListSupported = true,
            OrganizationID = organizationId,
            ReturnOrganizationLogo = true,
            AutoCreateNewUser = autoCreateUser,
            FailOnEnterpriseUserConflict = failOnEnterpriseConflict
        };
        var retData = await _svc.InvokePostAsync<CheckUserAuthorizedRequest, LoggedInUserInfo>(
            ServiceEndpoint.SingleUserAuthorization, ServiceAction.CheckUserAuthorization, userInfo);

        return retData;
    }

    public async Task<LoginResultUser> LoginAsync(UserAuth user)
    {
        LoginResultUser loggedInUser;
        try
        {

            var retUser =
                await _svc.InvokePostAsync<UserAuth, JObject>("/.auth/login/custom", user);

            var authToken = retUser.Value<string>("authenticationToken");
            loggedInUser = JsonConvert.DeserializeObject<LoginResultUser>(retUser["user"]?.ToString() ?? string.Empty);

            var staticTableVersions = JsonConvert.DeserializeObject<List<AppConstantsTableVersion>>(retUser["staticTableVersions"]?.ToString() ?? string.Empty);
            var customEventInfo = JsonConvert.DeserializeObject<CustomEventInfo>(retUser["customEventInfo"]?.ToString() ?? string.Empty);

            if (loggedInUser == null || !loggedInUser.IsValid())
                return new LoginResultUser(user, loggedInUser?.IsNewUser ?? false) { LoginResult = "Login Failed" };

            loggedInUser.StaticTableVersions = staticTableVersions;
            loggedInUser.CustomEventInfo = customEventInfo;

            if (string.IsNullOrEmpty(loggedInUser.SchoolTimeZone))
                loggedInUser.SchoolTimeZone = TimeZoneInfo.Local.StandardName;


            if (loggedInUser.LoggedInEmployee == null)
            {
                var empInfo = await _svc.InvokeGetAsync<EmployeeInfoShort>(ServiceEndpoint.EmployeeInfo);
                loggedInUser.LoggedInEmployee = empInfo;
            }

            _svc.RegisterLoggedInUser(loggedInUser, authToken);
            AppInfo.UserInfo = loggedInUser;

        }
        catch (Exception ex)
        {

            loggedInUser = new LoginResultUser(user, false) { LoginResult = ex.Message };
            if (ex is RemoteRequestException)
            {
                if (string.IsNullOrWhiteSpace(loggedInUser.LoginResult))
                    loggedInUser.LoginResult = "Login Failed";
            }
        }

        return loggedInUser;

    }
}
