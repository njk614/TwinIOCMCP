using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using TwinEasy.McpServer.Clients;
using TwinEasy.McpServer.Configuration;
using TwinEasy.McpServer.Services;
using TwinEasy.McpServer.Tools;

var bootstrapConfiguration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: true)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var transport = bootstrapConfiguration["Mcp:Transport"] ?? "stdio";

if (IsHttpTransport(transport))
{
    await RunHttpServerAsync(args);
}
else
{
    await RunStdioServerAsync(args);
}

static async Task RunStdioServerAsync(string[] args)
{
    var builder = Host.CreateApplicationBuilder(args);

    // MCP 客户端通常从自己的工作目录启动服务，所以额外读取程序输出目录的配置文件。
    builder.Configuration.AddJsonFile(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        optional: true,
        reloadOnChange: false);

    ConfigureLogging(builder.Logging, logToStdErr: true);
    ConfigureTwinEasyServices(builder.Services, builder.Configuration);

    ConfigureMcpServer(builder.Services)
        // stdio 模式由 Codex 拉起子进程，并通过 stdin/stdout 传输 MCP JSON-RPC 消息。
        .WithStdioServerTransport();

    await builder.Build().RunAsync();
}

static async Task RunHttpServerAsync(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddJsonFile(
        Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
        optional: true,
        reloadOnChange: false);

    var httpUrl = builder.Configuration["Mcp:HttpUrl"];
    if (!string.IsNullOrWhiteSpace(httpUrl))
    {
        builder.WebHost.UseUrls(httpUrl);
    }

    ConfigureLogging(builder.Logging, logToStdErr: false);
    ConfigureTwinEasyServices(builder.Services, builder.Configuration);

    ConfigureMcpServer(builder.Services)
        // HTTP 模式使用 Streamable HTTP，Codex 通过固定 URL 连接这个常驻服务。
        .WithHttpTransport(_ => { });

    var app = builder.Build();

    var endpointPath = builder.Configuration["Mcp:EndpointPath"] ?? "/mcp";
    app.MapGet("/", () => Results.Ok(new
    {
        name = "TwinEasy Standard MCP Server",
        transport = "http",
        mcp_endpoint = endpointPath
    }));
    app.MapMcp(endpointPath);

    await app.RunAsync();
}

static IMcpServerBuilder ConfigureMcpServer(IServiceCollection services)
{
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = false
    };

    return services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "TwinEasy Standard MCP Server",
                Version = "1.0.0"
            };
        })
        // 扫描带 [McpServerToolType] / [McpServerTool] 的类和方法，并注册为 MCP Tools。
        .WithToolsFromAssembly(typeof(ContextMcpTools).Assembly, jsonOptions);
}

static void ConfigureTwinEasyServices(IServiceCollection services, IConfiguration configuration)
{
    // 绑定 appsettings.json / 环境变量中的 TwinBackend 配置。
    services.Configure<TwinBackendOptions>(
        configuration.GetSection(TwinBackendOptions.SectionName));

    services.AddHttpClient<TwinBackendClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<TwinBackendOptions>>().Value;

        // BaseUrl 可能包含 /api/editor 这类路径，必须以 / 结尾，后续相对路径才能正确拼接。
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

    // 当前登录 token 存在 MCP Server 进程内；HTTP 常驻时会在服务生命周期内复用。
    services.AddSingleton<TwinBackendSessionManager>();
    services.AddSingleton<TwinToolResultFactory>();
    services.AddSingleton<ContextMcpTools>();
    services.AddSingleton<SceneMcpTools>();
    services.AddSingleton<SpatialObjectMcpTools>();
}

static void ConfigureLogging(ILoggingBuilder logging, bool logToStdErr)
{
    logging.ClearProviders();
    logging.AddConsole(options =>
    {
        // stdio 模式下 stdout 是协议通道，普通日志必须走 stderr；HTTP 模式可以正常输出。
        options.LogToStandardErrorThreshold = logToStdErr ? LogLevel.Trace : LogLevel.None;
    });
}

static bool IsHttpTransport(string transport)
{
    return string.Equals(transport, "http", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(transport, "streamable_http", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(transport, "streamable-http", StringComparison.OrdinalIgnoreCase);
}
