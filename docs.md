# .NETCORE 中的 Generic Host

## 前言

在创建的ASPNETCORE项目中，我们可以在`Main()`中看见，我们通过`IWebHostBuild`创建了一个`IWebHost`，而微软提供了`WebHost.CreateDefaultBuilder(args)`来帮助我们更轻松得创建`WebHost`。

常常我们的需求不需要创建Web项目，比如后台任务，那么我们如何像使用AspNetCore一样创建控制台项目。

## 如何在控制台程序中创建主机

1. 通过`dotnet new console` 创建一个控制台项目
2. 通过Nuget添加以下包
    * Microsoft.Extensions.Hosting

首先，我们看下`IHostBuilder`接口里的方法

```c#
public interface IHostBuilder
{
    IHost Build();

    IHostBuilder ConfigureAppConfiguration(Action<HostBuilderContext, IConfigurationBuilder> configureDelegate);

    IHostBuilder ConfigureContainer<TContainerBuilder>(Action<HostBuilderContext, TContainerBuilder> configureDelegate);

    IHostBuilder ConfigureHostConfiguration(Action<IConfigurationBuilder> configureDelegate);

    IHostBuilder ConfigureServices(Action<HostBuilderContext, IServiceCollection> configureDelegate);
    
    IHostBuilder UseServiceProviderFactory<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory);
}

```

* `ConfigureAppConfiguration()` 可以配置应用的一些配置，如环境变量等等
* `ConfigureContainer()` & `UseServiceProviderFactory()` 可以配置替换默认的依赖注入的组件，比如替换成`Autofac`
* `ConfigureHostConfiguration()` 可以配置`IConfiguration`
* `ConfigureServices()` 可以注入服务


接下去，通过以下代码，我们可以构建一个简单的主机。

```c#
static void Main(string[] args)
{
    CreateDefaultHost(args).Build().Run();
}

static IHostBuilder CreateDefaultHost(string[] args) => new HostBuilder()
    .ConfigureHostConfiguration(builder =>
    {
        //todo
    })
    .ConfigureAppConfiguration((ctx, builder) =>
    {
        builder
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", true, true)
            .AddEnvironmentVariables()
            ;
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddLogging();
        services.AddHostedService<CustomHostService>();
    })
    .UseConsoleLifetime()
    ;

```

```c#

public class CustomHostService: IHostedService
{

    private ILogger _logger;
    private Task _executingTask;

    public Task StartAsync(...)
    {
        _logger.LogInformation($"{nameof(CustomHostService):}start");

        _executingTask = ExecuteAsync(...);
        if(_executingTask.IsCompleted){
            return _executingTask;
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    public Task ExecuteAsync(...)
    {
        _logger.LogInformation($"{nameof(CustomHostService):executing}")
        return Task.Delay(5000);
    }

}

```

如上，我们自定义的 `CustomHostService` 需要实现 `IHostedService`接口，当然，我们可以直接继承 `BackgoundService` 类。

在实现了 `IHostedService` 接口后，我们通过 `services.AddHostedService<>()` 进行注入，或者通过 `service.AddTransient<IHostedService,THostedService>()` 进入注入。

*启动以上项目，我们发现，我们的程序默认的`Hosting Environment`一直是`Production`，那么如何修改呢 ??*

## 配置环境变量

在AspNetCore项目中，我们可以通过设置环境变量`ASPNETCORE_ENVIRONMENT`的值来指定主机环境变量的。而在Generic Host 中暂时没有这一项配置。

如果查看`IHostBuilder`的扩展，我们会发现以下方法:

```c#

new HostBuilder()
    .UseContentRoot(...)
    .UseEnvironment(...)
    ...

```

查看源代码后，我们可以通过`ConfigureHostConfiguration()`方法将这些配置配置到主机中。

现在我们假设我们以`DOTNETCORE_ENVIRONMENT`来指定GenericHost的环境。

```c#
new HostBuilder().ConfigureHostConfiguration(builder =>
    {
        builder.AddInMemoryCollection(new Dictionary<string, string>
        {
            [HostDefaults.EnvironmentKey] = Environment.GetEnvironmentVariable("DOTNETCORE_ENVIRONMENT"),
        })
        // Nuget:Microsoft.Extensions.Configuration.CommandLine
        //.AddCommandLine(args) 
        ;
    })
    
    //...

```

现在让我们打开命令行测试下。设置完成环境变量后我们通过`dotnet run` 启动程序。查看输出，Host Environment 变成为 `Stage`

```powershell
# 设置环境变量
$env:DOTNETCORE_ENVIRONMENT='Stage'
# 查看环境变量
$env:DOTNETCORE_ENVIRONMENT
```

当然我们也可以通过 commandline 的参数来设置启动的环境变量等值。
> Install-Package Microsoft.Extensions.Configuration.CommandLine


在`ConfigureHostConfiguration()`中使用`.AddCommandLine(args)`来指定参数。

现在我们可以通过 `dotnet run --environment=Development`来指定dev环境了，此时我们发现我们终于成功加载`appsettings.Development.json`中的配置信息了。


## 使用Autofac来替代默认的 DI

### 简单认识一下Autofac

一个第三方的依赖注入容器，相对`Microsft.Extensions.DependencyInjection`使用更加简单方便。

### 集成到Host中

通过Nuget安装以下两个包

> Install-Package Autofac  

> Install-Package Autofac.Extensions.DependencyInection

我们可以使用`UseServiceProviderFactory()`和`service.AddAutofac()` 将默认的DI 替换成 `Autofac`;

使用`ConfigureContainer<ContainerBuilder>()`可以使用Autofac来注入服务；

```c#
//省略了非关键代码
static IHostBuilder CreateDefaultHost(string[] args) => new HostBuilder()
//...略
    .ConfigureServices((ctx, services) =>
    {
        services.AddLogging(x=>{x.AddConsole();});

        services.AddAutofac();
    })
    .ConfigureContainer<ContainerBuilder>(builder => 
    {
        builder.RegisterType<CustomHostService>()
        .As<IHostedService>()
        .InstancePerDependency();
    })          
    .UseServiceProviderFactory<ContainerBuilder>(new AutofacServiceProviderFactory())
//...略
```

## 总结

个人认为出现GenericHost解决的几个痛点，相对AspNetCore中的管道机制，控制台程序如果不依靠GenericHost来管理Di，想进行大量`Microsoft.Extensions`包的集成会非常困难。通过IHostedService，可以方便的进行服务的托管。