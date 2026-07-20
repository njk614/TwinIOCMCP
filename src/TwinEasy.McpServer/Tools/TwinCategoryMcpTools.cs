using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using TwinEasy.McpServer.Clients;
using TwinEasy.McpServer.Models;
using TwinEasy.McpServer.Services;

namespace TwinEasy.McpServer.Tools;

/// <summary>
/// Tenant-level twin category catalog tools for the V1.1 setup flow.
/// These tools manage tenant category templates and do not operate on scene-level twinCategoryConfigID.
/// </summary>
[McpServerToolType]
public sealed class TwinCategoryMcpTools
{
    private const string TwinCategoryAssetFolderType = "TwinCategory";

    private static readonly object TenantCategoryScope = new
    {
        scope = "tenant_twin_category_catalog",
        id_field = "twinCategoryID",
        does_not_use = new[] { "locationId", "twinCategoryConfigID" },
        scene_level_counterpart = "scene_twin_category_config",
        note = "本工具类只管理租户级孪生体类别库；创建场景实例前还需要把租户类别加入场景，拿到 twinCategoryConfigID。"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyList<object> CategoryTypes = new object[]
    {
        new { category_type = "FixedEquipment", display_name = "固定设备（六轴）" },
        new { category_type = "FixedEquipmentThree", display_name = "固定设备（三轴）" },
        new { category_type = "MobileEquipment", display_name = "移动设备（六轴）" },
        new { category_type = "MobileEquipmentThree", display_name = "移动设备（三轴）" },
        new { category_type = "MobileEquipmentTwo", display_name = "移动设备（二轴）" },
        new { category_type = "RelationshipLine", display_name = "机房网线" },
        new { category_type = "MotorRoomCabinet", display_name = "机房机柜" },
        new { category_type = "CabinetServer", display_name = "机柜服务器" },
        new { category_type = "SensorCone", display_name = "传感器（圆锥）" },
        new { category_type = "SensorSquareCone", display_name = "传感器（方锥）" },
        new { category_type = "Connectivity", display_name = "通讯连接" },
        new { category_type = "Region", display_name = "区域" },
        new { category_type = "DataObject", display_name = "数据对象" }
    };

    private static readonly HashSet<string> CategoryTypeNames = CategoryTypes
        .Select(item => item.GetType().GetProperty("category_type")!.GetValue(item)!.ToString()!)
        .ToHashSet(StringComparer.Ordinal);

    private readonly TwinBackendClient _backendClient;
    private readonly TwinToolResultFactory _results;

    public TwinCategoryMcpTools(TwinBackendClient backendClient, TwinToolResultFactory results)
    {
        _backendClient = backendClient;
        _results = results;
    }

    [McpServerTool]
    [Description("查询可创建的孪生体类别元类型枚举，也就是创建类别时 category_type 的可选值；标准版内置 13 类，包含 DataObject/数据对象。不是租户下已创建的孪生体类别列表。若要回答“租户已创建的孪生体类型有哪些”，请用 list_tenant_twin_categories。")]
    public McpToolResult list_twin_category_types()
    {
        var items = CategoryTypes.ToArray();

        return _results.Success("已读取可创建的孪生体类别元类型枚举。", new
        {
            source = "TwinEasy standard built-in category type allowlist",
            resource_scope = TenantCategoryScope,
            total_available_count = CategoryTypes.Count,
            returned_count = items.Length,
            items,
            note = "这些是孪易标准版 MCP 当前允许创建的 13 个 category_type 清单，包含 DataObject/数据对象；不是租户下已手动或通过 MCP 创建的类别；部署环境不依赖 input 目录。",
            next_step = "创建租户类别时调用 get_twin_category_default_fields 获取所选元类型的默认字段；查询租户已创建类别时调用 list_tenant_twin_categories。"
        });
    }

    [McpServerTool]
    [Description("按孪生体类别元类型 category_type 查询默认字段。create_twin_category 默认会使用这些字段作为基础字段模板；本工具不查询租户已创建类别。")]
    public async Task<McpToolResult> get_twin_category_default_fields(
        [Description("孪生体类别类型，例如 FixedEquipment、MobileEquipment、SensorCone、Region。")] string category_type)
    {
        try
        {
            var normalizedCategoryType = NormalizeCategoryType(category_type);
            var data = await GetDefaultFieldsAsync(normalizedCategoryType);

            return _results.Success("已读取类别类型默认字段。", new
            {
                category_type = normalizedCategoryType,
                resource_scope = TenantCategoryScope,
                backend_api = "GET /v1/twinCategory/defaultField/{categoryType}",
                ledger_fields = GetPropertyOrNull(data, "ledgerFields"),
                time_series_fields = GetPropertyOrNull(data, "timeSeriesFields"),
                event_fields = GetPropertyOrNull(data, "eventFields"),
                raw = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("读取类别类型默认字段失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("查询租户级孪生体类别文件夹。parent_folder_id 不传时先取 TwinCategory 根文件夹，再自动用根 folderID 查询根下子文件夹，避免根接口 childFolderList 不完整；不操作场景级类别配置。")]
    public async Task<McpToolResult> list_twin_category_folders(
        [Description("父文件夹 ID；不传则查询默认分类/TwinCategory 根分类下的子文件夹。")] string? parent_folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var isRoot = string.IsNullOrWhiteSpace(parent_folder_id);

            if (isRoot)
            {
                var root = await GetTwinCategoryRootFolderAsync(op);
                var rootFolderId = ExtractString(root, "folderID") ?? ExtractString(root, "folderId") ?? ExtractString(root, "id");
                if (string.IsNullOrWhiteSpace(rootFolderId))
                {
                    return _results.Failed("查询孪生体类别文件夹失败：TwinCategory 根文件夹接口未返回 folderID。", data: new
                    {
                        operational_data = op,
                        resource_scope = TenantCategoryScope,
                        root
                    });
                }

                var childFolders = await GetTwinCategoryChildFoldersAsync(op, rootFolderId);

                return _results.Success("已读取默认分类/TwinCategory 根分类下的孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    asset_folder_type = TwinCategoryAssetFolderType,
                    parent_folder_id = rootFolderId,
                    parent_folder_id_source = "TwinCategoryRoot.folderID",
                    backend_api = new[]
                    {
                        "GET /v1/{operationalData}/TwinCategory/assetFolder/root",
                        "GET /v1/{operationalData}/TwinCategory/assetFolder/{parentFolderId}"
                    },
                    root_folder = root,
                    folders = childFolders,
                    note = "根接口返回的 childFolderList 可能不完整；本工具已自动使用 root folderID 再查询一次子文件夹接口。"
                });
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/{TwinCategoryAssetFolderType}/assetFolder/{Uri.EscapeDataString(parent_folder_id!.Trim())}";
            var data = await _backendClient.GetAsync(path, CancellationToken.None);

            return _results.Success("已读取孪生体类别子文件夹列表。", new
            {
                operational_data = op,
                resource_scope = TenantCategoryScope,
                asset_folder_type = TwinCategoryAssetFolderType,
                parent_folder_id = parent_folder_id!.Trim(),
                backend_api = "GET /v1/{operationalData}/TwinCategory/assetFolder/{parentFolderId}",
                folders = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("创建租户级孪生体类别文件夹。parent_folder_id 不传时自动创建在 TwinCategory 根文件夹下；不带 locationId，不操作场景级类别配置。")]
    public async Task<McpToolResult> create_twin_category_folder(
        [Description("文件夹名称。")] string name,
        [Description("父文件夹 ID；不传时自动使用 TwinCategory 根文件夹。")] string? parent_folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return _results.Failed("创建孪生体类别文件夹失败：name 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var resolvedParentId = await ResolveTwinCategoryFolderIdAsync(op, parent_folder_id);
            var body = new { name = name.Trim() };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将创建孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    asset_folder_type = TwinCategoryAssetFolderType,
                    parent_folder_id = resolvedParentId,
                    parent_folder_id_source = string.IsNullOrWhiteSpace(parent_folder_id) ? "TwinCategoryRoot" : "Input",
                    backend_api = "POST /v1/{operationalData}/TwinCategory/assetFolder/{parentFolderId}/add",
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("创建孪生体类别文件夹属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/{TwinCategoryAssetFolderType}/assetFolder/{Uri.EscapeDataString(resolvedParentId)}/add";
            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已创建孪生体类别文件夹。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    parent_folder_id = resolvedParentId,
                    folder = data
                },
                affectedObjects: new object[] { new { object_type = "twin_category_folder", name = name.Trim(), data } },
                nextActions: new[] { "调用 list_twin_categories 查询该文件夹下已有孪生体类别。", "调用 create_twin_category 创建孪生体类别。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("创建孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("编辑/重命名租户级孪生体类别文件夹。只修改文件夹名称，不移动文件夹；不带 locationId，不操作场景级类别配置。")]
    public async Task<McpToolResult> update_twin_category_folder(
        [Description("要编辑的租户级孪生体类别文件夹 ID。")] string folder_id,
        [Description("新的文件夹名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder_id))
            {
                return _results.Failed("编辑孪生体类别文件夹失败：folder_id 不能为空。");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return _results.Failed("编辑孪生体类别文件夹失败：name 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var normalizedFolderId = folder_id.Trim();
            await EnsureNotTwinCategoryRootFolderAsync(op, normalizedFolderId, "编辑");

            var body = new { name = name.Trim() };
            var path = $"/v1/{Uri.EscapeDataString(op)}/{TwinCategoryAssetFolderType}/assetFolder/{Uri.EscapeDataString(normalizedFolderId)}/edit";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    asset_folder_type = TwinCategoryAssetFolderType,
                    folder_id = normalizedFolderId,
                    backend_api = "POST /v1/{operationalData}/TwinCategory/assetFolder/{folderId}/edit",
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("编辑孪生体类别文件夹属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已编辑孪生体类别文件夹。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    folder_id = normalizedFolderId,
                    folder = data
                },
                affectedObjects: new object[] { new { object_type = "twin_category_folder", folder_id = normalizedFolderId, name = name.Trim(), data } },
                nextActions: new[] { "调用 list_twin_category_folders 回读文件夹列表。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("重命名租户级孪生体类别文件夹。等价于 update_twin_category_folder，只修改文件夹名称。")]
    public async Task<McpToolResult> rename_twin_category_folder(
        [Description("要重命名的租户级孪生体类别文件夹 ID。")] string folder_id,
        [Description("新的文件夹名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await update_twin_category_folder(folder_id, name, operational_data, mode, confirm);
    }

    [McpServerTool]
    [Description("删除租户级孪生体类别文件夹。会调用 TwinCategory 文件夹删除接口；不带 locationId，不操作场景级类别配置。")]
    public async Task<McpToolResult> delete_twin_category_folder(
        [Description("要删除的租户级孪生体类别文件夹 ID。")] string folder_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder_id))
            {
                return _results.Failed("删除孪生体类别文件夹失败：folder_id 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var normalizedFolderId = folder_id.Trim();
            await EnsureNotTwinCategoryRootFolderAsync(op, normalizedFolderId, "删除");

            var path = $"/v1/{Uri.EscapeDataString(op)}/{TwinCategoryAssetFolderType}/assetFolder/{Uri.EscapeDataString(normalizedFolderId)}/delete";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将删除孪生体类别文件夹。", new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    asset_folder_type = TwinCategoryAssetFolderType,
                    folder_id = normalizedFolderId,
                    backend_api = "POST /v1/{operationalData}/TwinCategory/assetFolder/{folderId}/delete",
                    warning = "删除文件夹可能受后端规则限制；若文件夹下仍有子文件夹或孪生体类别，后端可能拒绝删除。"
                },
                warnings: new[] { "删除操作不可自动回滚，请先确认该文件夹下没有仍需保留的类别或子文件夹。" });
            }

            if (!confirm)
            {
                return _results.Failed("删除孪生体类别文件夹属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body: null, CancellationToken.None);

            return _results.Success(
                "已删除孪生体类别文件夹。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    folder_id = normalizedFolderId,
                    delete_result = data
                },
                affectedObjects: new object[] { new { object_type = "twin_category_folder", folder_id = normalizedFolderId, action = "delete", data } },
                nextActions: new[] { "调用 list_twin_category_folders 回读文件夹列表。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除孪生体类别文件夹失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("查询租户下已创建的孪生体类型/类别。folder_id 不传时默认查询 TwinCategory 根及所有子文件夹；返回租户级 twinCategoryID，不返回元类型枚举。")]
    public async Task<McpToolResult> list_tenant_twin_categories(
        [Description("类别所属文件夹 ID；传入时只查该文件夹；不传时默认查 TwinCategory 根及所有子文件夹。")] string? folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("搜索关键字。")] string? keyword = null,
        [Description("页码；不传则查询全部。")] int? page = null,
        [Description("每页数量；不传则查询全部。")] int? page_size = null,
        [Description("folder_id 不传时是否查询根及所有子文件夹；默认 true。")] bool include_all_folders = true)
    {
        return await QueryTenantTwinCategoriesAsync(
            folder_id,
            operational_data,
            keyword,
            page,
            page_size,
            include_all_folders,
            "已查询租户下已创建的孪生体类别。",
            "TwinCategoryRootAndChildren");
    }

    [McpServerTool]
    [Description("查询租户级孪生体类别列表。返回的是租户已创建类别的 twinCategoryID，不是元类型枚举，也不是场景级 twinCategoryConfigID；folder_id 不传时默认查 TwinCategory 根及所有子文件夹。")]
    public async Task<McpToolResult> list_twin_categories(
        [Description("类别所属文件夹 ID；传入时只查该文件夹；不传时默认查 TwinCategory 根及所有子文件夹。")] string? folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("搜索关键字。")] string? keyword = null,
        [Description("页码；不传则查询全部。")] int? page = null,
        [Description("每页数量；不传则查询全部。")] int? page_size = null,
        [Description("folder_id 不传时是否查询根及所有子文件夹；默认 true。")] bool include_all_folders = true)
    {
        return await QueryTenantTwinCategoriesAsync(
            folder_id,
            operational_data,
            keyword,
            page,
            page_size,
            include_all_folders,
            "已查询孪生体类别列表。",
            "TwinCategoryRootAndChildren");
    }

    private async Task<McpToolResult> QueryTenantTwinCategoriesAsync(
        string? folder_id,
        string? operational_data,
        string? keyword,
        int? page,
        int? page_size,
        bool include_all_folders,
        string successSummary,
        string defaultFolderSource)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var body = BuildTwinCategoryListRequest(keyword, page, page_size);

            if (string.IsNullOrWhiteSpace(folder_id) && include_all_folders)
            {
                var folderIds = await ResolveAllTwinCategoryFolderIdsAsync(op);
                var folderResults = new JsonArray();
                var rows = new JsonArray();
                var backendTotal = 0;

                foreach (var folderId in folderIds)
                {
                    var folderData = await QueryTwinCategoriesInFolderAsync(op, folderId, body);
                    backendTotal += ExtractTotal(folderData);
                    AppendRows(folderData, rows);

                    folderResults.Add(new JsonObject
                    {
                        ["folder_id"] = folderId,
                        ["result"] = JsonNode.Parse(folderData.GetRawText())
                    });
                }

                var aggregate = new JsonObject
                {
                    ["total"] = rows.Count,
                    ["backend_total_sum"] = backendTotal,
                    ["rows"] = rows
                };

                return _results.Success(successSummary, new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    folder_scope = "TwinCategory root and all child folders",
                    folder_ids = folderIds,
                    folder_id_source = defaultFolderSource,
                    backend_api = "POST /v1/{operationalData}/twinCategory/folder/{folderId}",
                    request = body,
                    request_note = "未传 twinCategoryType 过滤条件，避免漏掉 categoryType=DataObject 的租户类别。",
                    id_mapping = new
                    {
                        tenant_category_id = "twinCategoryID",
                        category_meta_type = "categoryType"
                    },
                    categories = aggregate,
                    folder_results = folderResults
                });
            }

            var resolvedFolderId = await ResolveTwinCategoryFolderIdAsync(op, folder_id);
            var data = await QueryTwinCategoriesInFolderAsync(op, resolvedFolderId, body);

            return _results.Success(successSummary, new
            {
                operational_data = op,
                resource_scope = TenantCategoryScope,
                folder_id = resolvedFolderId,
                folder_id_source = string.IsNullOrWhiteSpace(folder_id) ? "TwinCategoryRoot" : "Input",
                backend_api = "POST /v1/{operationalData}/twinCategory/folder/{folderId}",
                request = body,
                request_note = "未传 twinCategoryType 过滤条件，避免漏掉 categoryType=DataObject 的租户类别。",
                id_mapping = new
                {
                    tenant_category_id = "twinCategoryID",
                    category_meta_type = "categoryType"
                },
                categories = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询孪生体类别列表失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("查询租户级孪生体类别详情。返回 twinCategoryID 对应的租户类别模板；不是场景内可直接创建实例的 twinCategoryConfigID。")]
    public async Task<McpToolResult> get_twin_category(
        [Description("孪生体类别 ID，对应 twinCategoryID。")] string category_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category_id))
            {
                return _results.Failed("查询孪生体类别详情失败：category_id 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var path = $"/v1/{Uri.EscapeDataString(op)}/twinCategory/{Uri.EscapeDataString(category_id.Trim())}";
            var data = await _backendClient.GetAsync(path, CancellationToken.None);

            return _results.Success("已查询孪生体类别详情。", new
            {
                operational_data = op,
                resource_scope = TenantCategoryScope,
                category_id = category_id.Trim(),
                backend_api = "GET /v1/{operationalData}/twinCategory/{twinCategoryId}",
                field_summary = BuildFieldSummary(data),
                category = data
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询孪生体类别详情失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("创建租户级孪生体类别模板。默认先按 category_type 拉取默认字段，再合并用户传入字段 JSON；不会创建场景级 twinCategoryConfigID。")]
    public async Task<McpToolResult> create_twin_category(
        [Description("孪生体类别名称。")] string name,
        [Description("孪生体类别类型，例如 FixedEquipment、MobileEquipment、SensorCone、Region。")] string category_type,
        [Description("类别所属文件夹 ID；不传时自动使用 TwinCategory 根文件夹。")] string? folder_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("自定义台账字段 JSON 数组；不传则使用默认字段。")] string? ledger_fields_json = null,
        [Description("自定义时序字段 JSON 数组；不传则使用默认字段。")] string? time_series_fields_json = null,
        [Description("自定义事件字段 JSON 数组；不传则使用默认字段。")] string? event_fields_json = null,
        [Description("模型内容 JSON 字符串或后端要求的 modelContent 字符串，可选。")] string? model_content = null,
        [Description("执行模式：preview 或 execute。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return _results.Failed("创建孪生体类别失败：name 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var normalizedCategoryType = NormalizeCategoryType(category_type);
            var resolvedFolderId = await ResolveTwinCategoryFolderIdAsync(op, folder_id);
            var defaultFields = await GetDefaultFieldsAsync(normalizedCategoryType);

            var ledgerFields = ResolveFieldArray(defaultFields, "ledgerFields", ledger_fields_json);
            var timeSeriesFields = ResolveFieldArray(defaultFields, "timeSeriesFields", time_series_fields_json);
            var eventFields = ResolveFieldArray(defaultFields, "eventFields", event_fields_json);

            var body = new Dictionary<string, object?>
            {
                ["twinCategoryID"] = null,
                ["name"] = name.Trim(),
                ["belongToFolderID"] = resolvedFolderId,
                ["categoryType"] = normalizedCategoryType,
                ["ledgerFields"] = ledgerFields,
                ["timeSeriesFields"] = timeSeriesFields,
                ["eventFields"] = eventFields,
                ["modelContent"] = string.IsNullOrWhiteSpace(model_content) ? null : model_content.Trim()
            };

            var payloadPreview = new
            {
                operational_data = op,
                resource_scope = TenantCategoryScope,
                folder_id = resolvedFolderId,
                folder_id_source = string.IsNullOrWhiteSpace(folder_id) ? "TwinCategoryRoot" : "Input",
                category_type = normalizedCategoryType,
                default_fields_used = new
                {
                    ledger_fields = string.IsNullOrWhiteSpace(ledger_fields_json),
                    time_series_fields = string.IsNullOrWhiteSpace(time_series_fields_json),
                    event_fields = string.IsNullOrWhiteSpace(event_fields_json)
                },
                backend_api = "POST /v1/{operationalData}/twinCategory/Save",
                default_fields_api = "GET /v1/twinCategory/defaultField/{categoryType}",
                save_payload = body
            };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将创建孪生体类别。", payloadPreview);
            }

            if (!confirm)
            {
                return _results.Failed("创建孪生体类别属于写操作，请确认 SaveTwinCategoryRequest 后使用 mode=execute 且 confirm=true。", data: payloadPreview);
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/twinCategory/Save";
            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已创建孪生体类别。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    folder_id = resolvedFolderId,
                    category_type = normalizedCategoryType,
                    category = data,
                    save_payload = body
                },
                affectedObjects: new object[] { new { object_type = "twin_category", name = name.Trim(), category_type = normalizedCategoryType, data } },
                nextActions: new[] { "调用 get_twin_category 回读租户类别详情。", "如需创建场景实例，先把该租户类别加入指定场景并取得 twinCategoryConfigID。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("创建孪生体类别失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("编辑租户级孪生体类别模板。先回读当前详情，再合并显式参数或 patch_json；不会编辑场景级 twinCategoryConfigID 配置。")]
    public async Task<McpToolResult> update_twin_category(
        [Description("孪生体类别 ID，对应 twinCategoryID。")] string category_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("新的类别名称；不传则保留当前名称。")] string? name = null,
        [Description("新的类别文件夹 ID；不传则保留当前 belongToFolderID。")] string? folder_id = null,
        [Description("新的类别类型；不传则保留当前 categoryType。")] string? category_type = null,
        [Description("台账字段 JSON 数组；按 fieldID/fieldId/id/name 覆盖当前字段并追加新增字段。")] string? ledger_fields_json = null,
        [Description("时序字段 JSON 数组；按 fieldID/fieldId/id/name 覆盖当前字段并追加新增字段。")] string? time_series_fields_json = null,
        [Description("事件字段 JSON 数组；按 fieldID/fieldId/id/name 覆盖当前字段并追加新增字段。")] string? event_fields_json = null,
        [Description("模型内容 JSON 字符串或后端要求的 modelContent 字符串；不传则不写 modelContent。")] string? model_content = null,
        [Description("可选 JSON 对象补丁，支持 name、folder_id/belongToFolderID、category_type/categoryType、ledgerFields、timeSeriesFields、eventFields、modelContent。")] string? patch_json = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category_id))
            {
                return _results.Failed("编辑孪生体类别失败：category_id 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var current = await GetTwinCategoryAsync(op, category_id.Trim());
            var patch = ParsePatchObject(patch_json);

            var resolvedName =
                FirstNonBlank(name, GetPatchString(patch, "name"), ExtractString(current, "name"));
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                return _results.Failed("编辑孪生体类别失败：当前类别名称为空，请显式传入 name。");
            }

            var resolvedFolderId =
                FirstNonBlank(folder_id, GetPatchString(patch, "folder_id"), GetPatchString(patch, "belongToFolderID"), ExtractString(current, "belongToFolderID"));
            if (string.IsNullOrWhiteSpace(resolvedFolderId))
            {
                resolvedFolderId = await ResolveTwinCategoryFolderIdAsync(op, null);
            }

            var currentCategoryType = ExtractString(current, "categoryType");
            var requestedCategoryType = FirstNonBlank(category_type, GetPatchString(patch, "category_type"), GetPatchString(patch, "categoryType"), currentCategoryType);
            var normalizedCategoryType = NormalizeCategoryType(requestedCategoryType ?? string.Empty);
            var categoryTypeChanged = !string.Equals(currentCategoryType, normalizedCategoryType, StringComparison.Ordinal);

            var defaultFieldsForNewType = categoryTypeChanged
                ? await GetDefaultFieldsAsync(normalizedCategoryType)
                : default;

            var ledgerPatch = FirstNonBlank(ledger_fields_json, GetPatchArrayJson(patch, "ledger_fields"), GetPatchArrayJson(patch, "ledgerFields"));
            var timeSeriesPatch = FirstNonBlank(time_series_fields_json, GetPatchArrayJson(patch, "time_series_fields"), GetPatchArrayJson(patch, "timeSeriesFields"));
            var eventPatch = FirstNonBlank(event_fields_json, GetPatchArrayJson(patch, "event_fields"), GetPatchArrayJson(patch, "eventFields"));

            var ledgerBase = categoryTypeChanged && string.IsNullOrWhiteSpace(ledgerPatch)
                ? defaultFieldsForNewType
                : current;
            var timeSeriesBase = categoryTypeChanged && string.IsNullOrWhiteSpace(timeSeriesPatch)
                ? defaultFieldsForNewType
                : current;
            var eventBase = categoryTypeChanged && string.IsNullOrWhiteSpace(eventPatch)
                ? defaultFieldsForNewType
                : current;

            var body = new Dictionary<string, object?>
            {
                ["twinCategoryID"] = category_id.Trim(),
                ["name"] = resolvedName.Trim(),
                ["belongToFolderID"] = resolvedFolderId.Trim(),
                ["categoryType"] = normalizedCategoryType,
                ["ledgerFields"] = ResolveFieldArray(ledgerBase, "ledgerFields", ledgerPatch),
                ["timeSeriesFields"] = ResolveFieldArray(timeSeriesBase, "timeSeriesFields", timeSeriesPatch),
                ["eventFields"] = ResolveFieldArray(eventBase, "eventFields", eventPatch)
            };

            var resolvedModelContent = FirstNonBlank(model_content, GetPatchString(patch, "model_content"), GetPatchString(patch, "modelContent"));
            if (!string.IsNullOrWhiteSpace(resolvedModelContent))
            {
                body["modelContent"] = resolvedModelContent.Trim();
            }

            var payloadPreview = new
            {
                operational_data = op,
                resource_scope = TenantCategoryScope,
                category_id = category_id.Trim(),
                category_type_changed = categoryTypeChanged,
                default_fields_used_for_type_change = new
                {
                    ledger_fields = categoryTypeChanged && string.IsNullOrWhiteSpace(ledgerPatch),
                    time_series_fields = categoryTypeChanged && string.IsNullOrWhiteSpace(timeSeriesPatch),
                    event_fields = categoryTypeChanged && string.IsNullOrWhiteSpace(eventPatch)
                },
                backend_api = "POST /v1/{operationalData}/twinCategory/Save",
                current_category = current,
                save_payload = body
            };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑孪生体类别。", payloadPreview);
            }

            if (!confirm)
            {
                return _results.Failed("编辑孪生体类别属于写操作，请确认 SaveTwinCategoryRequest 后使用 mode=execute 且 confirm=true。", data: payloadPreview);
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/twinCategory/Save";
            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已编辑孪生体类别。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    category_id = category_id.Trim(),
                    category = data,
                    save_payload = body
                },
                affectedObjects: new object[] { new { object_type = "twin_category", category_id = category_id.Trim(), name = resolvedName.Trim(), data } },
                nextActions: new[] { "调用 get_twin_category 回读租户类别详情。", "如需创建实例，先确认该类别已加入场景并取得 twinCategoryConfigID。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑孪生体类别失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("重命名租户级孪生体类别模板。等价于 update_twin_category 只传 name；输入/返回均使用租户级 twinCategoryID。")]
    public async Task<McpToolResult> rename_twin_category(
        [Description("孪生体类别 ID，对应 twinCategoryID。")] string category_id,
        [Description("新的类别名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await update_twin_category(
            category_id: category_id,
            operational_data: operational_data,
            name: name,
            folder_id: null,
            category_type: null,
            ledger_fields_json: null,
            time_series_fields_json: null,
            event_fields_json: null,
            model_content: null,
            patch_json: null,
            mode: mode,
            confirm: confirm);
    }

    [McpServerTool]
    [Description("删除租户级孪生体类别模板。category_ids 支持单个 ID 或逗号/分号/换行分隔的多个 twinCategoryID；不会删除场景级 twinCategoryConfigID。")]
    public async Task<McpToolResult> delete_twin_category(
        [Description("要删除的孪生体类别 ID；多个 ID 可用逗号、分号或换行分隔。")] string category_ids,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false)
    {
        try
        {
            var ids = ParseIdList(category_ids, "category_ids");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var path = $"/v1/{Uri.EscapeDataString(op)}/twinCategory/delete";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将删除租户级孪生体类别。", new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    backend_api = "POST /v1/{operationalData}/twinCategory/delete",
                    category_ids = ids,
                    body = ids,
                    warning = "删除租户级类别可能影响已引用该类别的场景配置或实例，请先确认依赖关系。"
                },
                warnings: new[] { "删除操作不可自动回滚；execute 时必须显式 confirm=true。" });
            }

            if (!confirm)
            {
                return _results.Failed("删除孪生体类别属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, ids, CancellationToken.None);
            return _results.Success(
                "已删除租户级孪生体类别。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    category_ids = ids,
                    delete_result = data
                },
                affectedObjects: ids.Select(id => new { object_type = "twin_category", category_id = id, action = "delete" }).Cast<object>().ToArray(),
                nextActions: new[] { "调用 list_tenant_twin_categories 回读租户类别列表。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除孪生体类别失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("复制租户级孪生体类别模板。category_ids 支持单个 ID 或逗号/分号/换行分隔的多个 twinCategoryID；后端复制接口不支持直接指定新名称或目标文件夹。")]
    public async Task<McpToolResult> copy_twin_category(
        [Description("要复制的孪生体类别 ID；多个 ID 可用逗号、分号或换行分隔。")] string category_ids,
        [Description("可选：复制完成后尝试移动复制出的新类别到目标文件夹；只有后端 copy_result 返回新 ID 时可自动移动。")] string? target_folder_id = null,
        [Description("操作域仅用于返回上下文；后端复制接口 POST /v1/twinCategorys/copy 不带 operationalData。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 复制时必须为 true。")] bool confirm = false)
    {
        try
        {
            var ids = ParseIdList(category_ids, "category_ids");
            var op = _backendClient.ResolveOperationalData(operational_data);
            const string path = "/v1/twinCategorys/copy";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将复制租户级孪生体类别。", new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    backend_api = "POST /v1/twinCategorys/copy",
                    category_ids = ids,
                    target_folder_id = string.IsNullOrWhiteSpace(target_folder_id) ? null : target_folder_id.Trim(),
                    body = ids,
                    note = "Swagger 显示复制接口请求体为 string[]，不支持在复制时直接传 target_name；如传 target_folder_id，本工具会在复制成功且后端返回新 ID 后自动调用 move_twin_category 逻辑移动复制件。"
                });
            }

            if (!confirm)
            {
                return _results.Failed("复制孪生体类别属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, ids, CancellationToken.None);
            JsonElement? moveResult = null;
            string[] copiedIds = Array.Empty<string>();
            var warnings = new List<string>();

            if (!string.IsNullOrWhiteSpace(target_folder_id))
            {
                copiedIds = ExtractBatchSuccessIds(data);
                if (copiedIds.Length == 0)
                {
                    warnings.Add("已调用复制接口，但未能从 copy_result.successRows 中解析复制出的新 twinCategoryID；请先回查列表，再调用 move_twin_category 移动复制件。");
                }
                else
                {
                    var normalizedFolderId = target_folder_id.Trim();
                    var movePath = $"/v1/{Uri.EscapeDataString(op)}/twinCategory/move/folder/{Uri.EscapeDataString(normalizedFolderId)}";
                    moveResult = await _backendClient.PostAsync(movePath, copiedIds, CancellationToken.None);
                }
            }

            return _results.Success(
                "已复制租户级孪生体类别。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    source_category_ids = ids,
                    target_folder_id = string.IsNullOrWhiteSpace(target_folder_id) ? null : target_folder_id.Trim(),
                    copied_category_ids = copiedIds,
                    copy_result = data,
                    move_result = moveResult
                },
                affectedObjects: ids.Select(id => new { object_type = "twin_category", source_category_id = id, action = "copy" }).Cast<object>().ToArray(),
                nextActions: new[] { "调用 list_tenant_twin_categories 回读复制后的类别；如需改名，再调用 rename_twin_category。" },
                warnings: warnings);
        }
        catch (Exception ex)
        {
            return _results.Failed("复制孪生体类别失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("移动租户级孪生体类别到目标文件夹。category_ids 支持单个 ID 或逗号/分号/换行分隔的多个 twinCategoryID。")]
    public async Task<McpToolResult> move_twin_category(
        [Description("要移动的孪生体类别 ID；多个 ID 可用逗号、分号或换行分隔。")] string category_ids,
        [Description("目标租户级孪生体类别文件夹 ID。")] string target_folder_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 移动时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target_folder_id))
            {
                return _results.Failed("移动孪生体类别失败：target_folder_id 不能为空。");
            }

            var ids = ParseIdList(category_ids, "category_ids");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var normalizedFolderId = target_folder_id.Trim();
            var path = $"/v1/{Uri.EscapeDataString(op)}/twinCategory/move/folder/{Uri.EscapeDataString(normalizedFolderId)}";

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将移动租户级孪生体类别。", new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    backend_api = "POST /v1/{operationalData}/twinCategory/move/folder/{folderId}",
                    target_folder_id = normalizedFolderId,
                    category_ids = ids,
                    body = ids
                });
            }

            if (!confirm)
            {
                return _results.Failed("移动孪生体类别属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, ids, CancellationToken.None);
            return _results.Success(
                "已移动租户级孪生体类别。",
                new
                {
                    operational_data = op,
                    resource_scope = TenantCategoryScope,
                    target_folder_id = normalizedFolderId,
                    category_ids = ids,
                    move_result = data
                },
                affectedObjects: ids.Select(id => new { object_type = "twin_category", category_id = id, action = "move", target_folder_id = normalizedFolderId }).Cast<object>().ToArray(),
                nextActions: new[] { "调用 list_tenant_twin_categories 并传入目标 folder_id 回读目标文件夹下的类别。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("移动孪生体类别失败。", new[] { ex.Message });
        }
    }

    private static string NormalizeCategoryType(string categoryType)
    {
        var normalized = categoryType.Trim();
        if (!CategoryTypeNames.Contains(normalized))
        {
            throw new InvalidOperationException($"不支持的 category_type：{categoryType}。请先调用 list_twin_category_types 查看可用类型。");
        }

        return normalized;
    }

    private async Task<JsonElement> GetDefaultFieldsAsync(string categoryType)
    {
        var path = $"/v1/twinCategory/defaultField/{Uri.EscapeDataString(categoryType)}";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    private async Task<JsonElement> GetTwinCategoryAsync(string operationalData, string categoryId)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/twinCategory/{Uri.EscapeDataString(categoryId)}";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    private async Task<JsonElement> QueryTwinCategoriesInFolderAsync(
        string operationalData,
        string folderId,
        IReadOnlyDictionary<string, object?> body)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/twinCategory/folder/{Uri.EscapeDataString(folderId)}";
        return await _backendClient.PostAsync(path, body, CancellationToken.None);
    }

    private static Dictionary<string, object?> BuildTwinCategoryListRequest(string? keyword, int? page, int? pageSize)
    {
        return new Dictionary<string, object?>
        {
            ["pageIndex"] = page ?? 1,
            ["pageSize"] = pageSize ?? 0,
            ["isSearchAll"] = page is null && pageSize is null,
            ["searchBy"] = keyword,
            ["orderBy"] = "createAt",
            ["ascending"] = false,
            ["isGetSensor"] = false,
            ["isPublish"] = false
        };
    }

    private static string[] ParseIdList(string rawIds, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(rawIds))
        {
            throw new InvalidOperationException($"{parameterName} 不能为空。");
        }

        var ids = rawIds
            .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            throw new InvalidOperationException($"{parameterName} 至少需要包含一个有效 ID。");
        }

        return ids;
    }

    private async Task<string> ResolveTwinCategoryFolderIdAsync(string operationalData, string? folderId)
    {
        if (!string.IsNullOrWhiteSpace(folderId))
        {
            return folderId.Trim();
        }

        var root = await GetTwinCategoryRootFolderAsync(operationalData);
        var resolvedFolderId = ExtractString(root, "folderID") ?? ExtractString(root, "folderId") ?? ExtractString(root, "id");
        if (string.IsNullOrWhiteSpace(resolvedFolderId))
        {
            throw new InvalidOperationException("TwinCategory 根文件夹接口未返回 folderID，无法继续。");
        }

        return resolvedFolderId;
    }

    private async Task EnsureNotTwinCategoryRootFolderAsync(string operationalData, string folderId, string actionName)
    {
        var root = await GetTwinCategoryRootFolderAsync(operationalData);
        var rootFolderId = ExtractString(root, "folderID") ?? ExtractString(root, "folderId") ?? ExtractString(root, "id");
        if (string.Equals(rootFolderId, folderId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"不允许{actionName} TwinCategory 根文件夹。请传入根目录下的子文件夹 ID。");
        }
    }

    private async Task<IReadOnlyList<string>> ResolveAllTwinCategoryFolderIdsAsync(string operationalData)
    {
        var root = await GetTwinCategoryRootFolderAsync(operationalData);
        var rootFolderId = ExtractString(root, "folderID") ?? ExtractString(root, "folderId") ?? ExtractString(root, "id");
        if (string.IsNullOrWhiteSpace(rootFolderId))
        {
            throw new InvalidOperationException("TwinCategory 根文件夹接口未返回 folderID，无法查询所有类别文件夹。");
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        var queue = new Queue<string>();

        AddFolderId(rootFolderId, visited, result);

        var rootChildren = await GetTwinCategoryChildFoldersAsync(operationalData, rootFolderId);
        EnqueueFolderIds(rootChildren, visited, result, queue);

        while (queue.Count > 0)
        {
            var parentFolderId = queue.Dequeue();
            var children = await GetTwinCategoryChildFoldersAsync(operationalData, parentFolderId);
            EnqueueFolderIds(children, visited, result, queue);
        }

        return result;
    }

    private async Task<JsonElement> GetTwinCategoryRootFolderAsync(string operationalData)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/{TwinCategoryAssetFolderType}/assetFolder/root";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    private async Task<JsonElement> GetTwinCategoryChildFoldersAsync(string operationalData, string parentFolderId)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/{TwinCategoryAssetFolderType}/assetFolder/{Uri.EscapeDataString(parentFolderId)}";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    private static void EnqueueFolderIds(
        JsonElement element,
        HashSet<string> visited,
        List<string> result,
        Queue<string> queue)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var folderId = ExtractString(element, "folderID") ?? ExtractString(element, "folderId");
                if (!string.IsNullOrWhiteSpace(folderId))
                {
                    AddFolderId(folderId, visited, result, queue);
                }

                foreach (var property in element.EnumerateObject())
                {
                    EnqueueFolderIds(property.Value, visited, result, queue);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    EnqueueFolderIds(item, visited, result, queue);
                }
                break;
        }
    }

    private static void AddFolderId(
        string folderId,
        HashSet<string> visited,
        List<string> result)
    {
        var normalized = folderId.Trim();
        if (visited.Add(normalized))
        {
            result.Add(normalized);
        }
    }

    private static void AddFolderId(
        string folderId,
        HashSet<string> visited,
        List<string> result,
        Queue<string> queue)
    {
        var normalized = folderId.Trim();
        if (visited.Add(normalized))
        {
            result.Add(normalized);
            queue.Enqueue(normalized);
        }
    }

    private static void AppendRows(JsonElement data, JsonArray rows)
    {
        if (TryGetRowsArray(data, out var sourceRows))
        {
            foreach (var row in sourceRows.EnumerateArray())
            {
                rows.Add(JsonNode.Parse(row.GetRawText()));
            }
        }
        else if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in data.EnumerateArray())
            {
                rows.Add(JsonNode.Parse(row.GetRawText()));
            }
        }
    }

    private static int ExtractTotal(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("total", out var total) &&
            total.ValueKind == JsonValueKind.Number &&
            total.TryGetInt32(out var value))
        {
            return value;
        }

        if (TryGetRowsArray(data, out var rows))
        {
            return rows.GetArrayLength();
        }

        return data.ValueKind == JsonValueKind.Array ? data.GetArrayLength() : 0;
    }

    private static string[] ExtractBatchSuccessIds(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty("successRows", out var successRows) ||
            successRows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var ids = new List<string>();
        foreach (var item in successRows.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    ids.Add(value.Trim());
                }
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                var id =
                    ExtractString(item, "twinCategoryID") ??
                    ExtractString(item, "twinCategoryId") ??
                    ExtractString(item, "id");
                if (!string.IsNullOrWhiteSpace(id))
                {
                    ids.Add(id.Trim());
                }
            }
        }

        return ids.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool TryGetRowsArray(JsonElement data, out JsonElement rows)
    {
        if (data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("rows", out rows) &&
            rows.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        rows = default;
        return false;
    }

    private static JsonNode? ResolveFieldArray(JsonElement defaultFields, string propertyName, string? customFieldsJson)
    {
        var defaultArray = GetJsonArrayOrEmpty(defaultFields, propertyName);
        if (string.IsNullOrWhiteSpace(customFieldsJson))
        {
            return defaultArray;
        }

        var customArray = ParseJsonArray(customFieldsJson, propertyName);
        return MergeFieldArrays(defaultArray, customArray);
    }

    private static JsonArray GetJsonArrayOrEmpty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            return JsonNode.Parse(value.GetRawText())!.AsArray();
        }

        return new JsonArray();
    }

    private static JsonArray ParseJsonArray(string json, string parameterName)
    {
        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonArray array)
            {
                return array;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{parameterName} 必须是合法 JSON 数组。", ex);
        }

        throw new InvalidOperationException($"{parameterName} 必须是 JSON 数组。");
    }

    private static JsonArray MergeFieldArrays(JsonArray defaults, JsonArray custom)
    {
        var result = new JsonArray();
        var indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in defaults)
        {
            var clone = CloneNode(item);
            var key = GetFieldKey(clone);
            if (key is not null)
            {
                indexByKey[key] = result.Count;
            }
            result.Add(clone);
        }

        foreach (var item in custom)
        {
            var clone = CloneNode(item);
            var key = GetFieldKey(clone);
            if (key is not null && indexByKey.TryGetValue(key, out var index))
            {
                result[index] = clone;
            }
            else
            {
                if (key is not null)
                {
                    indexByKey[key] = result.Count;
                }
                result.Add(clone);
            }
        }

        return result;
    }

    private static JsonObject? ParsePatchObject(string? patchJson)
    {
        if (string.IsNullOrWhiteSpace(patchJson))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(patchJson) is JsonObject patch)
            {
                return patch;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("patch_json must be a valid JSON object.", ex);
        }

        throw new InvalidOperationException("patch_json must be a JSON object.");
    }

    private static JsonNode? CloneNode(JsonNode? node)
    {
        return node is null ? null : JsonNode.Parse(node.ToJsonString(JsonOptions));
    }

    private static string? GetFieldKey(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return null;
        }

        return GetJsonString(obj, "fieldID") ??
               GetJsonString(obj, "fieldId") ??
               GetJsonString(obj, "id") ??
               GetJsonString(obj, "name");
    }

    private static string? GetJsonString(JsonObject obj, string propertyName)
    {
        return obj.TryGetPropertyValue(propertyName, out var value) && value is not null
            ? value.ToString()
            : null;
    }

    private static string? GetPatchString(JsonObject? patch, string propertyName)
    {
        if (patch is null ||
            !patch.TryGetPropertyValue(propertyName, out var value) ||
            value is null ||
            value.GetValueKind() == JsonValueKind.Null)
        {
            return null;
        }

        return value.ToString();
    }

    private static string? GetPatchArrayJson(JsonObject? patch, string propertyName)
    {
        if (patch is null ||
            !patch.TryGetPropertyValue(propertyName, out var value) ||
            value is not JsonArray)
        {
            return null;
        }

        return value.ToJsonString(JsonOptions);
    }

    private static string? FirstNonBlank(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static object BuildFieldSummary(JsonElement category)
    {
        return new
        {
            ledger_fields_count = CountArray(category, "ledgerFields"),
            time_series_fields_count = CountArray(category, "timeSeriesFields"),
            event_fields_count = CountArray(category, "eventFields")
        };
    }

    private static int CountArray(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Array
            ? value.GetArrayLength()
            : 0;
    }

    private static JsonElement? GetPropertyOrNull(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value)
            ? value.Clone()
            : null;
    }

    private static string? ExtractString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static bool IsPreview(string? mode)
    {
        return string.Equals(mode, "preview", StringComparison.OrdinalIgnoreCase);
    }
}
