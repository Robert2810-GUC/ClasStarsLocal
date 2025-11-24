#define AUTOLOGIN
using LogonServiceRequestTypes.Enums;
using LogonServiceRequestTypes;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace My.ClasStars.Pages;

public partial class Login
{
    private readonly List<ExternalProviders> _providerButtons = new();
    private bool _isLoadingProviders = true;
    private string? _loadError;

    public IEnumerable<ExternalProviders> ProviderButtons => _providerButtons;
    public IEnumerable<ExternalProviders> PrimaryProviders => OrderedProviders.Take(3);
    public IEnumerable<ExternalProviders> OverflowProviders => OrderedProviders.Skip(3);

    public List<ExternalProviders> CookieProvidersList { get; private set; } = new();

    private IEnumerable<ExternalProviders> OrderedProviders => _providerButtons
        .OrderByDescending(p => p.LastLoginDate ?? DateTime.MinValue)
        .ThenBy(p => p.Name);

    protected override async Task OnInitializedAsync()
    {
        Logout();
        _isLoadingProviders = true;

        try
        {
            var authorizationSecret = Startup.Configuration["ClasstarsAuthSecret"];
            await InvokeServices.GetToken(authorizationSecret);
            SchoolServices.NavDisplay = false;

            var secureInfo = new AnonymousRequestSecureInfo();
            var providers = await InvokeServices.InvokePostAsync<AnonymousRequestSecureInfo, List<ExternalDataProvider>>(
                ServiceEndpoint.ExternalIntegration,
                ServiceAction.GetSupportedLoginProviders,
                secureInfo);

            if (providers == null || providers.Count == 0)
            {
                _loadError = "No sign-in providers are available right now.";
                return;
            }

            foreach (var provider in providers)
            {
                _providerButtons.Add(new ExternalProviders
                {
                    Name = provider.ToString(),
                    LastLoginDate = null,
                    ImageUrl = $"Images/{GetImageName(provider.ToString())}",
                    ExpiryDate = null
                });
            }

            await LoadProvidersListAsync();
        }
        catch (Exception ex)
        {
            _loadError = "Unable to load sign-in providers. Please try again.";
            Logger?.LogError(ex, "Error initializing login providers.");
        }
        finally
        {
            _isLoadingProviders = false;
        }
    }

    private string GetImageName(string providerName)
    {
        providerName = providerName.ToLower();
        return providerName switch
        {
            "google" => "googlelogo.jpg",
            "classtars" => "classtars.svg",
            "apple" => "apple.svg",
            "facebook" => "facebook.svg",
            "clever" => "CleverLOGO.jpg",
            "classlink" => "classlinklogo.jpg",
            "nycdoe" => "NycDoe.svg",
            _ => ""
        };
    }

    private async Task LoadProvidersListAsync()
    {
        var providersJson = await localStorage.GetItemAsync<string>("ProvidersList");
        if (string.IsNullOrWhiteSpace(providersJson))
        {
            return;
        }

        try
        {
            CookieProvidersList = JsonConvert.DeserializeObject<List<ExternalProviders>>(providersJson) ?? new List<ExternalProviders>();
            CookieProvidersList.RemoveAll(p => p.ExpiryDate < DateTime.Now);

            await GenerateNewLocalStorageAsync();

            if (CookieProvidersList.Count == 0)
            {
                return;
            }

            foreach (var button in _providerButtons)
            {
                var existing = CookieProvidersList.FirstOrDefault(p => p.Name == button.Name);
                if (existing != null)
                {
                    button.LastLoginDate = existing.LastLoginDate;
                }
            }
        }
        catch (JsonException ex)
        {
            Logger?.LogWarning(ex, "Providers list in local storage was invalid and has been cleared.");
            await localStorage.RemoveItemAsync("ProvidersList");
            CookieProvidersList.Clear();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error while loading providers from local storage.");
            await localStorage.RemoveItemAsync("ProvidersList");
            CookieProvidersList.Clear();
        }
    }

    private async Task GenerateNewLocalStorageAsync()
    {
        var providerList = JsonConvert.SerializeObject(CookieProvidersList);
        await localStorage.SetItemAsync("ProvidersList", providerList);
    }

    private async Task HandleLoginAsync(string providerName)
    {
        try
        {
            var providersListJson = await localStorage.GetItemAsync<string>("ProvidersList");
            if (!string.IsNullOrEmpty(providersListJson))
            {
                CookieProvidersList = JsonConvert.DeserializeObject<List<ExternalProviders>>(providersListJson) ?? new List<ExternalProviders>();
            }

            var provider = CookieProvidersList.FirstOrDefault(p => p.Name == providerName);

            if (provider != null)
            {
                provider.LastLoginDate = DateTime.Now;
                provider.ExpiryDate = DateTime.Now.AddYears(1);
            }
            else
            {
                CookieProvidersList.Add(new ExternalProviders
                {
                    Name = providerName,
                    LastLoginDate = DateTime.Now,
                    ImageUrl = null,
                    ExpiryDate = DateTime.Now.AddYears(1)
                });
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Unable to read providers list from local storage; resetting.");
            await localStorage.RemoveItemAsync("ProvidersList");
            CookieProvidersList = new List<ExternalProviders>
            {
                new ExternalProviders
                {
                    Name = providerName,
                    LastLoginDate = DateTime.Now,
                    ImageUrl = null,
                    ExpiryDate = DateTime.Now.AddYears(1)
                }
            };
        }

#if AUTOLOGIN && DEBUG


        SchoolServices.Email = "guc.dsforclassstars1@gmail.com";
        //var url = NavigationManager.ToAbsoluteUri("/homePage?access_token=ya29&expires=1716136044&email=pnqllc@gmail.com&provider=Google&full_name=Sol Fried&surname=Fried&given_name=Solomon");


        var givenName = "Fred";
        var surname = "Faiz";
        var orgId = "2375";

        var url = NavigationManager.ToAbsoluteUri($"/homePage?access_token=ya29&expires=1716136044&email={SchoolServices.Email}&provider=ClassLink&full_name={givenName} {surname}&surname={surname}&given_name={givenName}&orgId={orgId}");



        // var user = await new AuthorizationService(InvokeServices).CheckUserAuthorized(SchoolServices.Email, item, false);
        //if (user != null)
        //{
        SchoolServices.Initialized = true;
        SchoolServices.NavDisplay = true;

        NavigationManager.NavigateTo(url.ToString());
        return;
        //}

#else

        await GenerateNewLocalStorageAsync();
        var token = await new AuthorizationService(InvokeServices).GetToken();

        var callback = HttpUtility.UrlEncode($"{NavigationManager.Uri}homePage");
        var uriPath = $"{InvokeServices.ServiceAddress}api/Mobileauth/Authenticate/{providerName}/{false}/encoded?encodedCallback={callback}";
        uriPath += $"&accessToken={token}";
        var authUrl = new Uri(uriPath);
        SchoolServices.Initialized = true;
        SchoolServices.NavDisplay = true;
        NavigationManager.NavigateTo(authUrl.ToString());
#endif
    }

    private void Logout()
    {
        SchoolServices.NavDisplay = false;
        SchoolServices.Initialized = false;
        SchoolServices.SelectedSchool = null;
        SchoolServices.SchoolList = null;
        AppInfo.UserInfo = null;
        InvokeServices.Logout();
        SchoolServices.Email = null;
    }
}
