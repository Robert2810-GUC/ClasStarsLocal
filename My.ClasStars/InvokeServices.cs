using LogonServiceRequestTypes;
using LogonServiceRequestTypes.Exceptions;
using LogonServiceRequestTypes.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
    private const int MaxRetryCount = 2;
    private const string MobileAuthClientName = "MobileAuthHttpClient";

    public event EventHandler<EventArgs> ReloginRequested;

    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InvokeServices> _logger;
    private readonly object _headerLock = new();
    private readonly Dictionary<string, string> _defaultHeaders = new();
    private string _mobileServiceUserToken = string.Empty;
    private string _mobileServiceUserId = string.Empty;
    private string _platform = string.Empty;
    private string _idiom = string.Empty;
    private string _applicationName = string.Empty;
    private Version _version;

    private readonly JsonSerializerSettings _jsonSerializerSettings = new()
    {
        DateTimeZoneHandling = DateTimeZoneHandling.Unspecified
    };

    //Class
    public string MobileAuthServiceAddress { get; }
    public string ServiceAddress { get; }

    public InvokeServices(
        HttpClient httpClient,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<InvokeServices> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ServiceAddress = configuration["ServiceAddress"] ?? throw new ArgumentNullException("ServiceAddress");
        MobileAuthServiceAddress = configuration["MobileAuthServiceAddress"] ?? ServiceAddress;
        _httpClient.BaseAddress = BuildServiceUri(ServiceAddress);
    }

    public void SetApplicationInfo(Version version, string applicationName, string platform = "", string idiom = "")
    {
        _version = version;
        _platform = platform;
        _idiom = idiom;
        _applicationName = applicationName;
        UpdateClientContextHeaders();
    }

    private Uri BuildServiceUri(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Service address cannot be empty", nameof(address));
        }

        return address.EndsWith("/") ? new Uri(address) : new Uri(address + "/");
    }

    public async Task<TO> InvokePostAsync<TI, TO>(ServiceEndpoint endpoint, ServiceAction action, TI body)
    {
        return await InvokePostAsync<TI, TO>(ConvertToEndpoint(endpoint, action), body);
    }

    public async Task<TO> InvokePostAsync<TI, TO>(string endpoint, TI body)
    {
        if (body == null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        var json = JsonConvert.SerializeObject(body);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        return await InvokeApiAsync<TO>(endpoint, HttpMethod.Post, httpContent: httpContent);
    }

    private async Task<T> InvokeApiAsync<T>(string endpoint, HttpMethod method, IDictionary<string, string> parameters = null, HttpContent httpContent = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
        }

        if (method == null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        var retryCount = 0;

        while (true)
        {
            try
            {
                using var httpRequest = new HttpRequestMessage();
                ApplyHeaders(httpRequest);
                SetHeaderPerCallData(httpRequest);
                retryCount++;

                _logger.LogInformation("Calling {Endpoint} with method {Method}", endpoint, method);
                using var result = await _httpClient.InvokeApiAsync(endpoint, method, httpRequest, parameters, httpContent);
                _logger.LogDebug("Received {StatusCode} from {Endpoint}", result.StatusCode, endpoint);

                if (result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsStringAsync();
                    var retData = JsonConvert.DeserializeObject<T>(content, _jsonSerializerSettings);
                    if (retData != null)
                    {
                        return retData;
                    }

                    _logger.LogWarning("Response content for {Endpoint} could not be deserialized to {Type}", endpoint, typeof(T));
                    throw new RemoteRequestException(new InvalidOperationException("Unable to deserialize response"));
                }

                var msg = await result.Content.ReadAsStringAsync();
                throw new HttpResponseException(result.StatusCode, result.ReasonPhrase, msg);
            }
            catch (Exception ex)
            {
                var remoteEx = new RemoteRequestException(ex);
                if (remoteEx.ShouldRetry && retryCount < MaxRetryCount)
                {
                    _logger.LogWarning(remoteEx, "Retrying {Endpoint} after transient error", endpoint);
                    if (remoteEx.Unauthorized)
                    {
                        await TriggerReloginAsync();
                    }

                    continue;
                }

                _logger.LogError(remoteEx, "Failed to execute request for {Endpoint}", endpoint);
                throw;
            }
        }
    }

    private async Task TriggerReloginAsync()
    {
        if (ReloginRequested == null)
        {
            return;
        }

        ReloginRequested.Invoke(this, EventArgs.Empty);
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    public async Task<T> InvokeGetAsync<T>(ServiceEndpoint endpoint, IDictionary<string, string> parameters = null, HttpContent httpContent = null)
    {
        return await InvokeApiAsync<T>(endpoint.ToString(), HttpMethod.Get, parameters, httpContent);
    }

    public async Task<string> GetToken(string secret)
    {
        var client = CreateClientForToken(secret);
        using var httpRequest = new HttpRequestMessage();

        _logger.LogInformation("Requesting token from {Endpoint}", "api/MobileAuth/token");
        using var response = await client.InvokeApiAsync("api/MobileAuth/token", HttpMethod.Get, httpRequest);
        if (response.IsSuccessStatusCode == false)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new RemoteRequestException(new Exception($"Failed to start token flow: {content}"));
        }

        var json = await response.Content.ReadAsStringAsync();
        using var jsonDocument = JsonDocument.Parse(json);
        if (!jsonDocument.RootElement.TryGetProperty("access_token", out var accessTokenElement))
        {
            throw new RemoteRequestException(new InvalidOperationException("Token response did not include an access token"));
        }

        var accessToken = accessTokenElement.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new RemoteRequestException(new InvalidOperationException("Access token is empty"));
        }

        _mobileServiceUserToken = accessToken;
        return accessToken;
    }

    public async Task<T> InvokeGetAsync<T>(ServiceEndpoint endpoint, ServiceAction action, IDictionary<string, string> parameters = null, HttpContent httpContent = null)
    {
        return await InvokeApiAsync<T>(ConvertToEndpoint(endpoint, action), HttpMethod.Get, parameters, httpContent);
    }

    public void RegisterLoggedInUser(LoginResultUser loggedInUser, string authToken = null)
    {
        if (loggedInUser == null)
        {
            return;
        }

        SetDefaultHeader(ServiceHeaderKeys.DBNameKey, loggedInUser.DBName);
        SetDefaultHeader(ServiceHeaderKeys.DBServerKey, loggedInUser.DBServer);
        SetDefaultHeader(ServiceHeaderKeys.TimeZoneKey, loggedInUser.SchoolTimeZone);
        SetDefaultHeader(ServiceHeaderKeys.CultureKey, CultureInfo.CurrentCulture.Name);
        SetDefaultHeader(ServiceHeaderKeys.UserIdKey, loggedInUser.UserId);
        SetDefaultHeader(ServiceHeaderKeys.EmpIDKey, loggedInUser.LoggedInEmployee?.EmployeeID.ToString() ?? string.Empty);

        SetOrganizationInfo(loggedInUser.Organization);

        if (!string.IsNullOrEmpty(authToken))
        {
            SetMobileServiceUserInfo(loggedInUser.UserId, authToken);
        }

        UpdateClientContextHeaders();
    }

    private void SetOrganizationInfo(RegisteredOrganizationInfo organization)
    {
        if (organization == null)
        {
            return;
        }

        SetDefaultHeader(ServiceHeaderKeys.OrganizationCodeKey, organization.OrganizationCode);
        SetDefaultHeader(ServiceHeaderKeys.OrganizationIDKey, organization.ID.ToString());
        if (!string.IsNullOrEmpty(organization.DBName) && !string.IsNullOrEmpty(organization.DBServer))
        {
            SetDefaultHeader(ServiceHeaderKeys.DBNameKey, organization.DBName);
            SetDefaultHeader(ServiceHeaderKeys.DBServerKey, organization.DBServer);
        }

        if (organization.IsSchool)
        {
            SetSchoolHeaderInfo(organization);
        }
    }

    public void SetSchoolHeaderInfo(RegisteredOrganizationInfo organization)
    {
        SetDefaultHeader(ServiceHeaderKeys.SchoolCodeKey, organization?.OrganizationCode);
        SetDefaultHeader(ServiceHeaderKeys.SchoolIDKey, organization == null ? "0" : organization.ID.ToString());
    }

    private void SetMobileServiceUserInfo(string userID, string token)
    {
        _mobileServiceUserId = userID;
        _mobileServiceUserToken = token;
    }

    private void SetHeaderPerCallData(HttpRequestMessage httpRequestMessage)
    {
        httpRequestMessage.Headers.AddOrReplace(ServiceHeaderKeys.InvokeDateTime, DateTime.Now.SetKindToUnspecified().ToString("O"));
        httpRequestMessage.Headers.AddOrReplace(ServiceHeaderKeys.TimeZoneKey, TimeZoneInfo.Local.Id);
    }

    private void UpdateClientContextHeaders()
    {
        SetDefaultHeader(ServiceHeaderKeys.TimeZoneKey, TimeZoneInfo.Local.Id);
        SetDefaultHeader(ServiceHeaderKeys.AcceptLanguage, CultureInfo.CurrentCulture.Name);

        if (!string.IsNullOrEmpty(_mobileServiceUserToken) && !string.IsNullOrEmpty(_mobileServiceUserId))
        {
            SetDefaultHeader(ServiceHeaderKeys.UserIdKey, _mobileServiceUserId);
        }

        if (_version != null)
        {
            var buildInfo = $"{_version.Major:D2}.{_version.Minor:D2}.{_version.Build:D2}";
            SetDefaultHeader(ServiceHeaderKeys.CallerVersion, buildInfo);
        }

        if (!string.IsNullOrEmpty(_platform))
        {
            SetDefaultHeader(ServiceHeaderKeys.CallerPlatform, _platform);
        }

        if (!string.IsNullOrEmpty(_idiom))
        {
            SetDefaultHeader(ServiceHeaderKeys.CallerIdiom, _idiom);
        }

        if (!string.IsNullOrEmpty(_applicationName))
        {
            SetDefaultHeader(ServiceHeaderKeys.AppNameKey, _applicationName);
        }
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        lock (_headerLock)
        {
            foreach (var keyValuePair in _defaultHeaders)
            {
                request.Headers.AddOrReplace(keyValuePair.Key, keyValuePair.Value);
            }
        }

        if (!string.IsNullOrEmpty(_mobileServiceUserToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _mobileServiceUserToken);
        }
    }

    private void SetDefaultHeader(string key, string value)
    {
        lock (_headerLock)
        {
            _defaultHeaders[key] = value ?? string.Empty;
        }
    }

    private HttpClient CreateClientForToken(string token)
    {
        var client = _httpClientFactory.CreateClient(MobileAuthClientName);
        client.BaseAddress = BuildServiceUri(MobileAuthServiceAddress);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add(ServiceHeaderKeys.AuthSecretKey, token);
        return client;
    }

    private string ConvertToEndpoint(ServiceEndpoint endpoint, ServiceAction action)
    {
        return $"api/{endpoint}/{action}";
    }

    public void Logout()
    {
        _mobileServiceUserId = string.Empty;
        _mobileServiceUserToken = string.Empty;
    }
}
