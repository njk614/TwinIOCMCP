namespace TwinEasy.McpServer.Configuration;

/// <summary>
/// 孪易后端连接配置。可以来自 appsettings.json，也可以用环境变量覆盖。
/// </summary>
public sealed class TwinBackendOptions
{
    public const string SectionName = "TwinBackend";

    /// <summary>孪易后端 API 根地址，例如 http://127.0.0.1:5000。</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:5000";

    /// <summary>默认操作域：UserData 或 IndustryData。</summary>
    public string OperationalData { get; set; } = "UserData";

    /// <summary>可选默认登录用户名；配置后 login_twin_backend 可不再手动传 username。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>可选默认登录密码；配置后 login_twin_backend 可不再手动传 password。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>调用后端接口的超时时间。</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
