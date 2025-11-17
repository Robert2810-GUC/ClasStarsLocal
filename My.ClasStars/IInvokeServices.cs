using LogonServiceRequestTypes.Enums;
using SharedTypes;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace My.ClasStars;
public interface IInvokeServices
{
    event EventHandler<EventArgs> ReloginRequested;

    string MobileAuthServiceAddress { get; }
    string ServiceAddress { get;}
    Task<string> GetToken(string secret);
    Task<T> InvokeGetAsync<T>(ServiceEndpoint endpoint, IDictionary<string, string> parameters = null, HttpContent httpContent = null);
    Task<T> InvokeGetAsync<T>(ServiceEndpoint endpoint, ServiceAction action, IDictionary<string, string> parameters = null, HttpContent httpContent = null);
    Task<TO> InvokePostAsync<TI, TO>(ServiceEndpoint endpoint, ServiceAction action, TI body);
    Task<TO> InvokePostAsync<TI, TO>(string endpoint, TI body);
    void Logout();
    void RegisterLoggedInUser(LoginResultUser loggedInUser, string authToken = null);
    void SetApplicationInfo(Version version, string applicationName, string platform = "", string idiom = "");
    void SetSchoolHeaderInfo(RegisteredOrganizationInfo organization);
}