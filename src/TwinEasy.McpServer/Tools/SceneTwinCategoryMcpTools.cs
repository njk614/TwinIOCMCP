using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using TwinEasy.McpServer.Clients;
using TwinEasy.McpServer.Models;
using TwinEasy.McpServer.Services;

namespace TwinEasy.McpServer.Tools;

/// <summary>
/// Scene-level twin category configuration tools.
/// These tools operate on locationId + twinCategoryConfigID, not tenant-level twinCategoryID alone.
/// </summary>
[McpServerToolType]
public sealed class SceneTwinCategoryMcpTools
{
    private const string LocationTwinCategoryFolderType = "TwinCategory";

    private static readonly object SceneCategoryScope = new
    {
        scope = "scene_twin_category_config",
        id_field = "twinCategoryConfigID",
        requires = new[] { "locationId" },
        tenant_source_id_field = "twinCategoryID",
        note = "场景级类别配置是租户类别加入场景后的结果；创建、删除、更新场景实例前应先查询本层，拿到 twinCategoryConfigID。"
    };

    private readonly TwinBackendClient _backendClient;
    private readonly TwinToolResultFactory _results;

    public SceneTwinCategoryMcpTools(TwinBackendClient backendClient, TwinToolResultFactory results)
    {
        _backendClient = backendClient;
        _results = results;
    }

    [McpServerTool]
    [Description("查询场景级孪生体类别文件夹。parent_folder_id 不传时先取场景 TwinCategory 根文件夹，再用根 folderID 查询根下子文件夹。")]
    public async Task<McpToolResult> list_scene_twin_category_folders(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("父文件夹 ID；不传则查询场景 TwinCategory 根分类下的子文件夹。")] string? parent_folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var op = _backendClient.ResolveOperationalData(operational_data);

