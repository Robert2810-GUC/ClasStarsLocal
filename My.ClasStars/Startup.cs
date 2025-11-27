using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using My.ClasStars.Areas.Identity;
using Blazored.LocalStorage;
using Microsoft.IdentityModel.Tokens;
using Syncfusion.Blazor;
using System.Text;
using System;
using My.ClasStars.Components;

namespace My.ClasStars
{
    public class Startup 
    {

        public static string SigningKey;
        private static SymmetricSecurityKey _secureSigningKey;
        public static string Issuer;

        public static TokenValidationParameters TokenValidationParameters { get; private set; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            SigningKey = Environment.GetEnvironmentVariable("WEBSITE_AUTH_SIGNING_KEY") ?? "";
            _secureSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
            Issuer = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME");
        }

        public static IConfiguration Configuration { get; private set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940

        public void ConfigureServices(IServiceCollection services)
        {


            TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = Issuer,
                ValidAudience = Issuer,
                IssuerSigningKey = _secureSigningKey
            };

         
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
            services.AddSingleton<SchoolListService>();
            services.AddScoped<ExternalProviders>();

            //Resource File Service
            services.AddRazorPages().AddViewLocalization();
            services.AddSingleton<CommonLocalizationService>();

            //Syncfusion Pie Chart Service
            services.AddSyncfusionBlazor();
            services.AddSignalR(o => { o.MaximumReceiveMessageSize = 102400000; });
            //Local Storage Service
            services.AddBlazoredLocalStorage();
            services.AddBlazoredLocalStorage(config =>
                config.JsonSerializerOptions.WriteIndented = true);

            services.AddSingleton<IInvokeServices, InvokeServices>();
            services.AddSingleton<IClasStarsServices, ClasStarsServices>();
            services.AddScoped<ToastService>();

            services.AddAuthentication()
                .AddJwtBearer();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NGaF5cXmdCeUx0Q3xbf1xzZFNMYVpbQHJPMyBoS35RdUVkWHZednVTQmRVU0Rw");
            var syncfusionLicense = Configuration.GetValue<string>("SyncfusionLicense");
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicense);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                //app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
    }
}
