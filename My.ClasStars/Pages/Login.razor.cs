#define AUTOLOGIN
using LogonServiceRequestTypes.Enums;
using LogonServiceRequestTypes;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Web;
#if !AUTOLOGIN
using System.Web;
#endif
using Microsoft.AspNetCore.Components;

namespace My.ClasStars.Pages;

public partial class Login
{
    [Inject] private IAuthorizationService AuthorizationService { get; set; }

    public List<ExternalDataProvider> ProviderList; // API

    private readonly List<ExternalProviders> OrderButton = new List<ExternalProviders>(); // Display

    public List<ExternalProviders> CookieProvidersList = new List<ExternalProviders>(); // Cookies

    protected override async Task OnInitializedAsync()
    {
        Logout();
        await AuthorizationService.GetToken();
        SchoolServices.NavDisplay = false;
        // Get API data
        var secInfo = new AnonymousRequestSecureInfo();
        var retData = await InvokeServices.InvokePostAsync<AnonymousRequestSecureInfo, List<ExternalDataProvider>>(ServiceEndpoint.ExternalIntegration,
    ServiceAction.GetSupportedLoginProviders, secInfo);
        ProviderList = retData;


        for (var i = 0; i < (ProviderList.Count); i++)
        {
            OrderButton.Add(new ExternalProviders { Name = ProviderList[i].ToString(), LastLoginDate = null, ImageUrl = "Images/" + getImageName(ProviderList[i].ToString()), ExpiryDate = null });
        }

        await LoadProvidersList();
    }

    private string getImageName(string ProviderName)
    {
        ProviderName = ProviderName.ToLower();
        string FileName = string.Empty;
        switch (ProviderName)
        {
            case "google":
                FileName = "googlelogo.jpg";
                break;

            case "classtars":
                FileName = "classtars.svg";
                break;

            case "apple":
                FileName = "apple.svg";
                break;

            case "facebook":
                FileName = "facebook.svg";
                break;

            case "clever":
                FileName = "CleverLOGO.jpg";
                break;

            case "classlink":
                FileName = "classlinklogo.jpg";
                break;

            case "nycdoe":
                FileName = "NycDoe.svg";
                break;
        }
        return FileName;
    }

    private async Task LoadProvidersList()
    {
        try
        {
            var providersJson = await localStorage.GetItemAsync<string>("ProvidersList");
            if (!string.IsNullOrEmpty(providersJson))
            {
                CookieProvidersList = JsonConvert.DeserializeObject<List<ExternalProviders>>(providersJson);
                for (var i = (CookieProvidersList.Count) - 1; i >= 0; i--)
                {
                    if (CookieProvidersList[i].ExpiryDate < DateTime.Now)
                    {
                        var expiredCookie = CookieProvidersList.FirstOrDefault(p => p.Name == CookieProvidersList[i].Name);
                        CookieProvidersList.Remove(expiredCookie);
                    }
                }

                GenerateNewLocalStorage();
                if (CookieProvidersList.Count != 0)
                {
                    foreach (var t in OrderButton)
                    {
                        foreach (var t1 in CookieProvidersList)
                        {
                            if (t.Name == t1.Name)
                            {
                                t.LastLoginDate = t1.LastLoginDate;
                                break;
                            }
                        }
                    }
                }
            }
        }
        //If the cookies returns invalid data
        catch (Exception)
        {
            // ignored
        }
    }

    private async void GenerateNewLocalStorage()
    {
        var providerList = JsonConvert.SerializeObject(CookieProvidersList);
        await localStorage.SetItemAsync("ProvidersList", providerList);
    }

    private async void HandleLogin(string item)
    {
        try
        {
            var providersListJson = await localStorage.GetItemAsync<string>("ProvidersList");
            if (!string.IsNullOrEmpty(providersListJson))
            {
                CookieProvidersList = JsonConvert.DeserializeObject<List<ExternalProviders>>(providersListJson);
            }
            var provider = CookieProvidersList.FirstOrDefault(p => p.Name == item);

            if (provider != null)
            {
                provider.LastLoginDate = DateTime.Now;
                provider.ExpiryDate = DateTime.Now.AddYears(1);
            }
            else
            {
                CookieProvidersList.Add(new ExternalProviders { Name = item, LastLoginDate = DateTime.Now, ImageUrl = null, ExpiryDate = DateTime.Now.AddYears(1) });
            }
        }
        catch (Exception)
        {
            await localStorage.RemoveItemAsync("ProvidersList");
            CookieProvidersList.Add(new ExternalProviders { Name = item, LastLoginDate = DateTime.Now, ImageUrl = null, ExpiryDate = DateTime.Now.AddYears(1) });
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

        var providerJson = JsonConvert.SerializeObject(CookieProvidersList);
        await localStorage.SetItemAsync("ProvidersList", providerJson);
        var token = await AuthorizationService.GetToken();

        var callback = HttpUtility.UrlEncode($"{NavigationManager.Uri}homePage");
        var uriPath = $"{InvokeServices.ServiceAddress}api/Mobileauth/Authenticate/{item}/{false}/encoded?encodedCallback={callback}";
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