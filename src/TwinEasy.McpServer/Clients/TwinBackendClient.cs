using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwinEasy.McpServer.Configuration;
using TwinEasy.McpServer.Models;
using TwinEasy.McpServer.Services;

namespace TwinEasy.McpServer.Clients;

/// <summary>
/// 孪易后端 HTTP 客户端：负责登录、自动携带 Bearer Token、解析统一响应包。
/// </summary>
public sealed class TwinBackendClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly TwinBackendOptions _options;
    private readonly TwinBackendSessionManager _session;
    private readonly ILogger<TwinBackendClient> _logger;

    public TwinBackendClient(
        HttpClient httpClient,
        IOptions<TwinBackendOptions> options,
        TwinBackendSessionManager session,
        ILogger<TwinBackendClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _session = session;
        _logger = logger;
    }

    public string BaseUrl => _options.BaseUrl;

    public string DefaultOperationalData => _options.OperationalData;

    /// <summary>
    /// Tool 未显式传 operational_data 时，使用配置里的默认操作域。
    /// </summary>
    public string ResolveOperationalData(string? operationalData)
    {
        return string.IsNullOrWhiteSpace(operationalData)
            ? _options.OperationalData
            : operationalData.Trim();
    }

    /// <summary>
    /// 调用 /v1/validateCode?purpose=Login，返回 PNG 验证码 base64。
    /// 该接口是登录前接口，不需要 token。
    /// </summary>
    public async Task<string> GetValidateCodeAsync(string purpose, CancellationToken cancellationToken)
    {
        var normalizedPurpose = string.IsNullOrWhiteSpace(purpose) ? "Login" : purpose.Trim();
        var path = $"/v1/validateCode?purpose={Uri.EscapeDataString(normalizedPurpose)}";
        var envelope = await SendEnvelopeAsync(HttpMethod.Get, path, body: null, includeToken: false, cancellationToken);

        if (envelope.Data.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("验证码接口未返回 base64 字符串。");
        }

        var imageBase64 = envelope.Data.GetString();
        if (string.IsNullOrWhiteSpace(imageBase64))
        {
            throw new InvalidOperationException("验证码接口返回的 base64 字符串为空。");
        }

        return imageBase64;
    }

    /// <summary>
    /// 调用 /v1/login，返回 token 和脱敏后的用户摘要。
    /// 后端登录请求体：{ userName, password, validCode? }。
    /// 自动化登录默认不传 validCode，也不发送 X-Validate-Code-ID 请求头。
    /// 这里的 password 与前端保持一致：明文密码先做 SHA-512，再转大写十六进制。
    /// </summary>
    public async Task<(string AccessToken, string? UserName, object UserSummary)> LoginRawAsync(
        string username,
        string password,
        string? validCode,
        CancellationToken cancellationToken)
    {
        var loginPassword = NormalizeLoginPassword(password);
        var body = new Dictionary<string, object?>
        {
            ["userName"] = username,
            ["password"] = loginPassword
        };

        if (!string.IsNullOrWhiteSpace(validCode))
        {
            body["validCode"] = validCode.Trim();
        }

        var envelope = await SendEnvelopeAsync(HttpMethod.Post, "/v1/login", body, includeToken: false, cancellationToken);
        var data = envelope.Data;
        var token = GetString(data, "accessToken") ?? GetString(data, "token");

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("登录成功，但后端未返回 accessToken/token 字段。");
        }

        var userName = GetString(data, "userName") ?? username;
        var userId = GetString(data, "userID") ?? GetString(data, "userId");
        var userType = GetString(data, "userType");
        var isAdmin = GetBool(data, "isAdmin");

        var userSummary = new
        {
            user_id = userId,
            user_name = userName,
            user_type = userType,
            is_admin = isAdmin,
            access_token_masked = MaskToken(token)
        };

        return (token, userName, userSummary);
    }

    public async Task<JsonElement> GetMyInfoAsync(CancellationToken cancellationToken)
    {
        var envelope = await SendEnvelopeWithAutoLoginAsync(HttpMethod.Get, "/v1/my/info", null, cancellationToken);
        return envelope.Data.Clone();
    }

    public async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        var envelope = await SendEnvelopeWithAutoLoginAsync(HttpMethod.Get, path, null, cancellationToken);
        return envelope.Data.Clone();
    }

    public async Task<JsonElement> PostAsync(string path, object? body, CancellationToken cancellationToken)
    {
        var envelope = await SendEnvelopeWithAutoLoginAsync(HttpMethod.Post, path, body, cancellationToken);
        return envelope.Data.Clone();
    }

    private async Task<TwinBackendEnvelope> SendEnvelopeWithAutoLoginAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        // 业务接口统一先确保登录，再带 token 调后端。
        await _session.EnsureLoggedInAsync(this, cancellationToken);
        return await SendEnvelopeAsync(method, path, body, includeToken: true, cancellationToken);
    }

    private async Task<TwinBackendEnvelope> SendEnvelopeAsync(
        HttpMethod method,
        string path,
        object? body,
        bool includeToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, NormalizeRequestPath(path));

        // 孪易后端要求 Authorization: Bearer {token}。
        if (includeToken && !string.IsNullOrWhiteSpace(_session.CurrentToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.CurrentToken);
        }

        if (body is not null)
        {
            // 明确按 UTF-8 序列化请求体，并保留中文原文，避免路线/区域名称在后端被保存成问号。
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // token 失效后清理缓存，下一次可以重新登录。
            _session.ClearToken();
            throw new UnauthorizedAccessException("孪易后端返回 401，当前 token 已清理。请重新调用 login_twin_backend 登录后再操作。");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Twin backend HTTP {StatusCode} for {Method} {Path}.", response.StatusCode, method, path);
            throw new InvalidOperationException($"Twin backend HTTP {(int)response.StatusCode}: {content}");
        }

        TwinBackendEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<TwinBackendEnvelope>(content, JsonOptions);
        }
        catch (JsonException ex)
        {
            var preview = content.Length > 200 ? content[..200] + "..." : content;
            throw new InvalidOperationException(
                $"孪易后端返回了非 JSON 响应。RequestUri={request.RequestUri}; ResponsePreview={preview}",
                ex);
        }

        if (envelope is null)
        {
            throw new InvalidOperationException("孪易后端返回了空响应或非法 JSON。");
        }

        if (!envelope.Success)
        {
            throw new InvalidOperationException($"孪易后端业务错误 {envelope.Code}: {envelope.Message}");
        }

        return envelope;
    }

    private static string NormalizeRequestPath(string path)
    {
        // 传入 /v1/... 会让 HttpClient 丢掉 BaseAddress 中的 /api/editor。
        // 统一转成相对路径 v1/...，保证 BaseUrl=http://host/api/editor 能拼成 /api/editor/v1/...
        return path.TrimStart('/');
    }

    private static string NormalizeLoginPassword(string password)
    {
        var trimmedPassword = password.Trim();
        if (IsSha512Hex(trimmedPassword))
        {
            return trimmedPassword.ToUpperInvariant();
        }

        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = SHA512.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool IsSha512Hex(string value)
    {
        if (value.Length != 128)
        {
            return false;
        }

        foreach (var ch in value)
        {
            var isHex =
                ch is >= '0' and <= '9' ||
                ch is >= 'a' and <= 'f' ||
                ch is >= 'A' and <= 'F';

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static string MaskToken(string token)
    {
        if (token.Length <= 12)
        {
            return "***";
        }

        return $"{token[..6]}...{token[^6..]}";
    }
}
