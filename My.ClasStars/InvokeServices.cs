using LogonServiceRequestTypes;
using LogonServiceRequestTypes.Enums;
using LogonServiceRequestTypes.Exceptions;
using Microsoft.Extensions.Options;
using My.ClasStars.Configuration;
using Newtonsoft.Json;
using Serilog;
using SharedTypes;
using SharedTypes.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace My.ClasStars;

public class InvokeServices : IInvokeServices
{
    public event EventHandler<EventArgs> ReloginRequested;

    private TaskCompletionSource<object> _reLoginTaskCompletionSource;
    private HttpClient _client;
    private readonly Dictionary<string, string> _defaultHeaders = new();
    private string _mobileServiceUserToken = string.Empty;

    private readonly JsonSerializerSettings _jsonSerializerSettings = new() { DateTimeZoneHandling = DateTimeZoneHandling.Unspecified };

    private string _mobileServiceUserId = string.Empty;
    private string _platform = string.Empty;
    private string _idiom = string.Empty;
    private string _applicationName;

    private Version _version;
    //Class
    public string MobileAuthServiceAddress { get; }
    public string ServiceAddress { get; }

    public InvokeServices(IOptions<ServiceEndpointOptions> options)
    {
        var endpointOptions = options.Value;
        ServiceAddress = endpointOptions.ServiceAddress;
        MobileAuthServiceAddress = endpointOptions.MobileAuthServiceAddress;
    }

    public void SetApplicationInfo(Version version, string applicationName, string platform = "", string idiom = "")
    {
        _version = version;
        _platform = platform;
        _idiom = idiom;
        _applicationName = applicationName;
        _client = null;
    }

    private HttpClient GetClientForToken(string token)
    {
        var client = new HttpClient()
        {
            BaseAddress = new Uri(ServiceAddress)
        };
        client.DefaultRequestHeaders.Add(ServiceHeaderKeys.AuthSecretKey, token);
        return client;
    }

