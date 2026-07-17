using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using TwinEasy.McpServer.Clients;
using TwinEasy.McpServer.Models;
using TwinEasy.McpServer.Services;

namespace TwinEasy.McpServer.Tools;

/// <summary>
/// 场景类 Tool：覆盖 V1.0 的场景根目录、场景创建/回读、层级创建/查询。
/// </summary>
[McpServerToolType]
public sealed class SceneMcpTools
{
    private const string LocationAssetFolderType = "Location";
    private static readonly JsonSerializerOptions LocationSettingJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly TwinBackendClient _backendClient;
    private readonly TwinToolResultFactory _results;

    public SceneMcpTools(TwinBackendClient backendClient, TwinToolResultFactory results)
    {
        _backendClient = backendClient;
        _results = results;
    }

    /// <summary>
    /// 后端映射：GET /v1/{operationalData}/Location/assetFolder/root。
    /// 创建场景前需要 assetFolderID，本 Tool 用来获取 Location 根文件夹。
    /// </summary>
    [McpServerTool]
    [Description("获取场景 Location 的根资产文件夹。创建场景时默认自动使用这个根 folderID。")]
    public async Task<McpToolResult> get_location_asset_folder_root(
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var root = await GetLocationAssetFolderRootAsync(op);
            var folderId = ExtractString(root, "folderID");

            return _results.Success("已获取 Location 根资产文件夹。", new
            {
                operational_data = op,
                asset_folder_type = LocationAssetFolderType,
                backend_api = "GET /v1/{operationalData}/Location/assetFolder/root",
                asset_folder_id = folderId,
                root
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("获取 Location 根资产文件夹失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：
    /// - GET /v1/{operationalData}/assetFolders/{assetFolderID}/location
    /// - POST /v1/{operationalData}/assetFolders/{assetFolderID}/location
    /// 如果没有传 asset_folder_id，会先调用 Location 根文件夹接口取 folderID。
    /// </summary>
    [McpServerTool]
    [Description("查询场景列表。asset_folder_id 不传时，自动使用 Location 根资产文件夹。")]
    public async Task<McpToolResult> list_scenes(
        [Description("场景所属资产文件夹 ID；不传时自动取 /v1/{operationalData}/Location/assetFolder/root 的 folderID。")] string? asset_folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("搜索关键字。")] string? keyword = null,
        [Description("页码；不传则查询全部。")] int? page = null,
        [Description("每页数量；不传则查询全部。")] int? page_size = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var resolvedFolderId = await ResolveLocationAssetFolderIdAsync(op, asset_folder_id);
            var path = $"/v1/{Uri.EscapeDataString(op)}/assetFolders/{Uri.EscapeDataString(resolvedFolderId)}/location";

            object? data;
            if (page is null && page_size is null && string.IsNullOrWhiteSpace(keyword))
            {
                data = await _backendClient.GetAsync(path, CancellationToken.None);
            }
            else
            {
                data = await _backendClient.PostAsync(path, new
                {
                    pageIndex = page ?? 1,
                    pageSize = page_size ?? 20,
                    isSearchAll = page is null && page_size is null,
                    searchBy = keyword,
                    orderBy = "createAt",
                    ascending = false
                }, CancellationToken.None);
            }

            return _results.Success("已查询场景列表。", new
            {
                operational_data = op,
                asset_folder_id = resolvedFolderId,
                asset_folder_id_source = string.IsNullOrWhiteSpace(asset_folder_id) ? "LocationRoot" : "Input",
                backend_api = "GET/POST /v1/{operationalData}/assetFolders/{assetFolderID}/location",
                scenes = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询场景列表失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：POST /v1/{operationalData}/assetFolders/{assetFolderID}/location/add。
    /// 创建场景时不要求用户提供 assetFolderID，默认用 Location 根文件夹 folderID。
    /// 如果调用方没有传 location_setting，则自动补孪易前端手工创建场景时使用的基础配置壳。
    /// 支持 preview/execute 两种模式：preview 只返回拟调用接口和请求体，execute 才真正写入后端。
    /// </summary>
    [McpServerTool]
    [Description("创建场景。asset_folder_id 不传时，自动使用 Location 根资产文件夹；preview 模式只返回拟写入内容。")]
    public async Task<McpToolResult> create_scene(
        [Description("场景名称。")] string name,
        [Description("场景所属资产文件夹 ID；不传时自动取 /v1/{operationalData}/Location/assetFolder/root 的 folderID。")] string? asset_folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("场景图标路径，可选。")] string? icon_path = null,
        [Description("场景设置 JSON 字符串，可选。")] string? location_setting = null,
        [Description("执行模式：preview 或 execute。")] string mode = "execute",
        [Description("execute 写入时必须为 true。")] bool confirm = true)
    {
        try
        {
            // 解析操作域和 Location 根目录，避免调用方每次都手工传 assetFolderID。
            var op = _backendClient.ResolveOperationalData(operational_data);
            var resolvedFolderId = await ResolveLocationAssetFolderIdAsync(op, asset_folder_id);

            // 组装新增场景请求体；locationSetting 不传时按手工创建场景的默认壳补齐。
            var resolvedLocationSetting = ResolveCreateSceneLocationSetting(location_setting);
            var body = new
            {
                name,
                iconPath = icon_path ?? string.Empty,
                locationSetting = resolvedLocationSetting
            };

            if (IsPreview(mode))
            {
                // 预览模式只展示将要写入的内容，不调用后端写接口。
                return _results.Preview("预览：将创建场景。", new
                {
                    backend_api = "POST /v1/{operationalData}/assetFolders/{assetFolderID}/location/add",
                    operational_data = op,
                    asset_folder_id = resolvedFolderId,
                    asset_folder_id_source = string.IsNullOrWhiteSpace(asset_folder_id) ? "LocationRoot" : "Input",
                    location_setting_source = string.IsNullOrWhiteSpace(location_setting) ? "DefaultManualCreateShell" : "Input",
                    body
                });
            }

            if (!confirm)
            {
                // 写操作必须显式确认，防止 Agent 误创建场景。
                return _results.Failed("创建场景属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/assetFolders/{Uri.EscapeDataString(resolvedFolderId)}/location/add";
            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已创建场景。",
                new
                {
                    operational_data = op,
                    asset_folder_id = resolvedFolderId,
                    asset_folder_id_source = string.IsNullOrWhiteSpace(asset_folder_id) ? "LocationRoot" : "Input",
                    location_setting_source = string.IsNullOrWhiteSpace(location_setting) ? "DefaultManualCreateShell" : "Input",
                    scene = data
                },
                affectedObjects: new object[] { new { object_type = "scene", name, data } },
                nextActions: new[] { "调用 create_scene_hierarchy 为场景创建层级。", "调用 get_scene 回读场景详情。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("创建场景失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：GET /v1/{operationalData}/location/{locationId}。
    /// </summary>
    [McpServerTool]
    [Description("根据场景 ID 回读场景详情。")]
    public async Task<McpToolResult> get_scene(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}";
            var data = await _backendClient.GetAsync(path, CancellationToken.None);

            return _results.Success("已回读场景详情。", new
            {
                operational_data = op,
                scene_id,
                backend_api = "GET /v1/{operationalData}/location/{locationId}",
                scene = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("回读场景详情失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 编辑场景/地点基础信息。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/edit。
    /// 后端请求体仍是 AddLocationRequest，所以这里先读取现有场景，再合并 name、iconPath 和 locationSetting，避免只改一个字段时覆盖掉已有配置。
    /// </summary>
    [McpServerTool]
    [Description("编辑场景信息和场景设置。可补 scene_server、scene_status、经纬度等让空壳场景具备可呈现地址。")]
    public async Task<McpToolResult> update_scene(
        [Description("要编辑的场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("新的场景名称；不传则保留原名称。")] string? name = null,
        [Description("新的场景图标路径；不传则保留原图标路径。")] string? icon_path = null,
        [Description("完整场景设置 JSON 字符串；传入后会整体替换 locationSetting。")] string? location_setting = null,
        [Description("场景设置局部补丁 JSON；用于补充任意 locationSetting 字段。")] string? location_setting_patch = null,
        [Description("场景服务地址，对应 locationSetting.sceneServer。")] string? scene_server = null,
        [Description("场景状态，对应 locationSetting.sceneStatus。")] string? scene_status = null,
        [Description("经度，对应 locationSetting.longitude。")] string? longitude = null,
        [Description("纬度，对应 locationSetting.latitude。")] string? latitude = null,
        [Description("选中建筑，对应 locationSetting.selectBuilding。")] string? select_building = null,
        [Description("场景渲染类别，对应 locationSetting.sceneCategory；不传则保留原值。")] string? scene_category = null,
        [Description("场景地点类别，对应 locationSetting.locationCategory；不传则保留原值。")] string? location_category = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentScenePath = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}";
            var currentScene = await _backendClient.GetAsync(currentScenePath, CancellationToken.None);

            // 先读后写：保留未显式修改的 name、iconPath 和 locationSetting 字段。
            var resolvedName = name ?? ExtractString(currentScene, "name") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                throw new InvalidOperationException("场景名称为空，无法调用后端编辑接口。请传入 name。");
            }

            var resolvedIconPath = icon_path ?? ExtractString(currentScene, "iconPath") ?? string.Empty;
            var resolvedLocationSetting = ResolveUpdateSceneLocationSetting(
                currentScene,
                location_setting,
                location_setting_patch,
                scene_server,
                scene_status,
                longitude,
                latitude,
                select_building,
                scene_category,
                location_category);

            var body = new
            {
                name = resolvedName,
                iconPath = resolvedIconPath,
                locationSetting = resolvedLocationSetting
            };

            var editPath = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/edit";
            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑场景信息。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/edit",
                    operational_data = op,
                    scene_id,
                    location_setting_source = string.IsNullOrWhiteSpace(location_setting) ? "MergedFromCurrentScene" : "InputFullReplace",
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("编辑场景属于写操作，请确认拟写入内容后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(editPath, body, CancellationToken.None);

            return _results.Success(
                "已编辑场景信息。",
                new
                {
                    operational_data = op,
                    scene_id,
                    scene = data
                },
                affectedObjects: new object[] { new { object_type = "scene", scene_id, action = "update", data } },
                nextActions: new[] { "调用 get_scene 回读场景详情，确认 sceneServer、状态和经纬度已写入。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑场景信息失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 重命名场景/地点。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/rename。
    /// 该接口只修改名称，不读取或写入 locationSetting，适合纯重命名场景。
    /// </summary>
    [McpServerTool]
    [Description("重命名场景/地点。只修改名称，不影响 locationSetting。默认 preview，execute 需要 confirm=true。")]
    public async Task<McpToolResult> rename_scene(
        [Description("要重命名的场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("新的场景名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return _results.Failed("新场景名称不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/rename";
            var body = new { name = name.Trim() };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将重命名场景。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/rename",
                    operational_data = op,
                    scene_id,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("重命名场景属于写操作，请确认名称后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已重命名场景。",
                new
                {
                    operational_data = op,
                    scene_id,
                    scene = data
                },
                affectedObjects: new object[] { new { object_type = "scene", scene_id, action = "rename", name = name.Trim(), data } },
                nextActions: new[] { "调用 get_scene 回读场景详情，确认名称已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("重命名场景失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 删除一个场景/地点。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/delete。
    /// 删除属于高风险操作：执行前会先读取场景名称，execute 时必须确认名称完全一致。
    /// </summary>
    [McpServerTool]
    [Description("删除场景/地点。默认 preview；真正删除必须 mode=execute、confirm=true，并确认当前场景名称。")]
    public async Task<McpToolResult> delete_scene(
        [Description("要删除的场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。删除默认 preview，避免误删。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false,
        [Description("删除二次确认名称；execute 删除时必须传入当前场景名称。")] string? confirm_scene_name = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentScenePath = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}";
            var currentScene = await _backendClient.GetAsync(currentScenePath, CancellationToken.None);
            var sceneName = ExtractString(currentScene, "name") ?? string.Empty;
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/delete";

            if (IsPreview(mode))
            {
                // 预览阶段先回读场景名称，让用户按名称确认，避免误删 ID 相近的场景。
                return _results.Preview("预览：将删除场景。请确认场景名称后再执行删除。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/delete",
                    operational_data = op,
                    scene_id,
                    scene_name = sceneName,
                    confirm_required = true,
                    confirm_scene_name_required = sceneName,
                    path
                });
            }

            if (!confirm)
            {
                // 删除场景不可逆，必须显式确认当前场景名称。
                return _results.Failed("删除场景属于高风险操作，请先确认目标场景后使用 mode=execute、confirm=true，并传入 confirm_scene_name。");
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return _results.Failed("删除场景失败：无法读取当前场景名称，不能进行名称确认删除。");
            }

            if (!string.Equals(sceneName, confirm_scene_name?.Trim(), StringComparison.Ordinal))
            {
                return _results.Failed($"删除场景二次确认失败：confirm_scene_name 必须和当前场景名称完全一致。当前场景名称：{sceneName}");
            }

            var data = await _backendClient.PostAsync(path, body: null, CancellationToken.None);

            return _results.Success(
                "已删除场景。",
                new
                {
                    operational_data = op,
                    scene_id,
                    scene_name = sceneName,
                    delete_result = data
                },
                affectedObjects: new object[] { new { object_type = "scene", scene_id, scene_name = sceneName, action = "delete", data } });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除场景失败。", new[] { ex.Message });
        }
    }
    /// <summary>
    /// 复制一个场景/地点。
    /// 不传 target_folder_id 时，调用 POST /v1/location/{locationId}/copy 在当前账号内复制场景。
    /// 传入 target_folder_id 时，调用 POST /v1/copyIndustryData/location/{locationId}/folder/{folderId}，用于把行业/模板场景导入到目标文件夹。
    /// </summary>
    [McpServerTool]
    [Description("复制场景/地点。不传 target_folder_id 时当前账号内复制；传入时按行业/模板场景导入到目标文件夹。")]
    public async Task<McpToolResult> copy_scene(
        [Description("源场景/地点 ID，对应后端 locationId。")] string source_scene_id,
        [Description("目标文件夹 ID；不传时走当前账号内复制，传入时走行业/模板场景导入。")] string? target_folder_id = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 复制时必须为 true。")] bool confirm = false)
    {
        try
        {
            var isIndustryImport = !string.IsNullOrWhiteSpace(target_folder_id);
            var path = isIndustryImport
                ? $"/v1/copyIndustryData/location/{Uri.EscapeDataString(source_scene_id)}/folder/{Uri.EscapeDataString(target_folder_id!.Trim())}"
                : $"/v1/location/{Uri.EscapeDataString(source_scene_id)}/copy";
            var backendApi = isIndustryImport
                ? "POST /v1/copyIndustryData/location/{locationId}/folder/{folderId}"
                : "POST /v1/location/{locationId}/copy";

            if (IsPreview(mode))
            {
                // 复制也是写操作，预览模式先让用户确认源场景和目标文件夹。
                return _results.Preview("预览：将复制场景。", new
                {
                    backend_api = backendApi,
                    copy_mode = isIndustryImport ? "IndustryDataToFolder" : "SameAccount",
                    source_scene_id,
                    target_folder_id = string.IsNullOrWhiteSpace(target_folder_id) ? null : target_folder_id.Trim(),
                    confirm_required = true,
                    path
                });
            }

            if (!confirm)
            {
                return _results.Failed("复制场景属于写操作，请确认源场景和目标文件夹后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body: null, CancellationToken.None);

            return _results.Success(
                "已复制场景。",
                new
                {
                    backend_api = backendApi,
                    copy_mode = isIndustryImport ? "IndustryDataToFolder" : "SameAccount",
                    source_scene_id,
                    target_folder_id = string.IsNullOrWhiteSpace(target_folder_id) ? null : target_folder_id.Trim(),
                    copy_result = data
                },
                affectedObjects: new object[] { new { object_type = "scene", source_scene_id, action = "copy", data } },
                nextActions: new[] { "调用 list_scenes 回读场景列表，确认复制后的场景是否出现。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("复制场景失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/add。
    /// 后端 body 是数组；V1.0 先封装单个层级，后续可扩展批量创建。
    /// </summary>
    [McpServerTool]
    [Description("创建场景层级。后端接口一次接收数组；当前 Tool 先支持单层级写入。")]
    public async Task<McpToolResult> create_scene_hierarchy(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("场景状态，可选。")] string? scene_status = null,
        [Description("层级地面高度，可选。")] double? level_height = null,
        [Description("本层级高度，可选。")] double? height = null,
        [Description("执行模式：preview 或 execute。")] string mode = "execute",
        [Description("execute 写入时必须为 true。")] bool confirm = true)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var body = new[]
            {
                new
                {
                    name,
                    sceneStatus = scene_status,
                    levelHeight = level_height,
                    height
                }
            };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将创建场景层级。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/level/add",
                    operational_data = op,
                    scene_id,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("创建场景层级属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/level/add";
            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已创建场景层级。",
                new
                {
                    operational_data = op,
                    scene_id,
                    hierarchies = data
                },
                affectedObjects: new object[] { new { object_type = "scene_hierarchy", name, data } },
                nextActions: new[] { "调用 list_scene_hierarchies 回读层级列表。", "调用 create_scene_poi 在层级上创建兴趣点。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("创建场景层级失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 编辑场景层级。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit。
    /// 后端请求体使用 LocationLevelRequest：name、sceneStatus、levelHeight、height。
    /// </summary>
    [McpServerTool]
    [Description("编辑场景层级名称、状态、地面高度和本层级高度。默认 preview，execute 需要 confirm=true。")]
    public async Task<McpToolResult> update_scene_hierarchy(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("层级名称；不传时保留当前名称。")] string? name = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("场景状态，可选。")] string? scene_status = null,
        [Description("层级地面高度，可选。")] double? level_height = null,
        [Description("本层级高度，可选。")] double? height = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentHierarchy = await GetSceneHierarchyRawAsync(op, scene_id, hierarchy_id);
            if (currentHierarchy is null)
            {
                return _results.Failed("编辑层级失败：未在当前场景层级列表中找到指定 hierarchy_id。");
            }

            // edit 接口的 name 是必填字段；调用方不传时从当前层级读取，避免局部更新失败。
            var resolvedName = name ?? ExtractString(currentHierarchy.Value, "levelName") ?? ExtractString(currentHierarchy.Value, "name");
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                return _results.Failed("编辑层级失败：后端要求 name 必填，但当前层级未返回 levelName/name，请显式传入 name。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/level/{Uri.EscapeDataString(hierarchy_id)}/edit";
            var body = new Dictionary<string, object?>
            {
                ["name"] = resolvedName.Trim()
            };
            AddIfNotNull(body, "sceneStatus", scene_status ?? ExtractString(currentHierarchy.Value, "sceneStatus"));
            AddIfNotNull(body, "levelHeight", level_height ?? ExtractDouble(currentHierarchy.Value, "levelHeight"));
            AddIfNotNull(body, "height", height ?? ExtractDouble(currentHierarchy.Value, "height"));

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑场景层级。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("编辑场景层级属于写操作，请确认拟写入内容后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已编辑场景层级。",
                new
                {
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    hierarchy = data
                },
                affectedObjects: new object[] { new { object_type = "scene_hierarchy", scene_id, hierarchy_id, action = "update", data } },
                nextActions: new[] { "调用 list_scene_hierarchies 回读层级列表，确认层级信息已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑场景层级失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 查询单个场景层级详情。
    /// 后端没有单独详情接口，这里通过 GET /v1/{operationalData}/location/{locationId}/level 后按 levelID 过滤。
    /// </summary>
    [McpServerTool]
    [Description("查询单个场景层级详情。后端无单独详情接口，本 Tool 从层级列表中按 levelID 过滤。")]
    public async Task<McpToolResult> get_scene_hierarchy(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var hierarchy = await GetSceneHierarchyRawAsync(op, scene_id, hierarchy_id);
            if (hierarchy is null)
            {
                return _results.Failed("查询层级详情失败：未在当前场景层级列表中找到指定 hierarchy_id。");
            }

            return _results.Success("已查询场景层级详情。", new
            {
                operational_data = op,
                scene_id,
                hierarchy_id,
                backend_api = "GET /v1/{operationalData}/location/{locationId}/level 后由 MCP 按 levelID 过滤",
                hierarchy = hierarchy.Value
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询层级详情失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 重命名场景层级。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit。
    /// 该接口只有 edit，没有独立 rename；这里先回读当前层级，再只替换 name。
    /// </summary>
    [McpServerTool]
    [Description("重命名场景层级。复用层级 edit 接口，只修改名称并保留当前状态/高度。默认 preview。")]
    public async Task<McpToolResult> rename_scene_hierarchy(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("新的层级名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return _results.Failed("重命名层级失败：name 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentHierarchy = await GetSceneHierarchyRawAsync(op, scene_id, hierarchy_id);
            if (currentHierarchy is null)
            {
                return _results.Failed("重命名层级失败：未在当前场景层级列表中找到指定 hierarchy_id。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/level/{Uri.EscapeDataString(hierarchy_id)}/edit";
            var body = new Dictionary<string, object?>
            {
                ["name"] = name.Trim()
            };
            AddIfNotNull(body, "sceneStatus", ExtractString(currentHierarchy.Value, "sceneStatus"));
            AddIfNotNull(body, "levelHeight", ExtractDouble(currentHierarchy.Value, "levelHeight"));
            AddIfNotNull(body, "height", ExtractDouble(currentHierarchy.Value, "height"));

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将重命名场景层级。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/level/{levelId}/edit",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    old_name = ExtractString(currentHierarchy.Value, "levelName") ?? ExtractString(currentHierarchy.Value, "name"),
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("重命名场景层级属于写操作，请确认新名称后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已重命名场景层级。",
                new { operational_data = op, scene_id, hierarchy_id, hierarchy = data },
                affectedObjects: new object[] { new { object_type = "scene_hierarchy", scene_id, hierarchy_id, action = "rename", name = name.Trim(), data } },
                nextActions: new[] { "调用 list_scene_hierarchies 回读层级列表，确认名称已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("重命名场景层级失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 删除一个场景层级。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/delete，body 为层级 ID 数组。
    /// 删除属于高风险操作：execute 时需要确认当前层级名称。
    /// </summary>
    [McpServerTool]
    [Description("删除场景层级。默认 preview；真正删除必须 mode=execute、confirm=true，并确认当前层级名称。")]
    public async Task<McpToolResult> delete_scene_hierarchy(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。删除默认 preview，避免误删。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false,
        [Description("删除二次确认名称；execute 删除时必须传入当前层级名称。")] string? confirm_hierarchy_name = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentHierarchy = await GetSceneHierarchyRawAsync(op, scene_id, hierarchy_id);
            if (currentHierarchy is null)
            {
                return _results.Failed("删除层级失败：未在当前场景层级列表中找到指定 hierarchy_id。");
            }

            var hierarchyName = ExtractString(currentHierarchy.Value, "levelName") ?? ExtractString(currentHierarchy.Value, "name") ?? string.Empty;
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/level/delete";
            var body = new[] { hierarchy_id };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将删除场景层级。请确认层级名称后再执行删除。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/level/delete",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    hierarchy_name = hierarchyName,
                    confirm_required = true,
                    confirm_hierarchy_name_required = hierarchyName,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("删除场景层级属于高风险操作，请先确认目标层级后使用 mode=execute、confirm=true，并传入 confirm_hierarchy_name。");
            }

            if (string.IsNullOrWhiteSpace(hierarchyName))
            {
                return _results.Failed("删除层级失败：无法读取当前层级名称，不能进行名称确认删除。");
            }

            if (!string.Equals(hierarchyName, confirm_hierarchy_name?.Trim(), StringComparison.Ordinal))
            {
                return _results.Failed($"删除层级二次确认失败：confirm_hierarchy_name 必须和当前层级名称完全一致。当前层级名称：{hierarchyName}");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已删除场景层级。",
                new { operational_data = op, scene_id, hierarchy_id, hierarchy_name = hierarchyName, delete_result = data },
                affectedObjects: new object[] { new { object_type = "scene_hierarchy", scene_id, hierarchy_id, hierarchy_name = hierarchyName, action = "delete", data } });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除场景层级失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 上移或下移场景层级。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/{levelId}/{sortType}。
    /// sortType 后端枚举为 Upward / Downward。
    /// </summary>
    [McpServerTool]
    [Description("调整场景层级顺序。sort_type 支持 Upward/Downward，也兼容 up/down。默认 preview。")]
    public async Task<McpToolResult> move_scene_hierarchy(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("排序方向：Upward/Downward，或 up/down。")] string sort_type,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var normalizedSortType = NormalizeHierarchySortType(sort_type);
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/level/{Uri.EscapeDataString(hierarchy_id)}/{normalizedSortType}";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将调整场景层级顺序。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/level/{levelId}/{sortType}",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    sort_type = normalizedSortType,
                    path
                });
            }

            if (!confirm)
            {
                return _results.Failed("调整场景层级顺序属于写操作，请确认方向后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body: null, CancellationToken.None);
            return _results.Success(
                "已调整场景层级顺序。",
                new { operational_data = op, scene_id, hierarchy_id, sort_type = normalizedSortType, move_result = data },
                affectedObjects: new object[] { new { object_type = "scene_hierarchy", scene_id, hierarchy_id, action = "move", sort_type = normalizedSortType, data } },
                nextActions: new[] { "调用 list_scene_hierarchies 回读层级列表，确认排序已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("调整场景层级顺序失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：GET /v1/{operationalData}/location/{locationId}/level。
    /// </summary>
    [McpServerTool]
    [Description("查询场景下已有层级列表。")]
    public async Task<McpToolResult> list_scene_hierarchies(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/level";
            var data = await _backendClient.GetAsync(path, CancellationToken.None);

            return _results.Success("已查询场景层级列表。", new
            {
                operational_data = op,
                scene_id,
                backend_api = "GET /v1/{operationalData}/location/{locationId}/level",
                hierarchies = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询场景层级列表失败。", new[] { ex.Message });
        }
    }

    private async Task<string> ResolveLocationAssetFolderIdAsync(string operationalData, string? assetFolderId)
    {
        if (!string.IsNullOrWhiteSpace(assetFolderId))
        {
            return assetFolderId.Trim();
        }

        var root = await GetLocationAssetFolderRootAsync(operationalData);
        var rootFolderId = ExtractString(root, "folderID");
        if (string.IsNullOrWhiteSpace(rootFolderId))
        {
            throw new InvalidOperationException("Location 根资产文件夹接口未返回 folderID，无法创建或查询场景。");
        }

        return rootFolderId;
    }

    private async Task<JsonElement> GetLocationAssetFolderRootAsync(string operationalData)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/{LocationAssetFolderType}/assetFolder/root";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    private async Task<JsonElement?> GetSceneHierarchyRawAsync(string operationalData, string sceneId, string hierarchyId)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/location/{Uri.EscapeDataString(sceneId)}/level";
        var data = await _backendClient.GetAsync(path, CancellationToken.None);
        return FindHierarchyById(data, hierarchyId);
    }

    private static JsonElement? FindHierarchyById(JsonElement element, string hierarchyId)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = FindHierarchyById(item, hierarchyId);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var currentId =
            ExtractString(element, "levelID") ??
            ExtractString(element, "levelId") ??
            ExtractString(element, "hierarchy_id") ??
            ExtractString(element, "id");

        if (string.Equals(currentId, hierarchyId, StringComparison.Ordinal))
        {
            return element.Clone();
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
            {
                var found = FindHierarchyById(property.Value, hierarchyId);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static string? ExtractString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static double? ExtractDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return double.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static void AddIfNotNull(Dictionary<string, object?> target, string propertyName, object? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    private static bool IsPreview(string? mode)
    {
        return string.Equals(mode, "preview", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHierarchySortType(string sortType)
    {
        return sortType.Trim().ToLowerInvariant() switch
        {
            "up" or "upward" or "上移" => "Upward",
            "down" or "downward" or "下移" => "Downward",
            _ => throw new InvalidOperationException("sort_type 只支持 Upward/Downward，或 up/down。")
        };
    }

    private static string ResolveUpdateSceneLocationSetting(
        JsonElement currentScene,
        string? locationSetting,
        string? locationSettingPatch,
        string? sceneServer,
        string? sceneStatus,
        string? longitude,
        string? latitude,
        string? selectBuilding,
        string? sceneCategory,
        string? locationCategory)
    {
        if (!string.IsNullOrWhiteSpace(locationSetting))
        {
            // 调用方传完整 locationSetting 时，按显式配置整体替换。
            return locationSetting.Trim();
        }

        var currentLocationSetting = ExtractString(currentScene, "locationSetting");
        var setting = ParseLocationSettingOrDefault(currentLocationSetting);

        // 先合并通用 JSON 补丁，再合并显式参数；显式参数优先级更高。
        MergeLocationSettingPatch(setting, locationSettingPatch);
        SetJsonString(setting, "sceneServer", sceneServer);
        SetJsonString(setting, "sceneStatus", sceneStatus);
        SetJsonString(setting, "longitude", longitude);
        SetJsonString(setting, "latitude", latitude);
        SetJsonString(setting, "selectBuilding", selectBuilding);
        SetJsonString(setting, "sceneCategory", sceneCategory);
        SetJsonString(setting, "locationCategory", locationCategory);

        return setting.ToJsonString(LocationSettingJsonOptions);
    }

    private static JsonObject ParseLocationSettingOrDefault(string? locationSetting)
    {
        if (!string.IsNullOrWhiteSpace(locationSetting))
        {
            try
            {
                if (JsonNode.Parse(locationSetting) is JsonObject parsed)
                {
                    return parsed;
                }
            }
            catch (JsonException)
            {
                // 历史数据如果不是合法 JSON，就回退到默认壳，避免更新接口写入失败。
            }
        }

        return JsonNode.Parse(ResolveCreateSceneLocationSetting(null))!.AsObject();
    }

    private static void MergeLocationSettingPatch(JsonObject target, string? patchJson)
    {
        if (string.IsNullOrWhiteSpace(patchJson))
        {
            return;
        }

        if (JsonNode.Parse(patchJson) is not JsonObject patch)
        {
            throw new InvalidOperationException("location_setting_patch 必须是 JSON 对象。");
        }

        foreach (var item in patch)
        {
            target[item.Key] = item.Value?.DeepClone();
        }
    }

    private static void SetJsonString(JsonObject target, string propertyName, string? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    /// <summary>
    /// 解析创建场景时要写入后端的 locationSetting。
    /// 调用方显式传入配置时原样使用；未传时自动生成一份与孪易前端手工创建场景一致的默认配置壳。
    /// 这个默认壳只负责让场景具备基础配置结构，不补真实服务地址、场景状态、经纬度或建筑选择。
    /// </summary>
    /// <param name="locationSetting">调用方传入的场景配置 JSON 字符串，可为空。</param>
    /// <returns>最终写入 AddLocationRequest.locationSetting 的 JSON 字符串。</returns>
    private static string ResolveCreateSceneLocationSetting(string? locationSetting)
    {
        if (!string.IsNullOrWhiteSpace(locationSetting))
        {
            // 用户显式传入 locationSetting 时优先使用用户配置，避免覆盖业务侧已有判断。
            return locationSetting.Trim();
        }

        // 模拟孪易前端“只输入名称创建场景”时写入的默认配置壳。
        // 服务地址、场景状态、经纬度和建筑选择等真实业务字段，后续在场景设置里再补。
        return JsonSerializer.Serialize(new
        {
            // 适配方式：手工创建默认是单设备适配。
            adapterType = "单设备适配",

            // 场景渲染类别：标准版默认按服务端渲染场景处理。
            sceneCategory = "ServerSideRendering",

            // 地点类别：普通场景地点。
            locationCategory = "SceneLocation",

            // 场景基础显示参数：默认不缩放、不透明。
            scale = 1.0,
            alpha = 1.0,

            // Dock 面板默认停靠到右下角。
            dockHorizontal = "right",
            dockVertical = "bottom",

            // 父场景和真实服务配置先留空，保持与手工创建的基础壳一致。
            parentSceneCategory = string.Empty,
            sceneServer = string.Empty,
            sceneStatus = string.Empty,

            // 经纬度和建筑选择先留空，由后续场景设置/地图配置补齐。
            longitude = string.Empty,
            latitude = string.Empty,
            selectBuilding = string.Empty
        }, LocationSettingJsonOptions);
    }
}
