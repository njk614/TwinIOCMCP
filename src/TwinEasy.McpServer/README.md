# TwinEasy.McpServer

孪易标准版执行型 MCP Server。当前服务用于让 Codex、OpenCode 等 Agent 工具通过 MCP 调用孪易后端配置接口。

## 启动服务

先确认本机已安装 .NET 8 SDK，然后在仓库根目录运行：

```powershell
dotnet run --project E:\AI\TwinIOCMCP\src\TwinEasy.McpServer\TwinEasy.McpServer.csproj
```

默认使用 HTTP MCP 服务：

```text
http://172.16.5.34:5108/mcp
```

代码修改后，只需要重启 `TwinEasy.McpServer` 服务。只要 MCP URL 不变，Codex / OpenCode 通常不用重启。

## 服务配置

配置文件：

```text
src/TwinEasy.McpServer/appsettings.json
```

关键配置：

```json
{
  "Mcp": {
    "Transport": "http",
    "HttpUrl": "http://172.16.5.34:5108",
    "EndpointPath": "/mcp"
  },
  "TwinBackend": {
    "BaseUrl": "http://test.twinioc.net/api/editor",
    "OperationalData": "UserData",
    "Username": "",
    "Password": "",
    "TimeoutSeconds": 60
  }
}
```

- `Mcp:Transport`：MCP 传输模式，当前推荐 `http`。
- `Mcp:HttpUrl`：HTTP 服务监听地址。
- `Mcp:EndpointPath`：MCP endpoint，默认 `/mcp`。
- `TwinBackend:BaseUrl`：孪易后端 API 根地址。
- `TwinBackend:OperationalData`：默认操作域，通常为 `UserData`。
- `TwinBackend:Username` / `Password`：可选默认账号密码；配置后可直接调用 `login_twin_backend`。

## Codex 配置

把下面配置加入 Codex 的 `config.toml`：

```toml
[mcp_servers.twineasy]
url = "http://172.16.5.34:5108/mcp"
transport = "streamable_http"
startup_timeout_sec = 120
```

如果 Codex 版本不识别 `transport` 字段，可以删除这一行，只保留 `url`。

## OpenCode 配置

把下面配置加入 OpenCode 的 `opencode.jsonc`：

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "twineasy": {
      "type": "remote",
      "url": "http://172.16.5.34:5108/mcp",
      "enabled": true,
      "timeout": 30000
    }
  }
}
```

如果 OpenCode 和 MCP 服务不在同一台机器，确认 `172.16.5.34:5108` 能从 OpenCode 所在机器访问，并放行 Windows 防火墙端口。

## 登录流程

自动化调用默认使用用户名和密码直接登录，不传验证码，也不会发送 `X-Validate-Code-ID` 请求头。按当前后端逻辑，只有请求头包含 `X-Validate-Code-ID` 时才会校验验证码。

1. 调用 `login_twin_backend`，传入 `username` / `password`，或在 `appsettings.json` 配置默认账号密码后直接调用。
2. 调用 `get_twin_mcp_context` 确认登录状态、后端地址和当前操作域。
3. `get_login_validate_code` 是备用工具，当前默认不用；只有后端明确要求验证码时才调用。

如果传入明文密码，服务会在调用 `/v1/login` 前自动按前端规则计算 SHA-512 大写十六进制；如果传入的已经是 128 位 SHA-512，则直接原样发送。

## 查看可用工具

连接成功后，可以先调用：

```text
get_twin_mcp_context
```

该工具会返回当前 MCP 上下文、登录状态和当前版本可用的核心工具列表。

常见场景工具包括：

- `list_scenes`：查询场景列表。
- `create_scene`：创建场景。
- `get_scene`：回读场景详情。
- `update_scene`：编辑场景信息。
- `rename_scene`：重命名场景。
- `copy_scene`：复制场景。
- `delete_scene`：删除场景。
- `create_scene_hierarchy`：创建场景层级。
- `list_scene_hierarchies`：查询场景层级列表。
- `get_scene_hierarchy`：查询单个场景层级详情。
- `update_scene_hierarchy`：编辑场景层级。
- `rename_scene_hierarchy`：重命名场景层级。
- `delete_scene_hierarchy`：删除场景层级。
- `move_scene_hierarchy`：上移/下移场景层级。
- `create_scene_poi`：创建兴趣点 POI。
- `list_scene_pois`：查询兴趣点列表。
- `get_scene_poi`：回读单个兴趣点。
- `update_scene_poi`：编辑兴趣点名称或 content。
- `rename_scene_poi`：重命名兴趣点。
- `delete_scene_poi`：删除兴趣点。

## 层级接口说明

- 后端没有单独的层级详情接口，`get_scene_hierarchy` 会先调用 `GET /v1/{operationalData}/location/{locationId}/level`，再按 `levelID` 过滤。
- 后端层级编辑接口要求 `name` 必填，`update_scene_hierarchy` 会先回读当前层级，不传的字段尽量保留原值。
- 设计文档里的 `patch` 入参已落为显式字段：`name`、`scene_status`、`level_height`、`height`，更贴近后端 `LocationLevelRequest`。
- 设计文档里的 `order?` 当前不写入新增接口；后端已提供独立排序接口，使用 `move_scene_hierarchy` 做上移/下移。

## 写操作安全

写操作通常支持：

- `mode = "preview"`：只返回将要调用的后端接口和请求体，不真正写入。
- `mode = "execute"` + `confirm = true`：真正调用孪易后端写接口。

`delete_scene` 和 `delete_scene_hierarchy` 是高风险操作，除 `mode=execute` 和 `confirm=true` 外，还要求确认当前名称，避免用户只凭 ID 误删。
