using LogonServiceRequestTypes;
using LogonServiceRequestTypes.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace My.ClasStars.Pages;

public partial class Login
{
    private const string ProvidersStorageKey = "ProvidersList";

    public List<ExternalDataProvider> ProviderList { get; private set; } = new();
    private readonly List<ExternalProviders> _providerButtons = new();
    public List<ExternalProviders> CookieProvidersList { get; private set; } = new();

    [Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        ResetSessionState();

        await AuthorizationService.GetToken();
        SchoolServices.NavDisplay = false;

        ProviderList = await LoadProvidersFromApiAsync();
        BuildProviderButtons();
        await LoadProvidersFromStorageAsync();
    }

    private async Task<List<ExternalDataProvider>> LoadProvidersFromApiAsync()
    {
        var secureInfo = new AnonymousRequestSecureInfo();
        var providers = await InvokeServices.InvokePostAsync<AnonymousRequestSecureInfo, List<ExternalDataProvider>>(
            ServiceEndpoint.ExternalIntegration,
            ServiceAction.GetSupportedLoginProviders,
            secureInfo);

        return providers ?? new List<ExternalDataProvider>();
    }

    private void BuildProviderButtons()
    {
        _providerButtons.Clear();

        foreach (var provider in ProviderList)
        {
            var providerName = provider.ToString();
            _providerButtons.Add(new ExternalProviders
            {
                Name = providerName,
                ImageUrl = $"Images/{GetImageName(providerName)}"
            });
        }
    }

    private static string GetImageName(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "google" => "googlelogo.jpg",
            "classtars" => "classtars.svg",
            "apple" => "apple.svg",
            "facebook" => "facebook.svg",
            "clever" => "CleverLOGO.jpg",
            "classlink" => "classlinklogo.jpg",
            "nycdoe" => "NycDoe.svg",
            _ => string.Empty
        };
    }

    private async Task LoadProvidersFromStorageAsync()
    {
        try
        {
            var providersJson = await localStorage.GetItemAsync<string>(ProvidersStorageKey);
            if (string.IsNullOrWhiteSpace(providersJson))
            {
                return;
            }

            var persistedProviders = JsonConvert.DeserializeObject<List<ExternalProviders>>(providersJson) ?? new();
            CookieProvidersList = persistedProviders
                .Where(provider => !provider.ExpiryDate.HasValue || provider.ExpiryDate.Value >= DateTime.UtcNow)
                .ToList();

            foreach (var provider in _providerButtons)
            {
                provider.LastLoginDate = CookieProvidersList.FirstOrDefault(p => p.Name == provider.Name)?.LastLoginDate;
            }

            await PersistProvidersAsync();
        }
        catch
        {
            CookieProvidersList.Clear();
            await localStorage.RemoveItemAsync(ProvidersStorageKey);
        }
    }

    private async Task PersistProvidersAsync()
    {
        var providerList = JsonConvert.SerializeObject(CookieProvidersList);
        await localStorage.SetItemAsync(ProvidersStorageKey, providerList);
    }

    private async Task HandleLogin(string providerName)
    {
        await RefreshPersistedProvidersAsync();
        UpdateProviderHistory(providerName);
        await PersistProvidersAsync();

        var token = await AuthorizationService.GetToken();
        var callback = HttpUtility.UrlEncode($"{NavigationManager.Uri}homePage");
        var uriPath = $"{InvokeServices.ServiceAddress}api/Mobileauth/Authenticate/{providerName}/{false}/encoded?encodedCallback={callback}";
        uriPath += $"&accessToken={token}";

        SchoolServices.Initialized = true;
        SchoolServices.NavDisplay = true;
        NavigationManager.NavigateTo(new Uri(uriPath).ToString());
    }

    private async Task RefreshPersistedProvidersAsync()
    {
        var providersListJson = await localStorage.GetItemAsync<string>(ProvidersStorageKey);
        if (!string.IsNullOrEmpty(providersListJson))
        {
            CookieProvidersList = JsonConvert.DeserializeObject<List<ExternalProviders>>(providersListJson) ?? new();
        }
    }

    private void UpdateProviderHistory(string providerName)
    {
        var provider = CookieProvidersList.FirstOrDefault(p => p.Name == providerName);
        if (provider != null)
        {
            provider.LastLoginDate = DateTime.UtcNow;
            provider.ExpiryDate = DateTime.UtcNow.AddYears(1);
        }
        else
        {
            CookieProvidersList.Add(new ExternalProviders
            {
                Name = providerName,
                LastLoginDate = DateTime.UtcNow,
                ImageUrl = null,
                ExpiryDate = DateTime.UtcNow.AddYears(1)
            });
        }
    }

    private void ResetSessionState()
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
