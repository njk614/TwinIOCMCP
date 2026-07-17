using System.ComponentModel;
using ModelContextProtocol.Server;
using TwinEasy.McpServer.Clients;
using TwinEasy.McpServer.Models;
using TwinEasy.McpServer.Services;

namespace TwinEasy.McpServer.Tools;

/// <summary>
/// 上下文类 Tool：负责登录、查看当前后端连接和会话状态；验证码工具仅作为备用排障入口。
/// </summary>
[McpServerToolType]
public sealed class ContextMcpTools
{
    private readonly TwinBackendClient _backendClient;
    private readonly TwinBackendSessionManager _session;
    private readonly TwinToolResultFactory _results;

    public ContextMcpTools(
        TwinBackendClient backendClient,
        TwinBackendSessionManager session,
        TwinToolResultFactory results)
    {
        _backendClient = backendClient;
        _session = session;
        _results = results;
    }

    /// <summary>
    /// 备用工具。后端映射：GET /v1/validateCode?purpose=Login。
    /// 当前自动化登录默认不用验证码；只有后端环境明确要求验证码时才调用。
    /// </summary>
    [McpServerTool]
    [Description("备用工具：获取登录验证码图片。当前自动化登录默认不用验证码，只有后端明确要求时才调用。")]
    public async Task<McpToolResult> get_login_validate_code(
        [Description("验证码用途，登录固定为 Login。")] string purpose = "Login")
    {
        try
        {
            var imageBase64 = await _backendClient.GetValidateCodeAsync(purpose, CancellationToken.None);
            var imageDataUri = $"data:image/png;base64,{imageBase64}";

            return _results.Success(
                "已获取备用登录验证码图片。当前默认登录流程不需要验证码；仅在后端明确要求验证码时使用。",
                new
                {
                    purpose,
                    mime_type = "image/png",
                    image_base64 = imageBase64,
                    image_data_uri = imageDataUri,
                    backend_api = "GET /v1/validateCode?purpose=Login",
                    default_usage = "not_used",
                    next_login_tool = "login_twin_backend",
                    login_valid_code_field = "valid_code"
                },
                nextActions: new[]
                {
                    "默认不要调用本工具，直接使用 login_twin_backend 用户名密码登录。",
                    "如果后端明确要求验证码，再人工查看 image_data_uri，并把验证码传给 login_twin_backend.valid_code。"
                });
        }
        catch (Exception ex)
        {
            return _results.Failed("获取登录验证码失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：POST /v1/login。
    /// 请求体：{ userName, password, validCode? }。后端只有在请求头包含 X-Validate-Code-ID 时才校验验证码，
    /// 因此自动化调用默认不传验证码，也不会发送 X-Validate-Code-ID。
    /// </summary>
    [McpServerTool]
    [Description("使用用户名和密码登录孪易后端，并缓存 Bearer Token；验证码可选，自动化调用通常不需要传。")]
    public async Task<McpToolResult> login_twin_backend(
        [Description("登录验证码，可选。只有后端要求验证码时才需要先调用 get_login_validate_code 后填入。")] string? valid_code = null,
        [Description("孪易后端用户名；不传时使用 appsettings.json 的 TwinBackend:Username。")] string? username = null,
        [Description("孪易后端密码；不传时使用 appsettings.json 的 TwinBackend:Password。")] string? password = null)
    {
        try
        {
            var user = await _session.LoginAsync(_backendClient, username, password, valid_code, CancellationToken.None);
            return _results.Success(
                "已登录孪易后端，后续工具会自动携带 token。",
                new
                {
                    backend_base_url = _backendClient.BaseUrl,
                    default_operational_data = _backendClient.DefaultOperationalData,
                    user
                },
                nextActions: new[]
                {
                    "调用 get_twin_mcp_context 确认当前用户和租户上下文。",
                    "调用 get_location_asset_folder_root、list_scenes 或 create_scene 开始 V1.0 场景配置流程。"
                });
        }
        catch (Exception ex)
        {
            return _results.Failed("登录孪易后端失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：GET /v1/my/info。
    /// 如果当前还没有 token，只返回 MCP Server 本地配置和未登录状态。
    /// </summary>
    [McpServerTool]
    [Description("读取当前 MCP Server 的后端连接、默认操作域和当前登录用户信息。")]
    public async Task<McpToolResult> get_twin_mcp_context()
    {
        try
        {
            object? myInfo = null;
            if (_session.HasToken)
            {
                myInfo = await _backendClient.GetMyInfoAsync(CancellationToken.None);
            }

            return _results.Success(
                "已读取孪易 MCP 当前上下文。",
                new
                {
                    backend_base_url = _backendClient.BaseUrl,
                    default_operational_data = _backendClient.DefaultOperationalData,
                    is_logged_in = _session.HasToken,
                    session_user = _session.CurrentUser,
                    my_info = myInfo,
                    login_hint = _session.HasToken
                        ? "当前已登录。"
                        : "当前未登录：请直接调用 login_twin_backend 使用用户名和密码登录；验证码工具默认不用。",
                    v1_0_tools = new[]
                    {
                        "login_twin_backend",
                        "get_twin_mcp_context",
                        "get_location_asset_folder_root",
                        "list_scenes",
                        "create_scene",
                        "get_scene",
                        "update_scene",
                        "rename_scene",
                        "delete_scene",
                        "copy_scene",
                        "create_scene_hierarchy",
                        "get_scene_hierarchy",
                        "update_scene_hierarchy",
                        "rename_scene_hierarchy",
                        "delete_scene_hierarchy",
                        "move_scene_hierarchy",
                        "list_scene_hierarchies",
                        "create_scene_poi",
                        "list_scene_pois",
                        "get_scene_poi",
                        "update_scene_poi",
                        "rename_scene_poi",
                        "delete_scene_poi"
                    },
                    optional_tools_not_used_by_default = new[]
                    {
                        "get_login_validate_code"
                    }
                });
        }
        catch (Exception ex)
        {
            return _results.Failed("读取孪易 MCP 上下文失败。", new[] { ex.Message });
        }
    }
}
