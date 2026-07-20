using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using TwinEasy.McpServer.Clients;
using TwinEasy.McpServer.Models;
using TwinEasy.McpServer.Services;

namespace TwinEasy.McpServer.Tools;

/// <summary>
/// Scene twin instance ledger-data tools.
/// These tools operate on locationId + twinCategoryConfigID + twinCategoryDataID.
/// </summary>
[McpServerToolType]
public sealed class TwinInstanceLedgerMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly object InstanceLedgerScope = new
    {
        scope = "scene_twin_instance_ledger_data",
        category_id_field = "twinCategoryConfigID",
        instance_id_field = "twinCategoryDataID",
        requires = new[] { "locationId", "twinCategoryConfigID" },
        note = "实例台账数据属于场景级孪生体类别配置；必须使用 scene_id + twin_category_config_id，不能直接使用租户级 twinCategoryID。"
    };

    private readonly TwinBackendClient _backendClient;
    private readonly TwinToolResultFactory _results;

    public TwinInstanceLedgerMcpTools(TwinBackendClient backendClient, TwinToolResultFactory results)
    {
        _backendClient = backendClient;
        _results = results;
    }

    [McpServerTool]
    [Description("查询场景级孪生体类别的台账字段定义。输入的是 twinCategoryConfigID，不是租户级 twinCategoryID。")]
    public async Task<McpToolResult> list_twin_ledger_fields(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var sceneId = NormalizeRequired(scene_id, "scene_id");
            var configId = NormalizeRequired(twin_category_config_id, "twin_category_config_id");
            var op = _backendClient.ResolveOperationalData(operational_data);
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(sceneId)}/twinCategory/{Uri.EscapeDataString(configId)}/field";
            var data = await _backendClient.GetAsync(path, CancellationToken.None);

            return _results.Success("已查询场景孪生体类别台账字段。", new
            {
                operational_data = op,
                resource_scope = InstanceLedgerScope,
                scene_id = sceneId,
                twin_category_config_id = configId,
                backend_api = "GET /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/field",
                fields = data
            }, nextActions: new[]
            {
                "根据返回字段组装 ledger_data_json，然后调用 create_twin_instance 或 upsert_twin_instance_ledger_data。",
                "ledger_data_json 会作为 content 字符串写入后端 /data 接口。"
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询孪生体台账字段失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("创建场景孪生体实例，也就是新增一条该类别下的台账数据。ledger_data_json 会序列化为后端 content 字符串。")]
    public async Task<McpToolResult> create_twin_instance(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("实例台账数据 JSON 对象字符串。")] string ledger_data_json,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("Ledger granularity type. Default Year. Year time fields require yyyy-MM-dd HH:mm:ss, e.g. 2026-01-01 00:00:00.")] string? granularity_type = "Year",
        [Description("Ledger granularity value. Year=month number, Month=day number, Day=hour number. Default 1.")] int granularity = 1,
        [Description("Execution mode: preview or execute. Default preview.")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await AddLedgerDataAsync(
            scene_id,
            twin_category_config_id,
            ledger_data_json,
            operational_data,
            granularity_type,
            granularity,
            mode,
            confirm,
            toolName: "create_twin_instance");
    }

    [McpServerTool]
    [Description("查询场景孪生体实例/台账数据列表。返回 twinCategoryDataID，后续编辑和删除实例用这个 ID。")]
    public async Task<McpToolResult> list_twin_instances(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("层级 ID；不传按后端默认查询，传 ALL 可查询所有层级数据。")] string? level_id = null,
        [Description("区域 ID；可选。")] string? region_id = null,
        [Description("所属场景孪生体类别配置 ID；用于查询子级实例，可选。")] string? parent_twin_category_config_id = null,
        [Description("所属孪生体台账 ID；可选。")] string? parent_id = null,
        [Description("可选：后端 ConditionModel JSON 对象字符串。")] string? condition_json = null,
        [Description("页码；不传则查询全部。")] int? page = null,
        [Description("每页数量，最大 100；不传则查询全部。")] int? page_size = null)
    {
        return await ListLedgerDataAsync(
            scene_id,
            twin_category_config_id,
            operational_data,
            level_id,
            region_id,
            parent_twin_category_config_id,
            parent_id,
            condition_json,
            page,
            page_size,
            summary: "已查询场景孪生体实例列表。");
    }

    [McpServerTool]
    [Description("查询单个场景孪生体实例/台账数据。instance_id 对应 twinCategoryDataID，也可用 instanceNumber 匹配。")]
    public async Task<McpToolResult> get_twin_instance(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("实例 ID，对应 twinCategoryDataID；也会尝试匹配 instanceNumber。")] string instance_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var normalizedInstanceId = NormalizeRequired(instance_id, "instance_id");
            var listResult = await QueryLedgerDataAsync(
                scene_id,
                twin_category_config_id,
                operational_data,
                levelId: "ALL",
                regionId: null,
                parentTwinCategoryConfigId: null,
                parentId: null,
                conditionJson: null,
                page: null,
                pageSize: null);

            var match = FindInstance(listResult.Data, normalizedInstanceId);
            if (match is null)
            {
                return _results.Failed("未找到指定场景孪生体实例。", data: new
                {
                    listResult.OperationalData,
                    resource_scope = InstanceLedgerScope,
                    scene_id = listResult.SceneId,
                    twin_category_config_id = listResult.TwinCategoryConfigId,
                    instance_id = normalizedInstanceId,
                    searched_fields = new[] { "twinCategoryDataID", "instanceNumber" }
                });
            }

            return _results.Success("已查询场景孪生体实例详情。", new
            {
                operational_data = listResult.OperationalData,
                resource_scope = InstanceLedgerScope,
                scene_id = listResult.SceneId,
                twin_category_config_id = listResult.TwinCategoryConfigId,
                instance_id = normalizedInstanceId,
                instance = match.Value
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询场景孪生体实例详情失败。", new[] { ex.Message });
        }
    }

    [McpServerTool]
    [Description("编辑场景孪生体实例/台账数据。instance_id 对应 twinCategoryDataID，ledger_data_json 会作为 content 字符串写入。")]
    public async Task<McpToolResult> update_twin_instance(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("实例 ID，对应 twinCategoryDataID。")] string instance_id,
        [Description("更新后的实例台账数据 JSON 对象字符串。")] string ledger_data_json,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("Ledger granularity type. Default Year. Year time fields require yyyy-MM-dd HH:mm:ss, e.g. 2026-01-01 00:00:00.")] string? granularity_type = "Year",
        [Description("Ledger granularity value. Year=month number, Month=day number, Day=hour number. Default 1.")] int granularity = 1,
        [Description("Execution mode: preview or execute. Default preview.")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await UpdateLedgerDataAsync(
            scene_id,
            twin_category_config_id,
            instance_id,
            ledger_data_json,
            operational_data,
            granularity_type,
            granularity,
            mode,
            confirm,
            toolName: "update_twin_instance");
    }

    [McpServerTool]
    [Description("删除场景孪生体实例/台账数据。instance_ids 支持单个 ID 或逗号/分号/换行分隔的多个 twinCategoryDataID。")]
    public async Task<McpToolResult> delete_twin_instance(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("实例 ID 集合；多个 ID 可用逗号、分号或换行分隔。")] string instance_ids,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("Execution mode: preview or execute. Default preview.")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false)
    {
        return await DeleteLedgerDataAsync(
            scene_id,
            twin_category_config_id,
            instance_ids,
            operational_data,
            mode,
            confirm,
            toolName: "delete_twin_instance");
    }

    [McpServerTool]
    [Description("新增或更新实例台账数据。instance_id 不传则新增；传入 twinCategoryDataID 则更新。")]
    public async Task<McpToolResult> upsert_twin_instance_ledger_data(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("实例台账数据 JSON 对象字符串。")] string ledger_data_json,
        [Description("可选：实例 ID，对应 twinCategoryDataID；不传则新增，传入则更新。")] string? instance_id = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("Ledger granularity type. Default Year. Year time fields require yyyy-MM-dd HH:mm:ss, e.g. 2026-01-01 00:00:00.")] string? granularity_type = "Year",
        [Description("Ledger granularity value. Year=month number, Month=day number, Day=hour number. Default 1.")] int granularity = 1,
        [Description("Execution mode: preview or execute. Default preview.")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        if (string.IsNullOrWhiteSpace(instance_id))
        {
            return await AddLedgerDataAsync(
                scene_id,
                twin_category_config_id,
                ledger_data_json,
                operational_data,
                granularity_type,
                granularity,
                mode,
                confirm,
                toolName: "upsert_twin_instance_ledger_data");
        }

        return await UpdateLedgerDataAsync(
            scene_id,
            twin_category_config_id,
            instance_id,
            ledger_data_json,
            operational_data,
            granularity_type,
            granularity,
            mode,
            confirm,
            toolName: "upsert_twin_instance_ledger_data");
    }

    [McpServerTool]
    [Description("查询实例台账数据列表。与 list_twin_instances 使用同一后端接口。")]
    public async Task<McpToolResult> list_twin_instance_ledger_data(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("层级 ID；不传按后端默认查询，传 ALL 可查询所有层级数据。")] string? level_id = null,
        [Description("区域 ID；可选。")] string? region_id = null,
        [Description("所属场景孪生体类别配置 ID；用于查询子级实例，可选。")] string? parent_twin_category_config_id = null,
        [Description("所属孪生体台账 ID；可选。")] string? parent_id = null,
        [Description("可选：后端 ConditionModel JSON 对象字符串。")] string? condition_json = null,
        [Description("页码；不传则查询全部。")] int? page = null,
        [Description("每页数量，最大 100；不传则查询全部。")] int? page_size = null)
    {
        return await ListLedgerDataAsync(
            scene_id,
            twin_category_config_id,
            operational_data,
            level_id,
            region_id,
            parent_twin_category_config_id,
            parent_id,
            condition_json,
            page,
            page_size,
            summary: "已查询实例台账数据列表。");
    }

    [McpServerTool]
    [Description("删除实例台账数据。与 delete_twin_instance 使用同一后端接口。")]
    public async Task<McpToolResult> delete_twin_instance_ledger_data(
        [Description("场景 ID，对应后端 locationId。")] string scene_id,
        [Description("场景级孪生体类别配置 ID，对应 twinCategoryConfigID。")] string twin_category_config_id,
        [Description("实例 ID 集合；多个 ID 可用逗号、分号或换行分隔。")] string instance_ids,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false)
    {
        return await DeleteLedgerDataAsync(
            scene_id,
            twin_category_config_id,
            instance_ids,
            operational_data,
            mode,
            confirm,
            toolName: "delete_twin_instance_ledger_data");
    }

    private async Task<McpToolResult> AddLedgerDataAsync(
        string sceneId,
        string twinCategoryConfigId,
        string ledgerDataJson,
        string? operationalData,
        string? granularityType,
        int granularity,
        string mode,
        bool confirm,
        string toolName)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(sceneId, "scene_id");
            var normalizedConfigId = NormalizeRequired(twinCategoryConfigId, "twin_category_config_id");
            var op = _backendClient.ResolveOperationalData(operationalData);
            var content = NormalizeContentJson(ledgerDataJson, "ledger_data_json");
            var normalization = NormalizeLedgerTimeContent(content, granularityType, granularity);
            if (normalization.Errors.Length > 0)
            {
                return _results.Failed("Ledger time granularity validation failed. Backend write was not called.", normalization.Errors, new
                {
                    tool = toolName,
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    granularity_type = normalization.GranularityType,
                    granularity = normalization.Granularity,
                    time_rule = normalization.Rule
                });
            }

            content = normalization.Content;
            var body = new Dictionary<string, object?>
            {
                ["content"] = content
            };

            var path = BuildDataPath(op, normalizedSceneId, normalizedConfigId, "add");

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将新增场景孪生体实例台账数据。", new
                {
                    tool = toolName,
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    granularity_type = normalization.GranularityType,
                    granularity = normalization.Granularity,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/add",
                    body,
                    time_normalization = new
                    {
                        rule = normalization.Rule,
                        normalized_fields = normalization.NormalizedFields
                    }
                },
                warnings: normalization.Warnings);
            }

            if (!confirm)
            {
                return _results.Failed("新增实例台账数据属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已新增场景孪生体实例台账数据。",
                new
                {
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    instance = data
                },
                affectedObjects: new object[] { new { object_type = "twin_instance", scene_id = normalizedSceneId, twin_category_config_id = normalizedConfigId, action = "create", data } },
                nextActions: new[] { "从返回值或 list_twin_instances 中取得 twinCategoryDataID，用于后续 update/delete。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("新增实例台账数据失败。", new[] { ex.Message });
        }
    }

    private async Task<McpToolResult> UpdateLedgerDataAsync(
        string sceneId,
        string twinCategoryConfigId,
        string instanceId,
        string ledgerDataJson,
        string? operationalData,
        string? granularityType,
        int granularity,
        string mode,
        bool confirm,
        string toolName)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(sceneId, "scene_id");
            var normalizedConfigId = NormalizeRequired(twinCategoryConfigId, "twin_category_config_id");
            var normalizedInstanceId = NormalizeRequired(instanceId, "instance_id");
            var op = _backendClient.ResolveOperationalData(operationalData);
            var content = NormalizeContentJson(ledgerDataJson, "ledger_data_json");
            var normalization = NormalizeLedgerTimeContent(content, granularityType, granularity);
            if (normalization.Errors.Length > 0)
            {
                return _results.Failed("Ledger time granularity validation failed. Backend write was not called.", normalization.Errors, new
                {
                    tool = toolName,
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    instance_id = normalizedInstanceId,
                    granularity_type = normalization.GranularityType,
                    granularity = normalization.Granularity,
                    time_rule = normalization.Rule
                });
            }

            content = normalization.Content;
            var body = new[]
            {
                new Dictionary<string, object?>
                {
                    ["dataID"] = normalizedInstanceId,
                    ["content"] = content
                }
            };
            var path = BuildDataPath(op, normalizedSceneId, normalizedConfigId, "batchEdit");

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑场景孪生体实例台账数据。", new
                {
                    tool = toolName,
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    instance_id = normalizedInstanceId,
                    granularity_type = normalization.GranularityType,
                    granularity = normalization.Granularity,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/batchEdit",
                    body,
                    time_normalization = new
                    {
                        rule = normalization.Rule,
                        normalized_fields = normalization.NormalizedFields
                    }
                },
                warnings: normalization.Warnings);
            }

            if (!confirm)
            {
                return _results.Failed("编辑实例台账数据属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已编辑场景孪生体实例台账数据。",
                new
                {
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    instance_id = normalizedInstanceId,
                    update_result = data
                },
                affectedObjects: new object[] { new { object_type = "twin_instance", scene_id = normalizedSceneId, twin_category_config_id = normalizedConfigId, instance_id = normalizedInstanceId, action = "update", data } },
                nextActions: new[] { "调用 get_twin_instance 或 list_twin_instances 回读确认。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑实例台账数据失败。", new[] { ex.Message });
        }
    }

    private async Task<McpToolResult> DeleteLedgerDataAsync(
        string sceneId,
        string twinCategoryConfigId,
        string instanceIds,
        string? operationalData,
        string mode,
        bool confirm,
        string toolName)
    {
        try
        {
            var normalizedSceneId = NormalizeRequired(sceneId, "scene_id");
            var normalizedConfigId = NormalizeRequired(twinCategoryConfigId, "twin_category_config_id");
            var ids = ParseIdList(instanceIds, "instance_ids");
            var op = _backendClient.ResolveOperationalData(operationalData);
            var path = BuildDataPath(op, normalizedSceneId, normalizedConfigId, "delete");

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将删除场景孪生体实例台账数据。", new
                {
                    tool = toolName,
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data/delete",
                    instance_ids = ids,
                    body = ids
                },
                warnings: new[] { "删除操作不可自动回滚，请确认实例数据不再需要保留。" });
            }

            if (!confirm)
            {
                return _results.Failed("删除实例台账数据属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, ids, CancellationToken.None);
            return _results.Success(
                "已删除场景孪生体实例台账数据。",
                new
                {
                    operational_data = op,
                    resource_scope = InstanceLedgerScope,
                    scene_id = normalizedSceneId,
                    twin_category_config_id = normalizedConfigId,
                    instance_ids = ids,
                    delete_result = data
                },
                affectedObjects: ids.Select(id => new { object_type = "twin_instance", scene_id = normalizedSceneId, twin_category_config_id = normalizedConfigId, instance_id = id, action = "delete" }).Cast<object>().ToArray(),
                nextActions: new[] { "调用 list_twin_instances 回读确认。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除实例台账数据失败。", new[] { ex.Message });
        }
    }

    private async Task<McpToolResult> ListLedgerDataAsync(
        string sceneId,
        string twinCategoryConfigId,
        string? operationalData,
        string? levelId,
        string? regionId,
        string? parentTwinCategoryConfigId,
        string? parentId,
        string? conditionJson,
        int? page,
        int? pageSize,
        string summary)
    {
        try
        {
            var result = await QueryLedgerDataAsync(
                sceneId,
                twinCategoryConfigId,
                operationalData,
                levelId,
                regionId,
                parentTwinCategoryConfigId,
                parentId,
                conditionJson,
                page,
                pageSize);

            return _results.Success(summary, new
            {
                operational_data = result.OperationalData,
                resource_scope = InstanceLedgerScope,
                scene_id = result.SceneId,
                twin_category_config_id = result.TwinCategoryConfigId,
                backend_api = "POST /v1/{operationalData}/location/{locationId}/twinCategory/{twinCategoryConfigID}/data",
                request = result.RequestBody,
                total = ExtractTotal(result.Data),
                rows = ExtractRowsOrRaw(result.Data)
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询实例台账数据失败。", new[] { ex.Message });
        }
    }

    private async Task<QueryLedgerDataResult> QueryLedgerDataAsync(
        string sceneId,
        string twinCategoryConfigId,
        string? operationalData,
        string? levelId,
        string? regionId,
        string? parentTwinCategoryConfigId,
        string? parentId,
        string? conditionJson,
        int? page,
        int? pageSize)
    {
        var normalizedSceneId = NormalizeRequired(sceneId, "scene_id");
        var normalizedConfigId = NormalizeRequired(twinCategoryConfigId, "twin_category_config_id");
        var op = _backendClient.ResolveOperationalData(operationalData);
        var body = BuildListRequest(levelId, regionId, parentTwinCategoryConfigId, parentId, conditionJson, page, pageSize);
        var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(normalizedSceneId)}/twinCategory/{Uri.EscapeDataString(normalizedConfigId)}/data";
        var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

        return new QueryLedgerDataResult(op, normalizedSceneId, normalizedConfigId, body, data);
    }

    private static Dictionary<string, object?> BuildListRequest(
        string? levelId,
        string? regionId,
        string? parentTwinCategoryConfigId,
        string? parentId,
        string? conditionJson,
        int? page,
        int? pageSize)
    {
        var body = new Dictionary<string, object?>
        {
            ["pageIndex"] = page ?? 1,
            ["pageSize"] = pageSize ?? 0,
            ["isSearchAll"] = page is null && pageSize is null
        };
        AddIfNotBlank(body, "levelId", levelId);
        AddIfNotBlank(body, "regionId", regionId);
        AddIfNotBlank(body, "parentTwinCategoryID", parentTwinCategoryConfigId);
        AddIfNotBlank(body, "parentID", parentId);

        if (!string.IsNullOrWhiteSpace(conditionJson))
        {
            body["condition"] = ParseJsonObject(conditionJson, "condition_json");
        }

        return body;
    }

    private static string BuildDataPath(string operationalData, string sceneId, string twinCategoryConfigId, string action)
    {
        return $"/v1/{Uri.EscapeDataString(operationalData)}/location/{Uri.EscapeDataString(sceneId)}/twinCategory/{Uri.EscapeDataString(twinCategoryConfigId)}/data/{action}";
    }

    private static JsonElement? FindInstance(JsonElement data, string instanceId)
    {
        if (TryGetRowsArray(data, out var rows))
        {
            foreach (var row in rows.EnumerateArray())
            {
                var rowId = ExtractString(row, "twinCategoryDataID") ?? ExtractString(row, "twinCategoryDataId");
                var instanceNumber = ExtractString(row, "instanceNumber");
                if (string.Equals(rowId, instanceId, StringComparison.Ordinal) ||
                    string.Equals(instanceNumber, instanceId, StringComparison.Ordinal))
                {
                    return row.Clone();
                }
            }
        }

        return null;
    }

    private static object ExtractRowsOrRaw(JsonElement data)
    {
        if (TryGetRowsArray(data, out var rows))
        {
            return rows.Clone();
        }

        return data.Clone();
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

    private static string NormalizeContentJson(string rawJson, string parameterName)
    {
        var normalized = NormalizeRequired(rawJson, parameterName);
        try
        {
            var node = JsonNode.Parse(normalized);
            if (node is null)
            {
                throw new InvalidOperationException($"{parameterName} cannot be JSON null.");
            }

            if (node is not JsonObject)
            {
                throw new InvalidOperationException($"{parameterName} must be a JSON object, not an array, string, or number.");
            }

            return node.ToJsonString(JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{parameterName} 必须是合法 JSON。", ex);
        }
    }


    private static LedgerTimeNormalization NormalizeLedgerTimeContent(string normalizedContentJson, string? granularityType, int granularity)
    {
        var type = NormalizeGranularityType(granularityType);
        var errors = new List<string>();
        var warnings = new List<string>
        {
            "ledger_data_json must use backend field names returned by list_twin_ledger_fields.",
            "MCP does not auto-generate longitude/latitude or position fields. Provide them explicitly in ledger_data_json when needed."
        };

        if (type is null)
        {
            return new LedgerTimeNormalization(
                normalizedContentJson,
                granularityType?.Trim() ?? "Year",
                granularity,
                "Ledger granularity supports Year/Month/Day only.",
                Array.Empty<string>(),
                new[] { "granularity_type supports Year, Month, Day, or Chinese year/month/day only." },
                warnings.ToArray());
        }

        if (!TryBuildLedgerTime(type, granularity, DateTime.Now, out var normalizedTime, out var rule, out var error))
        {
            return new LedgerTimeNormalization(
                normalizedContentJson,
                type,
                granularity,
                rule,
                Array.Empty<string>(),
                new[] { error },
                warnings.ToArray());
        }

        var root = JsonNode.Parse(normalizedContentJson)!.AsObject();
        var fields = NormalizeTimeFields(root, normalizedTime);
        if (fields.Count == 0)
        {
            warnings.Add($"No instanceTime/time field was found. If backend requires instanceTime, pass it in ledger_data_json; MCP will normalize it to {normalizedTime} for {type} granularity.");
        }

        return new LedgerTimeNormalization(
            root.ToJsonString(JsonOptions),
            type,
            granularity,
            rule,
            fields.ToArray(),
            Array.Empty<string>(),
            warnings.ToArray());
    }

    private static string? NormalizeGranularityType(string? granularityType)
    {
        var type = string.IsNullOrWhiteSpace(granularityType) ? "Year" : granularityType.Trim();
        return type.ToLowerInvariant() switch
        {
            "year" or "y" or "\u5E74" => "Year",
            "month" or "m" or "\u6708" => "Month",
            "day" or "d" or "\u65E5" => "Day",
            _ => null
        };
    }

    private static bool TryBuildLedgerTime(string granularityType, int granularity, DateTime now, out string value, out string rule, out string error)
    {
        error = string.Empty;
        if (granularityType == "Year")
        {
            rule = "Year granularity: use current year; granularity is month number. 1 => Jan 1, 2 => Feb 1.";
            if (granularity < 1 || granularity > 12)
            {
                value = string.Empty;
                error = "For Year granularity, granularity must be a month number from 1 to 12.";
                return false;
            }

            value = new DateTime(now.Year, granularity, 1, 0, 0, 0).ToString("yyyy-MM-dd HH:mm:ss");
            return true;
        }

        if (granularityType == "Month")
        {
            rule = "Month granularity: use current year and month; granularity is day number.";
            var maxDay = DateTime.DaysInMonth(now.Year, now.Month);
            if (granularity < 1 || granularity > maxDay)
            {
                value = string.Empty;
                error = $"For Month granularity, granularity must be a day number in current month, from 1 to {maxDay}.";
                return false;
            }

            value = new DateTime(now.Year, now.Month, granularity, 0, 0, 0).ToString("yyyy-MM-dd HH:mm:ss");
            return true;
        }

        rule = "Day granularity: use current date; granularity is hour number.";
        if (granularity < 0 || granularity > 23)
        {
            value = string.Empty;
            error = "For Day granularity, granularity must be an hour number from 0 to 23.";
            return false;
        }

        value = new DateTime(now.Year, now.Month, now.Day, granularity, 0, 0).ToString("yyyy-MM-dd HH:mm:ss");
        return true;
    }

    private static List<string> NormalizeTimeFields(JsonNode node, string normalizedTime, string prefix = "")
    {
        var fields = new List<string>();
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                var path = string.IsNullOrWhiteSpace(prefix) ? property.Key : $"{prefix}.{property.Key}";
                if (IsLedgerTimeField(property.Key))
                {
                    obj[property.Key] = normalizedTime;
                    fields.Add(path);
                    continue;
                }

                if (property.Value is JsonObject or JsonArray)
                {
                    fields.AddRange(NormalizeTimeFields(property.Value, normalizedTime, path));
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                if (array[index] is { } child)
                {
                    fields.AddRange(NormalizeTimeFields(child, normalizedTime, $"{prefix}[{index}]"));
                }
            }
        }

        return fields;
    }

    private static bool IsLedgerTimeField(string key)
    {
        var lower = key.ToLowerInvariant();
        return lower == "instancetime" || lower.EndsWith("time", StringComparison.Ordinal) || key.Contains("\u65F6\u95F4", StringComparison.Ordinal);
    }

    private static JsonNode ParseJsonObject(string rawJson, string parameterName)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is JsonObject)
            {
                return node;
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{parameterName} 必须是合法 JSON 对象。", ex);
        }

        throw new InvalidOperationException($"{parameterName} 必须是 JSON 对象。");
    }

    private static string[] ParseIdList(string rawIds, string parameterName)
    {
        var normalized = NormalizeRequired(rawIds, parameterName);
        var ids = normalized
            .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids.Length == 0)
        {
            throw new InvalidOperationException($"{parameterName} 至少需要包含一个有效 ID。");
        }

        return ids;
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

    private sealed record QueryLedgerDataResult(
        string OperationalData,
        string SceneId,
        string TwinCategoryConfigId,
        Dictionary<string, object?> RequestBody,
        JsonElement Data);

    private sealed record LedgerTimeNormalization(
        string Content,
        string GranularityType,
        int Granularity,
        string Rule,
        string[] NormalizedFields,
        string[] Errors,
        string[] Warnings);
}
