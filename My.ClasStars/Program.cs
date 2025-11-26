using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;

namespace My.ClasStars
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = BuildBootstrapLogger();

            try
            {
                CreateHostBuilder(args).Build().Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog((context, _, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .MinimumLevel.Error()
                    .WriteTo.File(
                        path: context.Configuration.GetValue("Logging:FilePath", "Logs/classtars-log-.txt"),
                        rollingInterval: RollingInterval.Day))
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });

        private static Logger BuildBootstrapLogger()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            return new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.File(
                    path: configuration.GetValue("Logging:FilePath", "Logs/classtars-log-.txt"),
                    rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }
    }
}