            if (string.IsNullOrWhiteSpace(parent_folder_id))
            {
                var root = await GetSceneTwinCategoryRootFolderAsync(op, normalizedSceneId);
                var rootFolderId = ExtractFolderId(root);
                if (string.IsNullOrWhiteSpace(rootFolderId))
                {
                    return _results.Failed("查询场景孪生体类别文件夹失败：场景 TwinCategory 根文件夹接口未返回 folderID。", data: new
                    {
                        operational_data = op,
                        resource_scope = SceneCategoryScope,
                        scene_id = normalizedSceneId,
                        root
                    });
                }

                var childFolders = await GetSceneTwinCategoryChildFoldersAsync(op, normalizedSceneId, rootFolderId);
                return _results.Success("已读取场景 TwinCategory 根分类下的孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    location_folder_type = LocationTwinCategoryFolderType,
                    parent_folder_id = rootFolderId,
                    parent_folder_id_source = "SceneTwinCategoryRoot.folderID",
                    backend_api = new[]
                    {
                        "GET /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/root",
                        "GET /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{parentFolderId}"
                    },
                    root_folder = root,
                    folders = childFolders
                });
            }

            var normalizedParentId = parent_folder_id.Trim();
            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/{LocationTwinCategoryFolderType}/assetFolder/{Uri.EscapeDataString(normalizedParentId)}";
            var data = await _backendClient.GetAsync(path, CancellationToken.None);

            return _results.Success("已读取场景孪生体类别子文件夹列表。", new
            {
                operational_data = op,
                resource_scope = SceneCategoryScope,
                scene_id = normalizedSceneId,
                location_folder_type = LocationTwinCategoryFolderType,
                parent_folder_id = normalizedParentId,
                backend_api = "GET /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{parentFolderId}",
                folders = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询场景孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("创建场景级孪生体类别文件夹。parent_folder_id 不传时自动创建在该场景 TwinCategory 根文件夹下。")]
    public async Task<McpToolResult> create_scene_twin_category_folder(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("文件夹名称。")] string name,
        [Description("父文件夹 ID；不传时自动使用场景 TwinCategory 根文件夹。")] string? parent_folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedName = NormalizeRequired(name, "name");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var resolvedParentId = await ResolveSceneTwinCategoryFolderIdAsync(op, normalizedSceneId, parent_folder_id);
            var body = new Dictionary<string, object?> { ["name"] = normalizedName };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将创建场景孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    location_folder_type = LocationTwinCategoryFolderType,
                    parent_folder_id = resolvedParentId,
                    parent_folder_id_source = string.IsNullOrWhiteSpace(parent_folder_id) ? "SceneTwinCategoryRoot" : "Input",
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{parentFolderId}/add",
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("创建场景孪生体类别文件夹属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/{LocationTwinCategoryFolderType}/assetFolder/{Uri.EscapeDataString(resolvedParentId)}/add";
            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已创建场景孪生体类别文件夹。",
                new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    parent_folder_id = resolvedParentId,
                    folder = data
                },
                affectedObjects: new object[] { new { object_type = "scene_twin_category_folder", scene_id = normalizedSceneId, name = normalizedName, data } },
                nextActions: new[] { "调用 add_scene_twin_category 将租户级类别加入该场景文件夹。", "调用 list_scene_twin_categories 回读文件夹下的场景类别配置。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("创建场景孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("编辑/重命名场景级孪生体类别文件夹。只修改文件夹名称，不移动文件夹。")]
    public async Task<McpToolResult> update_scene_twin_category_folder(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("要编辑的场景孪生体类别文件夹 ID。")] string folder_id,
        [Description("新的文件夹名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedFolderId = NormalizeRequired(folder_id, "folder_id");
            var normalizedName = NormalizeRequired(name, "name");
            var op = _backendClient.ResolveOperationalData(operational_data);
            await EnsureNotSceneTwinCategoryRootFolderAsync(op, normalizedSceneId, normalizedFolderId, "编辑");

            var body = new Dictionary<string, object?> { ["name"] = normalizedName };
            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/{LocationTwinCategoryFolderType}/assetFolder/{Uri.EscapeDataString(normalizedFolderId)}/edit";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑场景孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    location_folder_type = LocationTwinCategoryFolderType,
                    folder_id = normalizedFolderId,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{folderId}/edit",
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("编辑场景孪生体类别文件夹属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已编辑场景孪生体类别文件夹。",
                new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    folder_id = normalizedFolderId,
                    folder = data
                },
                affectedObjects: new object[] { new { object_type = "scene_twin_category_folder", scene_id = normalizedSceneId, folder_id = normalizedFolderId, name = normalizedName, data } },
                nextActions: new[] { "调用 list_scene_twin_category_folders 回读文件夹列表。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑场景孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("重命名场景级孪生体类别文件夹。等价于 update_scene_twin_category_folder，只修改文件夹名称。")]
    public async Task<McpToolResult> rename_scene_twin_category_folder(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("要重命名的场景孪生体类别文件夹 ID。")] string folder_id,
        [Description("新的文件夹名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await update_scene_twin_category_folder(scene_id, folder_id, name, operational_data, mode, confirm);
    }

    [McpServerTool]
    [Description("删除场景级孪生体类别文件夹。会调用 LocationFolder 的 TwinCategory 文件夹删除接口。")]
    public async Task<McpToolResult> delete_scene_twin_category_folder(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("要删除的场景孪生体类别文件夹 ID。")] string folder_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedFolderId = NormalizeRequired(folder_id, "folder_id");
            var op = _backendClient.ResolveOperationalData(operational_data);
            await EnsureNotSceneTwinCategoryRootFolderAsync(op, normalizedSceneId, normalizedFolderId, "删除");

            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/{LocationTwinCategoryFolderType}/assetFolder/{Uri.EscapeDataString(normalizedFolderId)}/delete";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将删除场景孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    location_folder_type = LocationTwinCategoryFolderType,
                    folder_id = normalizedFolderId,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/TwinCategory/assetFolder/{folderId}/delete",
                    warning = "删除文件夹可能受后端规则限制；若文件夹下仍有子文件夹或场景类别配置，后端可能拒绝删除。"
                },
                warnings: new[] { "删除操作不可自动回滚，请先确认该文件夹下没有仍需保留的类别配置或子文件夹。" });
            }

            if (!confirm)
            {
                return _results.Failed("删除场景孪生体类别文件夹属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body: null, CancellationToken.None);
            return _results.Success(
                "已删除场景孪生体类别文件夹。",
                new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    folder_id = normalizedFolderId,
                    delete_result = data
                },
                affectedObjects: new object[] { new { object_type = "scene_twin_category_folder", scene_id = normalizedSceneId, folder_id = normalizedFolderId, action = "delete", data } },
                nextActions: new[] { "调用 list_scene_twin_category_folders 回读文件夹列表。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除场景孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("查询某个场景文件夹下已启用的孪生体类别配置。返回场景级 twinCategoryConfigID，后续创建/删除/更新实例必须使用这个 ID。")]
    public async Task<McpToolResult> list_scene_twin_categories(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景类别文件夹 ID。")] string folder_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("层级 ID；不传时按后端默认查询。传 ALL 可按后端语义查询所有层级数据。")] string? level_id = null,
        [Description("区域 ID；可选。")] string? region_id = null,
        [Description("所属场景孪生体类别配置 ID；用于查询子级类别，可选。")] string? parent_twin_category_config_id = null,
        [Description("所属孪生体台账 ID；可选。")] string? parent_id = null,
        [Description("实例时间，ISO 日期时间字符串；可选。")] string? instance_time = null,
        [Description("时间粒度枚举；不传时不写入请求体。")] string? date_granularity = null)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedFolderId = NormalizeRequired(folder_id, "folder_id");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/folder/{Uri.EscapeDataString(normalizedFolderId)}/twinCategory";

            var body = new Dictionary<string, object?>();
            AddIfNotBlank(body, "levelId", level_id);
            AddIfNotBlank(body, "regionId", region_id);
            AddIfNotBlank(body, "parentTwinCategoryID", parent_twin_category_config_id);
            AddIfNotBlank(body, "parentID", parent_id);
            AddIfNotBlank(body, "instanceTime", instance_time);
            AddIfNotBlank(body, "dateGranularity", date_granularity);

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success("已查询场景文件夹下的孪生体类别配置。", new
            {
                operational_data = op,
                resource_scope = SceneCategoryScope,
                scene_id = normalizedSceneId,
                folder_id = normalizedFolderId,
                backend_api = "POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/twinCategory",
                request = body,
                id_mapping = new
                {
                    scene_category_config_id = "twinCategoryConfigID",
                    tenant_category_id = "twinCategoryID"
                },
                categories = data
            }, nextActions: new[]
            {
                "从返回项中选择 twinCategoryConfigID，用于 get_scene_twin_category 或 create_twin_instance。",
                "添加新类别前先用本工具检查同一文件夹是否已有对应 twinCategoryID。"
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询场景孪生体类别失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("将租户级孪生体类别加入场景文件夹，生成场景级 twinCategoryConfigID。输入 tenant_category_id 是租户级 twinCategoryID。")]
    public async Task<McpToolResult> add_scene_twin_category(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景孪生体类别文件夹 ID。")] string folder_id,
        [Description("租户级孪生体类别 ID，对应 twinCategoryID。")] string tenant_category_id,
        [Description("加入场景后的类别显示名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("台账数据粒度值；默认 1。")] int? granularity = null,
        [Description("台账数据粒度类型；默认 Year，避免后端默认使用 Second。可选值包括 Second、Minute、Hour、Day、Month、Year。")] string? granularity_type = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedFolderId = NormalizeRequired(folder_id, "folder_id");
            var normalizedTenantCategoryId = NormalizeRequired(tenant_category_id, "tenant_category_id");
            var normalizedName = NormalizeRequired(name, "name");
            var op = _backendClient.ResolveOperationalData(operational_data);

            var resolvedGranularity = granularity ?? 1;
            var resolvedGranularityType = string.IsNullOrWhiteSpace(granularity_type)
                ? "Year"
                : granularity_type.Trim();
            var body = new Dictionary<string, object?>
            {
                ["name"] = normalizedName,
                ["granularity"] = resolvedGranularity,
                ["granularityType"] = resolvedGranularityType
            };

            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/folder/{Uri.EscapeDataString(normalizedFolderId)}/twinCategory/{Uri.EscapeDataString(normalizedTenantCategoryId)}";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将把租户级孪生体类别加入场景。", new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    folder_id = normalizedFolderId,
                    tenant_category_id = normalizedTenantCategoryId,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/folder/{folderId}/twinCategory/{twinCategoryId}",
                    body,
                    default_granularity_note = "未显式传 granularity/granularity_type 时，MCP 默认使用 granularity=1、granularityType=Year，避免后端默认使用 Second。",
                    id_mapping = new
                    {
                        input_tenant_category_id = "twinCategoryID",
                        output_scene_category_config_id = "twinCategoryConfigID"
                    }
                });
            }

            if (!confirm)
            {
                return _results.Failed("添加场景孪生体类别属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已把租户级孪生体类别加入场景。",
                new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    folder_id = normalizedFolderId,
                    tenant_category_id = normalizedTenantCategoryId,
                    category = data
                },
                affectedObjects: new object[] { new { object_type = "scene_twin_category", scene_id = normalizedSceneId, tenant_category_id = normalizedTenantCategoryId, name = normalizedName, data } },
                nextActions: new[] { "调用 list_scene_twin_categories 回读并取得 twinCategoryConfigID。", "后续创建实例时使用 twin_category_config_id，不要直接使用 tenant_category_id。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("添加场景孪生体类别失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("查询场景级孪生体类别配置详情。输入的是 twinCategoryConfigID，不是租户级 twinCategoryID。")]
    public async Task<McpToolResult> get_scene_twin_category(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedConfigId = NormalizeRequired(twin_category_config_id, "twin_category_config_id");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/twinCategory/{Uri.EscapeDataString(normalizedConfigId)}/details";

            var data = await _backendClient.GetAsync(path, CancellationToken.None);

            return _results.Success("已查询场景孪生体类别配置详情。", new
            {
                operational_data = op,
                resource_scope = SceneCategoryScope,
                scene_id = normalizedSceneId,
                twin_category_config_id = normalizedConfigId,
                tenant_category_id = ExtractString(data, "twinCategoryID"),
                backend_api = "GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/details",
                field_summary = new
                {
                    ledger_field_count = ExtractInt(data, "ledgerFieldCount"),
                    time_series_field_count = ExtractInt(data, "timeSeriesFieldCount"),
                    event_field_count = ExtractInt(data, "eventFieldCount")
                },
                category = data
            }, nextActions: new[]
            {
                "后续创建实例时传 twin_category_config_id，不要传 tenant_category_id。",
                "如需编辑模型、字段或显示设置，继续使用 scene_id + twin_category_config_id。"
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询场景孪生体类别详情失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("编辑/重命名场景级孪生体类别配置名称。输入的是 twinCategoryConfigID，不是租户级 twinCategoryID。")]
    public async Task<McpToolResult> update_scene_twin_category(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("新的场景类别显示名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedConfigId = NormalizeRequired(twin_category_config_id, "twin_category_config_id");
            var normalizedName = NormalizeRequired(name, "name");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var body = new Dictionary<string, object?> { ["name"] = normalizedName };
            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/twinCategory/{Uri.EscapeDataString(normalizedConfigId)}/editName";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑场景孪生体类别名称。", new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/editName",
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("编辑场景孪生体类别属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已编辑场景孪生体类别名称。",
                new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    update_result = data
                },
                affectedObjects: new object[] { new { object_type = "scene_twin_category", scene_id = normalizedSceneId, twin_category_config_id = normalizedConfigId, name = normalizedName, action = "rename", data } },
                nextActions: new[] { "调用 get_scene_twin_category 回读场景类别配置详情。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑场景孪生体类别失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("重命名场景级孪生体类别配置。等价于 update_scene_twin_category，只修改名称。")]
    public async Task<McpToolResult> rename_scene_twin_category(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("新的场景类别显示名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await update_scene_twin_category(scene_id, twin_category_config_id, name, operational_data, mode, confirm);
    }

    [McpServerTool]
    [Description("从场景中删除孪生体类别配置及其数据。输入的是场景级 twinCategoryConfigID，不是租户级 twinCategoryID。")]
    public async Task<McpToolResult> delete_scene_twin_category(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(scene_id, "scene_id");
            var normalizedConfigId = NormalizeRequired(twin_category_config_id, "twin_category_config_id");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var path =
                $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/twinCategory/{Uri.EscapeDataString(normalizedConfigId)}/delete";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将从场景中删除孪生体类别配置及其数据。", new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/delete",
                    warning = "该操作删除的是场景级类别配置及其数据，不会删除租户级 twinCategoryID 类别模板。"
                },
                warnings: new[] { "删除操作不可自动回滚，请先确认该场景类别下实例/数据不再需要保留。" });
            }

            if (!confirm)
            {
                return _results.Failed("删除场景孪生体类别属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body: null, CancellationToken.None);
            return _results.Success(
                "已从场景中删除孪生体类别配置。",
                new
                {
                    operational_data = op,
                    resource_scope = SceneCategoryScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    delete_result = data
                },
                affectedObjects: new object[] { new { object_type = "scene_twin_category", scene_id = normalizedSceneId, twin_category_config_id = normalizedConfigId, action = "delete", data } },
                nextActions: new[] { "调用 list_scene_twin_categories 回读场景文件夹下的类别配置。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除场景孪生体类别失败。", new[] { ex.Message });
        }
    }

    private async Task<string> ResolveSceneTwinCategoryFolderIdAsync(string operationalData, string sceneId, string? folderId)
    {
        if (!string.IsNullOrWhiteSpace(folderId))
        {
            return folderId.Trim();
        }

        var root = await GetSceneTwinCategoryRootFolderAsync(operationalData, sceneId);
        var resolvedFolderId = ExtractFolderId(root);
        if (string.IsNullOrWhiteSpace(resolvedFolderId))
        {
            throw new InvalidOperationException("场景 TwinCategory 根文件夹接口未返回 folderID，无法继续。");
        }

        return resolvedFolderId;
    }

    private async Task EnsureNotSceneTwinCategoryRootFolderAsync(string operationalData, string sceneId, string folderId, string actionName)
    {
        var root = await GetSceneTwinCategoryRootFolderAsync(operationalData, sceneId);
        var rootFolderId = ExtractFolderId(root);
        if (string.Equals(rootFolderId, folderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"不允许{actionName}场景 TwinCategory 根文件夹。请传入根目录下的子文件夹 ID。");
        }
    }

    private async Task<JsonElement> GetSceneTwinCategoryRootFolderAsync(string operationalData, string sceneId)
    {
        var path =
            $"/v1/{Uri.EscapeDataString(operationalData)}/location/{Uri.EscapeDataString(sceneId)}/{LocationTwinCategoryFolderType}/assetFolder/root";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    private async Task<JsonElement> GetSceneTwinCategoryChildFoldersAsync(string operationalData, string sceneId, string parentFolderId)
    {
        var path =
            $"/v1/{Uri.EscapeDataString(operationalData)}/location/{Uri.EscapeDataString(sceneId)}/{LocationTwinCategoryFolderType}/assetFolder/{Uri.EscapeDataString(parentFolderId)}";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    private static void AddIfNotBlank(Dictionary<string, object?> body, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            body[key] = value.Trim();
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{parameterName} 不能为空。");
        }

        return value.Trim();
    }

    private static string? ExtractFolderId(JsonElement element)
    {
        return ExtractString(element, "folderID") ?? ExtractString(element, "folderId") ?? ExtractString(element, "id");
    }

    private static string? ExtractString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static int? ExtractInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

    private static bool IsPreview(string? mode)
    {
        return string.Equals(mode, "preview", StringComparison.OrdinalIgnoreCase);
    }
}
