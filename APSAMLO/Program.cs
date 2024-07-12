using Microsoft.Extensions.DependencyInjection;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config")]

namespace APSAMLO
{
    public static class Program
    {
        public static async Task Main()
        {
            var services = new ServiceCollection();
            services.AddSingleton<Application>();

            var serviceProvider = services.BuildServiceProvider();

            var Service = serviceProvider.GetService<Application>();
            await Service.Run();
        }
    }
}