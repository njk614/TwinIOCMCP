using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwinEasy.McpServer.Clients;
using TwinEasy.McpServer.Configuration;

namespace TwinEasy.McpServer.Services;

/// <summary>
/// 管理当前 MCP Server 进程内的孪易登录会话。
/// </summary>
public sealed class TwinBackendSessionManager
{
    private readonly TwinBackendOptions _options;
    private readonly ILogger<TwinBackendSessionManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private object? _currentUser;

    public TwinBackendSessionManager(
        IOptions<TwinBackendOptions> options,
        ILogger<TwinBackendSessionManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool HasToken => !string.IsNullOrWhiteSpace(_accessToken);

    public string? CurrentToken => _accessToken;

    public object? CurrentUser => _currentUser;

    /// <summary>
    /// 登录孪易后端。username/password 可为空，为空时使用 appsettings.json 中的默认账号。
    /// 自动化调用默认不传验证码；后端只有在请求头包含 X-Validate-Code-ID 时才会校验验证码。
    /// </summary>
    public async Task<object?> LoginAsync(
        TwinBackendClient client,
        string? username,
        string? password,
        string? validCode,
        CancellationToken cancellationToken)
    {
        var resolvedUsername = string.IsNullOrWhiteSpace(username) ? _options.Username : username.Trim();
        var resolvedPassword = string.IsNullOrWhiteSpace(password) ? _options.Password : password;

        if (string.IsNullOrWhiteSpace(resolvedUsername) || string.IsNullOrWhiteSpace(resolvedPassword))
        {
            throw new InvalidOperationException("缺少孪易登录账号或密码。请在 login_twin_backend 中传入 username/password，或在 appsettings.json 中配置 TwinBackend:Username / TwinBackend:Password。");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var normalizedValidCode = string.IsNullOrWhiteSpace(validCode) ? null : validCode.Trim();
            var loginResult = await client.LoginRawAsync(resolvedUsername, resolvedPassword, normalizedValidCode, cancellationToken);
            _accessToken = loginResult.AccessToken;
            _currentUser = loginResult.UserSummary;
            _logger.LogInformation("Logged in to TwinEasy backend as {UserName}.", loginResult.UserName);
            return _currentUser;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// 业务接口调用前确保已有 token。可先用 login_twin_backend 通过用户名和密码登录。
    /// </summary>
    public Task EnsureLoggedInAsync(TwinBackendClient client, CancellationToken cancellationToken)
    {
        if (HasToken)
        {
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("当前尚未登录孪易后端。请先调用 login_twin_backend 输入用户名和密码登录；如后端要求验证码，再调用 get_login_validate_code 获取验证码后重试。");
    }

    /// <summary>
    /// 401 或用户需要重新登录时清理本地 token。
    /// </summary>
    public void ClearToken()
    {
        _accessToken = null;
        _currentUser = null;
    }
}