    private void CreateNewClient()
    {
        var serviceAddress = ServiceAddress;
        if (!serviceAddress.EndsWith(@"/"))
            serviceAddress += @"/";
        _client = new HttpClient()
        {
            BaseAddress = new Uri(serviceAddress)
        };
        SetClientUserInfo();
        foreach (var keyValuePair in _defaultHeaders)
        {
            _client.DefaultRequestHeaders.Add(keyValuePair.Key, keyValuePair.Value);
        }
        if (!string.IsNullOrEmpty(_mobileServiceUserToken))
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _mobileServiceUserToken);
    }

    public async Task<TO> InvokePostAsync<TI, TO>(ServiceEndpoint endpoint, ServiceAction action, TI body)
    {
        return await InvokePostAsync<TI, TO>(ConvertToEndpoint(endpoint, action), body);
    }

    public async Task<TO> InvokePostAsync<TI, TO>(string endpoint, TI body)
    {
        var json = JsonConvert.SerializeObject(body);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        return await InvokeApiAsync<TO>(endpoint, HttpMethod.Post, httpContent: httpContent);
    }

    private async Task<T> InvokeApiAsync<T>(string endpoint, HttpMethod method, IDictionary<string, string> parameters = null, HttpContent httpContent = null)
    {
        if (_client == null)
            CreateNewClient();
        if (_client == null)
            throw new ArgumentNullException(nameof(_client));

        var retryCount = 0;
        while (true)
        {
            try
            {
                var httpRequest = new HttpRequestMessage();
                SetHeaderPerCallData(httpRequest); retryCount++;
                Log.Error($"Endpoint: {endpoint}"); //for logging
                var result = await _client.InvokeApiAsync(endpoint, method, httpRequest, parameters, httpContent);
                Log.Error($"result statuscode: {result.StatusCode}"); //for logging
                if (result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsStringAsync();
                    var retData = JsonConvert.DeserializeObject<T>(content, _jsonSerializerSettings);
                    if (retData != null) return retData;
                }
                else
                {
                    var msg = await result.Content.ReadAsStringAsync();
                    var ex = new HttpResponseException(result.StatusCode, result.ReasonPhrase, msg);
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                var remoteEx = new RemoteRequestException(ex);
                if (remoteEx.ShouldRetry && retryCount < 2)
                {
                    if (remoteEx.Unauthorized)
                    {
                        _reLoginTaskCompletionSource = new TaskCompletionSource<object>();
                        ReloginRequested?.Invoke(this, EventArgs.Empty);
                        _reLoginTaskCompletionSource.Task.Wait(TimeSpan.FromSeconds(5));
                    }
                    CreateNewClient();
                    continue;
                }
                throw remoteEx;
            }
        }
    }

    public async Task<T> InvokeGetAsync<T>(ServiceEndpoint endpoint, IDictionary<string, string> parameters = null, HttpContent httpContent = null)
    {
        return await InvokeApiAsync<T>(endpoint.ToString(), HttpMethod.Get, parameters, httpContent);
    }

    public async Task<string> GetToken(string secret)
    {
        Log.Error($"Secret:{secret}"); // for logging
        var client = GetClientForToken(secret);
        var httpRequest = new HttpRequestMessage();
        //SetHeaderPerCallData(httpRequest);
        Log.Error($"Endpoint: api/MobileAuth/token"); //for logging
        var response = await client.InvokeApiAsync("api/MobileAuth/token", HttpMethod.Get, httpRequest);
        if (response.IsSuccessStatusCode == false)
            throw (new Exception("Failed to start"));

        Log.Error($"Response code:{response.StatusCode}"); // for logging
        var json = await response.Content.ReadAsStringAsync();
        JsonDocument jsonDocument = JsonDocument.Parse(json);

        // get the value of the "name" property
        string access_token = jsonDocument.RootElement.GetProperty("access_token").GetString();
        Log.Error($"access_token:{access_token}"); // for logging
        _mobileServiceUserToken = access_token;
        Log.Error($"_mobileServiceUserToken:{_mobileServiceUserToken}"); // for logging
        return access_token;
    }

    public async Task<T> InvokeGetAsync<T>(ServiceEndpoint endpoint, ServiceAction action, IDictionary<string, string> parameters = null, HttpContent httpContent = null)
    {
        return await InvokeApiAsync<T>(ConvertToEndpoint(endpoint, action), HttpMethod.Get, parameters, httpContent);
    }

    public void RegisterLoggedInUser(LoginResultUser loggedInUser, string authToken = null)
    {
        if (loggedInUser != null)
        {
            _defaultHeaders[ServiceHeaderKeys.DBNameKey] = loggedInUser.DBName;
            _defaultHeaders[ServiceHeaderKeys.DBServerKey] = loggedInUser.DBServer;
            _defaultHeaders[ServiceHeaderKeys.TimeZoneKey] = loggedInUser.SchoolTimeZone;
            _defaultHeaders[ServiceHeaderKeys.CultureKey] = CultureInfo.CurrentCulture.Name;
            _defaultHeaders[ServiceHeaderKeys.UserIdKey] = loggedInUser.UserId;
            _defaultHeaders[ServiceHeaderKeys.EmpIDKey] = loggedInUser.LoggedInEmployee?.EmployeeID.ToString() ?? string.Empty;

            SetOrganizationInfo(loggedInUser.Organization);

            if (!string.IsNullOrEmpty(authToken))
                SetMobileServiceUserInfo(loggedInUser.UserId, authToken);
        }
    }

    private void SetOrganizationInfo(RegisteredOrganizationInfo organization)
    {
        if (organization == null) return;

        _defaultHeaders[ServiceHeaderKeys.OrganizationCodeKey] = organization.OrganizationCode;
        _defaultHeaders[ServiceHeaderKeys.OrganizationIDKey] = (organization.ID).ToString();
        if (!string.IsNullOrEmpty(organization.DBName) && !string.IsNullOrEmpty(organization.DBServer))
        {
            _defaultHeaders[ServiceHeaderKeys.DBNameKey] = organization.DBName;
            _defaultHeaders[ServiceHeaderKeys.DBServerKey] = organization.DBServer;
        }

        if (organization.IsSchool)
        {
            SetSchoolHeaderInfo(organization);
        }
    }

    public void SetSchoolHeaderInfo(RegisteredOrganizationInfo organization)
    {
        _defaultHeaders[ServiceHeaderKeys.SchoolCodeKey] = organization?.OrganizationCode;
        _defaultHeaders[ServiceHeaderKeys.SchoolIDKey] = organization == null ? "0" : (organization.ID).ToString();
    }

    private void SetMobileServiceUserInfo(string userID, string token)
    {
        _mobileServiceUserId = userID;
        _mobileServiceUserToken = token;
        _client = null;
    }

    private void SetHeaderPerCallData(HttpRequestMessage httpRequestMessage)
    {
        httpRequestMessage.Headers.AddOrReplace(ServiceHeaderKeys.InvokeDateTime, DateTime.Now.SetKindToUnspecified().ToString("O"));
        httpRequestMessage.Headers.AddOrReplace(ServiceHeaderKeys.TimeZoneKey, TimeZoneInfo.Local.Id);
    }

    private void SetClientUserInfo()
    {
        if (_client == null) return;
        _defaultHeaders[ServiceHeaderKeys.TimeZoneKey] = TimeZoneInfo.Local.Id;
        _defaultHeaders[ServiceHeaderKeys.AcceptLanguage] = CultureInfo.CurrentCulture.Name;


        if (!string.IsNullOrEmpty(_mobileServiceUserToken) && !string.IsNullOrEmpty(_mobileServiceUserId))
        {
            _defaultHeaders[ServiceHeaderKeys.UserIdKey] = _mobileServiceUserId;
        }


        if (_version != null)
        {
            var buildInfo = $"{_version.Major:D2}.{_version.Minor:D2}.{_version.Build:D2}";
            _defaultHeaders[ServiceHeaderKeys.CallerVersion] = buildInfo;
        }

        if (!string.IsNullOrEmpty(_platform))
            _defaultHeaders[ServiceHeaderKeys.CallerPlatform] = _platform;

        if (!string.IsNullOrEmpty(_idiom))
            _defaultHeaders[ServiceHeaderKeys.CallerIdiom] = _idiom;

        if (!string.IsNullOrEmpty(_applicationName))
            _defaultHeaders[ServiceHeaderKeys.AppNameKey] = _applicationName;
    }

    private string ConvertToEndpoint(ServiceEndpoint endpoint, ServiceAction action)
    {
        return $"api/{endpoint}/{action}";
    }

    public void Logout()
    {
        _mobileServiceUserId = string.Empty;
        _mobileServiceUserToken = string.Empty;
        _client = null;
    }
}