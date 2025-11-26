using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using My.ClasStars.Configuration;
using My.ClasStars;
using Syncfusion.Blazor;

namespace My.ClasStars.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(options =>
        {
            options.ClasstarsAuthSecret = configuration["Authentication:ClasstarsAuthSecret"]
                                        ?? configuration["ClasstarsAuthSecret"]
                                        ?? string.Empty;
            options.Issuer = configuration["Authentication:Issuer"] ?? configuration["WEBSITE_HOSTNAME"] ?? string.Empty;
        });

        services.Configure<ServiceEndpointOptions>(options =>
        {
            var serviceAddress = configuration["ServiceAddress"] ?? configuration["Services:BaseAddress"] ?? string.Empty;
            options.ServiceAddress = serviceAddress;
            options.MobileAuthServiceAddress = configuration["MobileAuthServiceAddress"]
                                           ?? configuration["Services:MobileAuthBaseAddress"]
                                           ?? serviceAddress;
        });

        return services;
    }

    public static IServiceCollection AddAppFramework(this IServiceCollection services)
    {
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddLocalization();
        return services;
    }

    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
        services.AddSingleton<SchoolListService>();
        services.AddScoped<ExternalProviders>();
        services.AddSingleton<CommonLocalizationService>();
        services.AddSingleton<IInvokeServices, InvokeServices>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddSingleton<IClasStarsServices, ClasStarsServices>();
        return services;
    }

    public static IServiceCollection AddThirdPartyFrameworks(this IServiceCollection services)
    {
        services.AddSyncfusionBlazor();
        services.AddSignalR(o => { o.MaximumReceiveMessageSize = 102400000; });
        services.AddBlazoredLocalStorage();
        services.AddBlazoredLocalStorage(config => config.JsonSerializerOptions.WriteIndented = true);
        services.AddAuthentication().AddJwtBearer();
        return services;
    }
}
