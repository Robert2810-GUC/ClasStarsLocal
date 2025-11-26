using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using My.ClasStars.Areas.Identity;
using My.ClasStars.Configuration;
using My.ClasStars.Extensions;
using System;
using System.Text;

namespace My.ClasStars
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static IConfiguration Configuration { get; private set; }

        public static TokenValidationParameters TokenValidationParameters { get; private set; }

        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureAuthentication();

            services
                .AddAppConfiguration(Configuration)
                .AddAppFramework()
                .AddAppServices()
                .AddThirdPartyFrameworks();

            services.AddViewLocalization();

            TokenValidationParameters = BuildTokenValidationParameters();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            RegisterSyncfusionLicense();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });
        }

        private void ConfigureAuthentication()
        {
            var signingKey = Environment.GetEnvironmentVariable("WEBSITE_AUTH_SIGNING_KEY") ?? string.Empty;
            var issuer = Configuration["Authentication:Issuer"] ?? Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");

            TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
            };
        }

        private TokenValidationParameters BuildTokenValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = TokenValidationParameters.ValidateIssuer,
                ValidateAudience = TokenValidationParameters.ValidateAudience,
                ValidateLifetime = TokenValidationParameters.ValidateLifetime,
                ValidateIssuerSigningKey = TokenValidationParameters.ValidateIssuerSigningKey,
                ValidIssuer = TokenValidationParameters.ValidIssuer,
                ValidAudience = TokenValidationParameters.ValidAudience,
                IssuerSigningKey = TokenValidationParameters.IssuerSigningKey
            };
        }

        private void RegisterSyncfusionLicense()
        {
            var syncfusionLicense = Configuration.GetValue<string>("SyncfusionLicense");
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicense);
        }
    }
}
