using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;

namespace My.ClasStars
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger= new LoggerConfiguration()
                .MinimumLevel.Error()
                .WriteTo.File(@"C:\logs\Classtarslogs.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
