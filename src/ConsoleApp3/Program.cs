using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace ConsoleApp3
{
    class Program
    {
        static void Main(string[] args)
        {
            CreateDefaultHost(args).Build().Run();
        }

        static IHostBuilder CreateDefaultHost(string[] args)
        {
            return new HostBuilder()
                .ConfigureHostConfiguration(builder =>
                {
                    builder.AddInMemoryCollection(new Dictionary<string, string>
                    {
                        [HostDefaults.EnvironmentKey] = Environment.GetEnvironmentVariable("DOTNETCORE_ENVIRONMENT"),
                    })
                    .AddCommandLine(args)
                    ;
                })
                .ConfigureAppConfiguration((ctx, builder) =>
                {
                    builder.SetBasePath(AppContext.BaseDirectory)
                        .AddJsonFile("appsettings.json", false, true)
                        .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", true, true)
                        ;
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddAutofac();
                    services.AddLogging(x=> {
                        x.AddConsole();
                    });
                })
                .ConfigureContainer<ContainerBuilder>(builder => {

                    builder.RegisterType<CustomBackgroundTask>()
                    .As<IHostedService>()
                    .InstancePerDependency();
                })
                .UseServiceProviderFactory<ContainerBuilder>(new AutofacServiceProviderFactory())
                .UseConsoleLifetime()
                ;

        }
    }
}
