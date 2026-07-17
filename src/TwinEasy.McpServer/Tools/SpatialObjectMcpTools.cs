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
/// 空间对象类 Tool：V1.0 先实现兴趣点 POI，后续再扩展线、面。
/// </summary>
[McpServerToolType]
public sealed class SpatialObjectMcpTools
{
    private static readonly JsonSerializerOptions ContentJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly TwinBackendClient _backendClient;
    private readonly TwinToolResultFactory _results;

    public SpatialObjectMcpTools(TwinBackendClient backendClient, TwinToolResultFactory results)
    {
        _backendClient = backendClient;
        _results = results;
    }

    /// <summary>
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/{levelId}/position/add。
    /// 后端 body 是数组，且只接收 name/content；content 必须符合孪易前端 POI 扁平结构，否则可能写入成功但场景里不显示。
    /// </summary>
    [McpServerTool]
    [Description("在场景层级上创建兴趣点 POI。默认生成孪易前端可显示的 content；也可直接传 content_json。")]
    public async Task<McpToolResult> create_scene_poi(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("POI 名称。")] string name,
        [Description("GeoJSON 风格几何信息 JSON 字符串，通常为 Point；传 content_json 时可不传。")] string? geometry_json = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("样式 JSON 字符串，可选。")] string? style_json = null,
        [Description("扩展属性 JSON 字符串，可选。")] string? properties_json = null,
        [Description("完整孪易 POI content JSON；传入后原样写入，优先级高于 geometry_json。")] string? content_json = null,
        [Description("执行模式：preview 或 execute。")] string mode = "execute",
        [Description("execute 写入时必须为 true。")] bool confirm = true)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var content = ResolvePoiContent(
                currentContent: null,
                currentPoiId: null,
                contentJson: content_json,
                geometryJson: geometry_json,
                styleJson: style_json,
                propertiesJson: properties_json,
                name: name);
            if (string.IsNullOrWhiteSpace(content))
            {
                return _results.Failed("创建兴趣点失败：请传入 content_json，或传入 geometry_json 坐标生成孪易 POI content。");
            }

            var body = new[]
            {
                new
                {
                    name,
                    content
                }
            };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将创建场景兴趣点。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/level/{levelId}/position/add",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("创建兴趣点属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/level/{Uri.EscapeDataString(hierarchy_id)}/position/add";
            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);

            return _results.Success(
                "已创建场景兴趣点。",
                new
                {
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    pois = data
                },
                affectedObjects: new object[] { new { object_type = "scene_poi", name, data } },
                nextActions: new[] { "调用 list_scene_pois 或 get_scene_poi 回读兴趣点。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("创建场景兴趣点失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端映射：GET /v1/{operationalData}/location/{locationId}/level/{levelId}/position。
    /// keyword 是 MCP Server 侧过滤，因为后端该接口没有搜索参数。
    /// </summary>
    [McpServerTool]
    [Description("查询场景层级下的兴趣点列表。")]
    public async Task<McpToolResult> list_scene_pois(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("客户端过滤关键字，按 positionName/name/content 包含关系过滤。")] string? keyword = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var data = await ListPoisRawAsync(op, scene_id, hierarchy_id);
            object output = data;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                output = FilterJsonArray(data, keyword);
            }

            return _results.Success("已查询场景兴趣点列表。", new
            {
                operational_data = op,
                scene_id,
                hierarchy_id,
                backend_api = "GET /v1/{operationalData}/location/{locationId}/level/{levelId}/position",
                pois = output
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("查询场景兴趣点列表失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 后端没有 POI 单查接口；这里复用列表接口，再按 positionID 做本地过滤。
    /// </summary>
    [McpServerTool]
    [Description("按 POI ID 回读兴趣点。后端无单查接口，本 Tool 使用列表接口后按 positionID 过滤。")]
    public async Task<McpToolResult> get_scene_poi(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("POI ID，对应后端 positionID。")] string poi_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var data = await ListPoisRawAsync(op, scene_id, hierarchy_id);
            var match = FindByProperty(data, "positionID", poi_id);

            if (match is null)
            {
                return _results.Failed("未找到指定兴趣点。", data: new
                {
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    poi_id,
                    backend_api = "GET /v1/{operationalData}/location/{locationId}/level/{levelId}/position"
                });
            }

            return _results.Success("已回读场景兴趣点。", new
            {
                operational_data = op,
                scene_id,
                hierarchy_id,
                poi_id,
                poi = match
            });
        }
        catch (Exception ex)
        {
            return _results.Failed("回读场景兴趣点失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 编辑兴趣点位 POI。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/position/{positionId}/edit。
    /// 后端请求体使用 LocationRequest：name、content；name 必填，所以未传 name 时会先回读当前 POI 名称。
    /// </summary>
    [McpServerTool]
    [Description("编辑兴趣点 POI。可改名称、完整 content，或用 geometry/style/properties 重新组装 content。默认 preview。")]
    public async Task<McpToolResult> update_scene_poi(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前 POI。")] string hierarchy_id,
        [Description("POI ID，对应后端 positionID。")] string poi_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("新的 POI 名称；不传则保留当前名称。")] string? name = null,
        [Description("完整 content JSON；优先级高于 geometry/style/properties。")] string? content_json = null,
        [Description("GeoJSON 风格几何信息 JSON；传入后会与 style/properties 组装成 content。")] string? geometry_json = null,
        [Description("样式 JSON，可选；仅在传 geometry_json 时参与组装 content。")] string? style_json = null,
        [Description("扩展属性 JSON，可选；仅在传 geometry_json 时参与组装 content。")] string? properties_json = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentPoi = await GetPoiRawAsync(op, scene_id, hierarchy_id, poi_id);
            if (currentPoi is null)
            {
                return _results.Failed("编辑兴趣点失败：未在当前层级兴趣点列表中找到指定 poi_id。");
            }

            var resolvedName = name ?? ExtractString(currentPoi.Value, "positionName") ?? ExtractString(currentPoi.Value, "name");
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                return _results.Failed("编辑兴趣点失败：后端要求 name 必填，但当前 POI 未返回 positionName/name，请显式传入 name。");
            }

            var resolvedContent = ResolvePoiContent(
                ExtractString(currentPoi.Value, "content"),
                poi_id,
                content_json,
                geometry_json,
                style_json,
                properties_json,
                resolvedName);

            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/position/{Uri.EscapeDataString(poi_id)}/edit";
            var body = new
            {
                name = resolvedName.Trim(),
                content = resolvedContent
            };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将编辑场景兴趣点。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/position/{positionId}/edit",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    poi_id,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("编辑兴趣点属于写操作，请确认拟写入内容后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已编辑场景兴趣点。",
                new { operational_data = op, scene_id, hierarchy_id, poi_id, poi = data },
                affectedObjects: new object[] { new { object_type = "scene_poi", scene_id, hierarchy_id, poi_id, action = "update", data } },
                nextActions: new[] { "调用 get_scene_poi 回读兴趣点详情，确认内容已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("编辑场景兴趣点失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 重命名兴趣点位 POI。
    /// 后端没有独立 rename 接口，这里复用 POST /v1/{operationalData}/location/{locationId}/position/{positionId}/edit。
    /// </summary>
    [McpServerTool]
    [Description("重命名兴趣点 POI。复用编辑接口，只修改名称并保留当前 content。默认 preview。")]
    public async Task<McpToolResult> rename_scene_poi(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前 POI。")] string hierarchy_id,
        [Description("POI ID，对应后端 positionID。")] string poi_id,
        [Description("新的 POI 名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return _results.Failed("重命名兴趣点失败：name 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentPoi = await GetPoiRawAsync(op, scene_id, hierarchy_id, poi_id);
            if (currentPoi is null)
            {
                return _results.Failed("重命名兴趣点失败：未在当前层级兴趣点列表中找到指定 poi_id。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/position/{Uri.EscapeDataString(poi_id)}/edit";
            var body = new
            {
                name = name.Trim(),
                content = RenamePoiContent(ExtractString(currentPoi.Value, "content"), name.Trim())
            };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将重命名场景兴趣点。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/position/{positionId}/edit",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    poi_id,
                    old_name = ExtractString(currentPoi.Value, "positionName") ?? ExtractString(currentPoi.Value, "name"),
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("重命名兴趣点属于写操作，请确认新名称后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已重命名场景兴趣点。",
                new { operational_data = op, scene_id, hierarchy_id, poi_id, poi = data },
                affectedObjects: new object[] { new { object_type = "scene_poi", scene_id, hierarchy_id, poi_id, action = "rename", name = name.Trim(), data } },
                nextActions: new[] { "调用 get_scene_poi 回读兴趣点详情，确认名称已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed("重命名场景兴趣点失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 删除兴趣点位 POI。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/position/delete，body 为 POI ID 数组。
    /// 删除属于高风险操作：execute 时需要确认当前 POI 名称。
    /// </summary>
    [McpServerTool]
    [Description("删除兴趣点 POI。默认 preview；真正删除必须 mode=execute、confirm=true，并确认当前 POI 名称。")]
    public async Task<McpToolResult> delete_scene_poi(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前 POI。")] string hierarchy_id,
        [Description("POI ID，对应后端 positionID。")] string poi_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。删除默认 preview，避免误删。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false,
        [Description("删除二次确认名称；execute 删除时必须传入当前 POI 名称。")] string? confirm_poi_name = null)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operational_data);
            var currentPoi = await GetPoiRawAsync(op, scene_id, hierarchy_id, poi_id);
            if (currentPoi is null)
            {
                return _results.Failed("删除兴趣点失败：未在当前层级兴趣点列表中找到指定 poi_id。");
            }

            var poiName = ExtractString(currentPoi.Value, "positionName") ?? ExtractString(currentPoi.Value, "name") ?? string.Empty;
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(scene_id)}/position/delete";
            var body = new[] { poi_id };

            if (IsPreview(mode))
            {
                return _results.Preview("预览：将删除场景兴趣点。请确认 POI 名称后再执行删除。", new
                {
                    backend_api = "POST /v1/{operationalData}/location/{locationId}/position/delete",
                    operational_data = op,
                    scene_id,
                    hierarchy_id,
                    poi_id,
                    poi_name = poiName,
                    confirm_required = true,
                    confirm_poi_name_required = poiName,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed("删除兴趣点属于高风险操作，请先确认目标 POI 后使用 mode=execute、confirm=true，并传入 confirm_poi_name。");
            }

            if (string.IsNullOrWhiteSpace(poiName))
            {
                return _results.Failed("删除兴趣点失败：无法读取当前 POI 名称，不能进行名称确认删除。");
            }

            if (!string.Equals(poiName, confirm_poi_name?.Trim(), StringComparison.Ordinal))
            {
                return _results.Failed($"删除兴趣点二次确认失败：confirm_poi_name 必须和当前 POI 名称完全一致。当前 POI 名称：{poiName}");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                "已删除场景兴趣点。",
                new { operational_data = op, scene_id, hierarchy_id, poi_id, poi_name = poiName, delete_result = data },
                affectedObjects: new object[] { new { object_type = "scene_poi", scene_id, hierarchy_id, poi_id, poi_name = poiName, action = "delete", data } });
        }
        catch (Exception ex)
        {
            return _results.Failed("删除场景兴趣点失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 创建指引路线。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/{levelId}/path/add。
    /// content 按孪易前端路线结构生成，支持直接传完整 content_json。
    /// </summary>
    [McpServerTool]
    [Description("创建指引路线。默认根据 LineString 坐标生成孪易前端可显示的路线 content；也可直接传 content_json。")]
    public async Task<McpToolResult> create_guide_route(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("路线名称。")] string name,
        [Description("路线几何 JSON，支持 GeoJSON LineString 或坐标数组 [[lng,lat,z?],...]；传 content_json 时可不传。")] string? geometry_json = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("样式 JSON，可覆盖 type、texture、color、width、autoScale 等字段。")] string? style_json = null,
        [Description("扩展属性 JSON，可覆盖 id、tag、snapSurface、lineDataId 等字段。")] string? properties_json = null,
        [Description("完整孪易路线 content JSON；传入后原样写入，优先级高于 geometry_json。")] string? content_json = null,
        [Description("执行模式：preview 或 execute。")] string mode = "execute",
        [Description("execute 写入时必须为 true。")] bool confirm = true)
    {
        return await CreateLinearSpatialObjectAsync("指引路线", "guide_route", "path", scene_id, hierarchy_id, name, geometry_json, operational_data, style_json, properties_json, content_json, mode, confirm);
    }

    /// <summary>
    /// 查询指定层级下的指引路线列表。
    /// 后端映射：GET /v1/{operationalData}/location/{locationId}/level/{levelId}/path。
    /// </summary>
    [McpServerTool]
    [Description("查询场景层级下的指引路线列表。")]
    public async Task<McpToolResult> list_guide_routes(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("客户端过滤关键字，按 pathName/name/content 包含关系过滤。")] string? keyword = null)
    {
        return await ListLinearSpatialObjectsAsync("指引路线", "path", scene_id, hierarchy_id, operational_data, keyword);
    }

    /// <summary>
    /// 回读单条指引路线。后端没有单查接口，使用列表接口后按 pathID 过滤。
    /// </summary>
    [McpServerTool]
    [Description("按路线 ID 回读指引路线。后端无单查接口，本 Tool 使用列表接口后按 pathID 过滤。")]
    public async Task<McpToolResult> get_guide_route(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("路线 ID，对应后端 pathID。")] string route_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        return await GetLinearSpatialObjectAsync("指引路线", "path", "pathID", scene_id, hierarchy_id, route_id, operational_data);
    }

    /// <summary>
    /// 编辑指引路线。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/path/{pathId}/edit。
    /// </summary>
    [McpServerTool]
    [Description("编辑指引路线。可改名称、完整 content，或用 LineString 坐标重新组装 content。默认 preview。")]
    public async Task<McpToolResult> update_guide_route(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前路线。")] string hierarchy_id,
        [Description("路线 ID，对应后端 pathID。")] string route_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("新的路线名称；不传则保留当前名称。")] string? name = null,
        [Description("完整 content JSON；优先级高于 geometry/style/properties。")] string? content_json = null,
        [Description("路线几何 JSON，支持 GeoJSON LineString 或坐标数组 [[lng,lat,z?],...]。")] string? geometry_json = null,
        [Description("样式 JSON，可选；仅在传 geometry_json 时参与组装 content。")] string? style_json = null,
        [Description("扩展属性 JSON，可选；仅在传 geometry_json 时参与组装 content。")] string? properties_json = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await UpdateLinearSpatialObjectAsync("指引路线", "guide_route", "path", "pathID", "pathName", scene_id, hierarchy_id, route_id, operational_data, name, content_json, geometry_json, style_json, properties_json, mode, confirm);
    }

    /// <summary>
    /// 重命名指引路线。后端没有独立 rename 接口，这里复用路线编辑接口。
    /// </summary>
    [McpServerTool]
    [Description("重命名指引路线。复用编辑接口，只修改名称并同步 content.name。默认 preview。")]
    public async Task<McpToolResult> rename_guide_route(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前路线。")] string hierarchy_id,
        [Description("路线 ID，对应后端 pathID。")] string route_id,
        [Description("新的路线名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await RenameLinearSpatialObjectAsync("指引路线", "guide_route", "path", "pathID", "pathName", scene_id, hierarchy_id, route_id, name, operational_data, mode, confirm);
    }

    /// <summary>
    /// 删除指引路线。删除属于高风险操作，execute 时需要确认当前路线名称。
    /// </summary>
    [McpServerTool]
    [Description("删除指引路线。默认 preview；真正删除必须 mode=execute、confirm=true，并确认当前路线名称。")]
    public async Task<McpToolResult> delete_guide_route(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前路线。")] string hierarchy_id,
        [Description("路线 ID，对应后端 pathID。")] string route_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。删除默认 preview，避免误删。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false,
        [Description("删除二次确认名称；execute 删除时必须传入当前路线名称。")] string? confirm_route_name = null)
    {
        return await DeleteLinearSpatialObjectAsync("指引路线", "guide_route", "path", "pathID", "pathName", scene_id, hierarchy_id, route_id, operational_data, mode, confirm, confirm_route_name);
    }

    /// <summary>
    /// 创建重点区域。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/level/{levelId}/region/add。
    /// content 按孪易前端区域结构生成，支持直接传完整 content_json。
    /// </summary>
    [McpServerTool]
    [Description("创建重点区域。默认根据 Polygon 坐标生成孪易前端可显示的区域 content；也可直接传 content_json。")]
    public async Task<McpToolResult> create_key_area(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("区域名称。")] string name,
        [Description("区域几何 JSON，支持 GeoJSON Polygon 或坐标数组 [[lng,lat,z?],...]；传 content_json 时可不传。")] string? geometry_json = null,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("样式 JSON，可覆盖 type、color、alpha、areaHeight、fillArea 等字段。")] string? style_json = null,
        [Description("扩展属性 JSON，可覆盖 id、tag、coordZ、fillPosition 等字段。")] string? properties_json = null,
        [Description("完整孪易区域 content JSON；传入后原样写入，优先级高于 geometry_json。")] string? content_json = null,
        [Description("执行模式：preview 或 execute。")] string mode = "execute",
        [Description("execute 写入时必须为 true。")] bool confirm = true)
    {
        return await CreateLinearSpatialObjectAsync("重点区域", "key_area", "region", scene_id, hierarchy_id, name, geometry_json, operational_data, style_json, properties_json, content_json, mode, confirm);
    }

    /// <summary>
    /// 查询指定层级下的重点区域列表。
    /// 后端映射：GET /v1/{operationalData}/location/{locationId}/level/{levelId}/region。
    /// </summary>
    [McpServerTool]
    [Description("查询场景层级下的重点区域列表。")]
    public async Task<McpToolResult> list_key_areas(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("客户端过滤关键字，按 regionName/name/content 包含关系过滤。")] string? keyword = null)
    {
        return await ListLinearSpatialObjectsAsync("重点区域", "region", scene_id, hierarchy_id, operational_data, keyword);
    }

    /// <summary>
    /// 回读单个重点区域。后端没有单查接口，使用列表接口后按 regionID 过滤。
    /// </summary>
    [McpServerTool]
    [Description("按区域 ID 回读重点区域。后端无单查接口，本 Tool 使用列表接口后按 regionID 过滤。")]
    public async Task<McpToolResult> get_key_area(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId。")] string hierarchy_id,
        [Description("区域 ID，对应后端 regionID。")] string area_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null)
    {
        return await GetLinearSpatialObjectAsync("重点区域", "region", "regionID", scene_id, hierarchy_id, area_id, operational_data);
    }

    /// <summary>
    /// 编辑重点区域。
    /// 后端映射：POST /v1/{operationalData}/location/{locationId}/region/{regionId}/edit。
    /// </summary>
    [McpServerTool]
    [Description("编辑重点区域。可改名称、完整 content，或用 Polygon 坐标重新组装 content。默认 preview。")]
    public async Task<McpToolResult> update_key_area(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前区域。")] string hierarchy_id,
        [Description("区域 ID，对应后端 regionID。")] string area_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("新的区域名称；不传则保留当前名称。")] string? name = null,
        [Description("完整 content JSON；优先级高于 geometry/style/properties。")] string? content_json = null,
        [Description("区域几何 JSON，支持 GeoJSON Polygon 或坐标数组 [[lng,lat,z?],...]。")] string? geometry_json = null,
        [Description("样式 JSON，可选；仅在传 geometry_json 时参与组装 content。")] string? style_json = null,
        [Description("扩展属性 JSON，可选；仅在传 geometry_json 时参与组装 content。")] string? properties_json = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await UpdateLinearSpatialObjectAsync("重点区域", "key_area", "region", "regionID", "regionName", scene_id, hierarchy_id, area_id, operational_data, name, content_json, geometry_json, style_json, properties_json, mode, confirm);
    }

    /// <summary>
    /// 重命名重点区域。后端没有独立 rename 接口，这里复用区域编辑接口。
    /// </summary>
    [McpServerTool]
    [Description("重命名重点区域。复用编辑接口，只修改名称并同步 content.name。默认 preview。")]
    public async Task<McpToolResult> rename_key_area(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前区域。")] string hierarchy_id,
        [Description("区域 ID，对应后端 regionID。")] string area_id,
        [Description("新的区域名称。")] string name,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。默认 preview。")] string mode = "preview",
        [Description("execute 写入时必须为 true。")] bool confirm = false)
    {
        return await RenameLinearSpatialObjectAsync("重点区域", "key_area", "region", "regionID", "regionName", scene_id, hierarchy_id, area_id, name, operational_data, mode, confirm);
    }

    /// <summary>
    /// 删除重点区域。删除属于高风险操作，execute 时需要确认当前区域名称。
    /// </summary>
    [McpServerTool]
    [Description("删除重点区域。默认 preview；真正删除必须 mode=execute、confirm=true，并确认当前区域名称。")]
    public async Task<McpToolResult> delete_key_area(
        [Description("场景/地点 ID，对应后端 locationId。")] string scene_id,
        [Description("层级 ID，对应后端 levelId，用于先回读当前区域。")] string hierarchy_id,
        [Description("区域 ID，对应后端 regionID。")] string area_id,
        [Description("操作域，通常为 UserData 或 IndustryData；不传使用配置默认值。")] string? operational_data = null,
        [Description("执行模式：preview 或 execute。删除默认 preview，避免误删。")] string mode = "preview",
        [Description("execute 删除时必须为 true。")] bool confirm = false,
        [Description("删除二次确认名称；execute 删除时必须传入当前区域名称。")] string? confirm_area_name = null)
    {
        return await DeleteLinearSpatialObjectAsync("重点区域", "key_area", "region", "regionID", "regionName", scene_id, hierarchy_id, area_id, operational_data, mode, confirm, confirm_area_name);
    }

    /// <summary>
    /// 创建线/面类空间对象的通用入口，目前用于指引路线 path 和重点区域 region。
    /// 负责解析操作域、生成孪易前端可识别的 content，并调用对应的 add 后端接口。
    /// </summary>
    private async Task<McpToolResult> CreateLinearSpatialObjectAsync(
        string displayName,
        string objectType,
        string backendObject,
        string sceneId,
        string hierarchyId,
        string name,
        string? geometryJson,
        string? operationalData,
        string? styleJson,
        string? propertiesJson,
        string? contentJson,
        string mode,
        bool confirm)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operationalData);
            var content = ResolveLinearSpatialContent(backendObject, null, null, contentJson, geometryJson, styleJson, propertiesJson, name);
            if (string.IsNullOrWhiteSpace(content))
            {
                return _results.Failed($"创建{displayName}失败：请传入 content_json，或传入 geometry_json 坐标生成孪易 {displayName} content。");
            }

            var body = new[] { new { name, content } };
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(sceneId)}/level/{Uri.EscapeDataString(hierarchyId)}/{backendObject}/add";

            if (IsPreview(mode))
            {
                return _results.Preview($"预览：将创建场景{displayName}。", new
                {
                    backend_api = $"POST /v1/{{operationalData}}/location/{{locationId}}/level/{{levelId}}/{backendObject}/add",
                    operational_data = op,
                    scene_id = sceneId,
                    hierarchy_id = hierarchyId,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed($"创建{displayName}属于写操作，execute 模式下 confirm 必须为 true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                $"已创建场景{displayName}。",
                new { operational_data = op, scene_id = sceneId, hierarchy_id = hierarchyId, result = data },
                affectedObjects: new object[] { new { object_type = objectType, name, data } },
                nextActions: new[] { $"调用 list_{objectType}s 或 get_{objectType} 回读{displayName}。" });
        }
        catch (Exception ex)
        {
            return _results.Failed($"创建场景{displayName}失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 查询线/面类空间对象列表的通用入口。
    /// 后端没有 keyword 参数，所以关键字过滤统一在 MCP Server 侧完成。
    /// </summary>
    private async Task<McpToolResult> ListLinearSpatialObjectsAsync(
        string displayName,
        string backendObject,
        string sceneId,
        string hierarchyId,
        string? operationalData,
        string? keyword)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operationalData);
            var data = await ListLinearSpatialObjectsRawAsync(op, sceneId, hierarchyId, backendObject);
            object output = string.IsNullOrWhiteSpace(keyword) ? data : FilterJsonArray(data, keyword);

            return _results.Success($"已查询场景{displayName}列表。", new
            {
                operational_data = op,
                scene_id = sceneId,
                hierarchy_id = hierarchyId,
                backend_api = $"GET /v1/{{operationalData}}/location/{{locationId}}/level/{{levelId}}/{backendObject}",
                items = output
            });
        }
        catch (Exception ex)
        {
            return _results.Failed($"查询场景{displayName}列表失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 查询单个线/面类空间对象的通用入口。
    /// 后端没有单对象详情接口，这里先查列表，再按 pathID/regionID 本地过滤。
    /// </summary>
    private async Task<McpToolResult> GetLinearSpatialObjectAsync(
        string displayName,
        string backendObject,
        string idProperty,
        string sceneId,
        string hierarchyId,
        string objectId,
        string? operationalData)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operationalData);
            var current = await GetLinearSpatialObjectRawAsync(op, sceneId, hierarchyId, backendObject, idProperty, objectId);
            if (current is null)
            {
                return _results.Failed($"未找到指定{displayName}。", data: new
                {
                    operational_data = op,
                    scene_id = sceneId,
                    hierarchy_id = hierarchyId,
                    object_id = objectId,
                    backend_api = $"GET /v1/{{operationalData}}/location/{{locationId}}/level/{{levelId}}/{backendObject}"
                });
            }

            return _results.Success($"已回读场景{displayName}。", new
            {
                operational_data = op,
                scene_id = sceneId,
                hierarchy_id = hierarchyId,
                object_id = objectId,
                item = current
            });
        }
        catch (Exception ex)
        {
            return _results.Failed($"回读场景{displayName}失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 编辑线/面类空间对象的通用入口。
    /// 支持直接写完整 content_json，也支持根据 geometry/style/properties 重新生成 content。
    /// </summary>
    private async Task<McpToolResult> UpdateLinearSpatialObjectAsync(
        string displayName,
        string objectType,
        string backendObject,
        string idProperty,
        string nameProperty,
        string sceneId,
        string hierarchyId,
        string objectId,
        string? operationalData,
        string? name,
        string? contentJson,
        string? geometryJson,
        string? styleJson,
        string? propertiesJson,
        string mode,
        bool confirm)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operationalData);
            var current = await GetLinearSpatialObjectRawAsync(op, sceneId, hierarchyId, backendObject, idProperty, objectId);
            if (current is null)
            {
                return _results.Failed($"编辑{displayName}失败：未在当前层级列表中找到指定 ID。");
            }

            var resolvedName = name ?? ExtractString(current.Value, nameProperty) ?? ExtractNameFromContent(ExtractString(current.Value, "content"));
            if (string.IsNullOrWhiteSpace(resolvedName))
            {
                return _results.Failed($"编辑{displayName}失败：后端要求 name 必填，但当前对象未返回名称，请显式传入 name。");
            }

            var currentContent = ExtractString(current.Value, "content");
            var content = ResolveLinearSpatialContent(backendObject, currentContent, objectId, contentJson, geometryJson, styleJson, propertiesJson, resolvedName);
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(sceneId)}/{backendObject}/{Uri.EscapeDataString(objectId)}/edit";
            var body = new { name = resolvedName.Trim(), content };

            if (IsPreview(mode))
            {
                return _results.Preview($"预览：将编辑场景{displayName}。", new
                {
                    backend_api = $"POST /v1/{{operationalData}}/location/{{locationId}}/{backendObject}/{{objectId}}/edit",
                    operational_data = op,
                    scene_id = sceneId,
                    hierarchy_id = hierarchyId,
                    object_id = objectId,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed($"编辑{displayName}属于写操作，请确认拟写入内容后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                $"已编辑场景{displayName}。",
                new { operational_data = op, scene_id = sceneId, hierarchy_id = hierarchyId, object_id = objectId, item = data },
                affectedObjects: new object[] { new { object_type = objectType, scene_id = sceneId, hierarchy_id = hierarchyId, object_id = objectId, action = "update", data } },
                nextActions: new[] { $"调用 get_{objectType} 回读{displayName}详情，确认内容已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed($"编辑场景{displayName}失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 重命名线/面类空间对象的通用入口。
    /// 后端没有独立 rename 接口，这里复用 edit 接口，并同步 content.name。
    /// </summary>
    private async Task<McpToolResult> RenameLinearSpatialObjectAsync(
        string displayName,
        string objectType,
        string backendObject,
        string idProperty,
        string nameProperty,
        string sceneId,
        string hierarchyId,
        string objectId,
        string name,
        string? operationalData,
        string mode,
        bool confirm)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return _results.Failed($"重命名{displayName}失败：name 不能为空。");
            }

            var op = _backendClient.ResolveOperationalData(operationalData);
            var current = await GetLinearSpatialObjectRawAsync(op, sceneId, hierarchyId, backendObject, idProperty, objectId);
            if (current is null)
            {
                return _results.Failed($"重命名{displayName}失败：未在当前层级列表中找到指定 ID。");
            }

            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(sceneId)}/{backendObject}/{Uri.EscapeDataString(objectId)}/edit";
            var body = new
            {
                name = name.Trim(),
                content = RenameFlatContent(ExtractString(current.Value, "content"), name.Trim())
            };

            if (IsPreview(mode))
            {
                return _results.Preview($"预览：将重命名场景{displayName}。", new
                {
                    backend_api = $"POST /v1/{{operationalData}}/location/{{locationId}}/{backendObject}/{{objectId}}/edit",
                    operational_data = op,
                    scene_id = sceneId,
                    hierarchy_id = hierarchyId,
                    object_id = objectId,
                    old_name = ExtractString(current.Value, nameProperty) ?? ExtractNameFromContent(ExtractString(current.Value, "content")),
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed($"重命名{displayName}属于写操作，请确认新名称后使用 mode=execute 且 confirm=true。");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                $"已重命名场景{displayName}。",
                new { operational_data = op, scene_id = sceneId, hierarchy_id = hierarchyId, object_id = objectId, item = data },
                affectedObjects: new object[] { new { object_type = objectType, scene_id = sceneId, hierarchy_id = hierarchyId, object_id = objectId, action = "rename", name = name.Trim(), data } },
                nextActions: new[] { $"调用 get_{objectType} 回读{displayName}详情，确认名称已更新。" });
        }
        catch (Exception ex)
        {
            return _results.Failed($"重命名场景{displayName}失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 删除线/面类空间对象的通用入口。
    /// 删除前先回读对象名称，execute 时必须按名称二次确认，避免只看 ID 误删。
    /// </summary>
    private async Task<McpToolResult> DeleteLinearSpatialObjectAsync(
        string displayName,
        string objectType,
        string backendObject,
        string idProperty,
        string nameProperty,
        string sceneId,
        string hierarchyId,
        string objectId,
        string? operationalData,
        string mode,
        bool confirm,
        string? confirmName)
    {
        try
        {
            var op = _backendClient.ResolveOperationalData(operationalData);
            var current = await GetLinearSpatialObjectRawAsync(op, sceneId, hierarchyId, backendObject, idProperty, objectId);
            if (current is null)
            {
                return _results.Failed($"删除{displayName}失败：未在当前层级列表中找到指定 ID。");
            }

            var objectName = ExtractString(current.Value, nameProperty) ?? ExtractNameFromContent(ExtractString(current.Value, "content")) ?? string.Empty;
            var path = $"/v1/{Uri.EscapeDataString(op)}/location/{Uri.EscapeDataString(sceneId)}/{backendObject}/delete";
            var body = new[] { objectId };

            if (IsPreview(mode))
            {
                return _results.Preview($"预览：将删除场景{displayName}。请确认名称后再执行删除。", new
                {
                    backend_api = $"POST /v1/{{operationalData}}/location/{{locationId}}/{backendObject}/delete",
                    operational_data = op,
                    scene_id = sceneId,
                    hierarchy_id = hierarchyId,
                    object_id = objectId,
                    object_name = objectName,
                    confirm_required = true,
                    confirm_name_required = objectName,
                    body
                });
            }

            if (!confirm)
            {
                return _results.Failed($"删除{displayName}属于高风险操作，请先确认目标后使用 mode=execute、confirm=true，并传入确认名称。");
            }

            if (string.IsNullOrWhiteSpace(objectName))
            {
                return _results.Failed($"删除{displayName}失败：无法读取当前名称，不能进行名称确认删除。");
            }

            if (!string.Equals(objectName, confirmName?.Trim(), StringComparison.Ordinal))
            {
                return _results.Failed($"删除{displayName}二次确认失败：确认名称必须和当前名称完全一致。当前名称：{objectName}");
            }

            var data = await _backendClient.PostAsync(path, body, CancellationToken.None);
            return _results.Success(
                $"已删除场景{displayName}。",
                new { operational_data = op, scene_id = sceneId, hierarchy_id = hierarchyId, object_id = objectId, object_name = objectName, delete_result = data },
                affectedObjects: new object[] { new { object_type = objectType, scene_id = sceneId, hierarchy_id = hierarchyId, object_id = objectId, object_name = objectName, action = "delete", data } });
        }
        catch (Exception ex)
        {
            return _results.Failed($"删除场景{displayName}失败。", new[] { ex.Message });
        }
    }

    /// <summary>
    /// 原始查询 POI 列表，供 list/get/update/delete 复用。
    /// </summary>
    private async Task<JsonElement> ListPoisRawAsync(string operationalData, string sceneId, string hierarchyId)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/location/{Uri.EscapeDataString(sceneId)}/level/{Uri.EscapeDataString(hierarchyId)}/position";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    /// <summary>
    /// 原始回读单个 POI。后端无单查接口，因此通过列表接口按 positionID 过滤。
    /// </summary>
    private async Task<JsonElement?> GetPoiRawAsync(string operationalData, string sceneId, string hierarchyId, string poiId)
    {
        var data = await ListPoisRawAsync(operationalData, sceneId, hierarchyId);
        return FindByProperty(data, "positionID", poiId);
    }

    /// <summary>
    /// 原始查询线/面对象列表。
    /// backendObject 取 path 或 region，用于拼接后端列表接口。
    /// </summary>
    private async Task<JsonElement> ListLinearSpatialObjectsRawAsync(string operationalData, string sceneId, string hierarchyId, string backendObject)
    {
        var path = $"/v1/{Uri.EscapeDataString(operationalData)}/location/{Uri.EscapeDataString(sceneId)}/level/{Uri.EscapeDataString(hierarchyId)}/{backendObject}";
        return await _backendClient.GetAsync(path, CancellationToken.None);
    }

    /// <summary>
    /// 原始回读单个线/面对象。后端无单查接口，因此通过列表接口按 pathID/regionID 过滤。
    /// </summary>
    private async Task<JsonElement?> GetLinearSpatialObjectRawAsync(
        string operationalData,
        string sceneId,
        string hierarchyId,
        string backendObject,
        string idProperty,
        string objectId)
    {
        var data = await ListLinearSpatialObjectsRawAsync(operationalData, sceneId, hierarchyId, backendObject);
        return FindByProperty(data, idProperty, objectId);
    }

    /// <summary>
    /// 解析线/面对象最终写入后端的 content。
    /// 优先使用完整 content_json；否则按 path/region 类型从 geometry_json 生成前端可显示的 content；未传几何时仅同步名称。
    /// </summary>
    private static string? ResolveLinearSpatialContent(
        string backendObject,
        string? currentContent,
        string? currentObjectId,
        string? contentJson,
        string? geometryJson,
        string? styleJson,
        string? propertiesJson,
        string name)
    {
        if (!string.IsNullOrWhiteSpace(contentJson))
        {
            using var content = JsonDocument.Parse(contentJson);
            return content.RootElement.GetRawText();
        }

        if (!string.IsNullOrWhiteSpace(geometryJson))
        {
            var fallbackId = ExtractIdFromContent(currentContent) ?? currentObjectId;
            return backendObject switch
            {
                "path" => BuildGuideRouteContentJson(name, geometryJson, styleJson, propertiesJson, fallbackId),
                "region" => BuildKeyAreaContentJson(name, geometryJson, styleJson, propertiesJson, fallbackId),
                _ => throw new InvalidOperationException($"暂不支持的空间对象类型：{backendObject}")
            };
        }

        return RenameFlatContent(currentContent, name);
    }

    /// <summary>
    /// 按孪易前端路线格式生成 path content。
    /// 参考手动创建路线的字段结构，默认生成 Arrow01/cableFlow.png/红色流线样式。
    /// </summary>
    private static string BuildGuideRouteContentJson(string name, string geometryJson, string? styleJson, string? propertiesJson, string? fallbackId)
    {
        using var geometry = JsonDocument.Parse(geometryJson);
        using var style = string.IsNullOrWhiteSpace(styleJson) ? null : JsonDocument.Parse(styleJson);
        using var properties = string.IsNullOrWhiteSpace(propertiesJson) ? null : JsonDocument.Parse(propertiesJson);

        var routeId = ExtractString(properties?.RootElement, "id") ?? fallbackId ?? CreateObjectId();
        var points = ExtractCoordinatePoints(geometry.RootElement)
            .Select(point => new
            {
                coord = new[] { point.Longitude, point.Latitude },
                coordZ = point.Altitude
            })
            .ToArray();

        if (points.Length < 2)
        {
            throw new InvalidOperationException("路线 geometry_json 至少需要 2 个坐标点。");
        }

        // 手动创建路线时，前端读取的是 path content 中的 points/style 字段；这里按前端格式生成默认可见路线。
        return JsonSerializer.Serialize(new
        {
            id = routeId,
            name,
            coordType = 0,
            coordTypeZ = 0,
            type = ExtractString(style?.RootElement, "type") ?? "Arrow01",
            texture = ExtractString(style?.RootElement, "texture") ?? "cableFlow.png",
            textureSpeed = ExtractDouble(style?.RootElement, "textureSpeed") ?? 1,
            color = ExtractString(style?.RootElement, "color") ?? "#ff0000",
            colorPass = ExtractString(style?.RootElement, "colorPass") ?? "#0000FF",
            width = ExtractDouble(style?.RootElement, "width") ?? 5,
            autoScale = ExtractBool(style?.RootElement, "autoScale") ?? true,
            visible = ExtractBool(properties?.RootElement, "visible") ?? true,
            lineDataId = ExtractString(properties?.RootElement, "lineDataId") ?? string.Empty,
            snapSurface = ExtractDouble(properties?.RootElement, "snapSurface") ?? 0,
            points,
            tag = ExtractString(properties?.RootElement, "tag") ?? "custominfo"
        }, ContentJsonOptions);
    }

    /// <summary>
    /// 按孪易前端区域格式生成 region content。
    /// 参考手动创建区域的字段结构，默认生成蓝色半透明底部填充区域。
    /// </summary>
    private static string BuildKeyAreaContentJson(string name, string geometryJson, string? styleJson, string? propertiesJson, string? fallbackId)
    {
        using var geometry = JsonDocument.Parse(geometryJson);
        using var style = string.IsNullOrWhiteSpace(styleJson) ? null : JsonDocument.Parse(styleJson);
        using var properties = string.IsNullOrWhiteSpace(propertiesJson) ? null : JsonDocument.Parse(propertiesJson);

        var areaId = ExtractString(properties?.RootElement, "id") ?? fallbackId ?? CreateObjectId();
        var points = ExtractCoordinatePoints(geometry.RootElement)
            .Select(point => new
            {
                coord = new[] { point.Longitude, point.Latitude }
            })
            .ToArray();

        if (points.Length < 3)
        {
            throw new InvalidOperationException("区域 geometry_json 至少需要 3 个坐标点。");
        }

        // 手动创建区域时，前端读取的是 region content 中的填充、颜色、高度和 points 字段。
        return JsonSerializer.Serialize(new
        {
            id = areaId,
            name,
            coordType = 0,
            coordTypeZ = 0,
            coordZ = ExtractDouble(properties?.RootElement, "coordZ") ?? 0,
            type = ExtractString(style?.RootElement, "type") ?? "Gradient03",
            color = ExtractString(style?.RootElement, "color") ?? "#0000FF",
            alpha = ExtractDouble(style?.RootElement, "alpha") ?? 0.6,
            areaHeight = ExtractDouble(style?.RootElement, "areaHeight") ?? 5,
            fillArea = ExtractString(style?.RootElement, "fillArea") ?? "Solid03",
            fillPosition = ExtractString(properties?.RootElement, "fillPosition") ?? ExtractString(style?.RootElement, "fillPosition") ?? "bottom",
            tag = ExtractString(properties?.RootElement, "tag") ?? "custominfo",
            visible = ExtractBool(properties?.RootElement, "visible") ?? true,
            points
        }, ContentJsonOptions);
    }

    /// <summary>
    /// 按孪易前端 POI 格式生成 position content。
    /// 用于把简单点坐标转换成前端能显示的兴趣点配置。
    /// </summary>
    private static string BuildContentJson(string name, string geometryJson, string? styleJson, string? propertiesJson, string? fallbackPoiId)
    {
        using var geometry = JsonDocument.Parse(geometryJson);
        using var style = string.IsNullOrWhiteSpace(styleJson) ? null : JsonDocument.Parse(styleJson);
        using var properties = string.IsNullOrWhiteSpace(propertiesJson) ? null : JsonDocument.Parse(propertiesJson);

        var (longitude, latitude, altitude) = ExtractPointCoordinate(geometry.RootElement);
        var poiId = ExtractString(properties?.RootElement, "id") ??
                    ExtractString(properties?.RootElement, "positionID") ??
                    fallbackPoiId ??
                    CreatePoiId();
        var iconName = ExtractString(style?.RootElement, "iconName") ??
                       ExtractString(properties?.RootElement, "iconName") ??
                       "/v1/communityAsset/SystemIcon000001/png/airport";
        var label = ExtractString(properties?.RootElement, "label") ?? name;
        var iconScale = ExtractString(style?.RootElement, "iconScale") ??
                        ExtractString(properties?.RootElement, "iconScale") ??
                        "0.7";
        var labelScale = ExtractString(style?.RootElement, "labelScale") ??
                         ExtractString(properties?.RootElement, "labelScale") ??
                         "0.7";

        // 孪易前端显示 POI 依赖这套扁平 content 字段；单纯 GeoJSON 包装会导致后端有数据但场景里不显示。
        return JsonSerializer.Serialize(new
        {
            id = poiId,
            coordType = 0,
            coordTypeZ = 0,
            iconName,
            autoScale = false,
            label,
            iconScale,
            labelScale,
            coord = new[] { longitude, latitude },
            coordZ = altitude,
            visible = true,
            labelOffset = new[] { 0.0, -0.6, 0.0 },
            tag = ExtractString(properties?.RootElement, "tag") ?? "custominfo",
            isShowLabel = true,
            positionName = name,
            positionID = poiId,
            jumpSettings = ExtractString(properties?.RootElement, "jumpSettings") ?? "无",
            sceneCategory = ExtractString(properties?.RootElement, "sceneCategory") ?? "无",
            name
        }, ContentJsonOptions);
    }

    /// <summary>
    /// 解析 POI 最终写入后端的 content。
    /// 优先使用完整 content_json；否则从点坐标生成 content；未传几何时仅同步名称字段。
    /// </summary>
    private static string? ResolvePoiContent(
        string? currentContent,
        string? currentPoiId,
        string? contentJson,
        string? geometryJson,
        string? styleJson,
        string? propertiesJson,
        string name)
    {
        if (!string.IsNullOrWhiteSpace(contentJson))
        {
            using var content = JsonDocument.Parse(contentJson);
            return content.RootElement.GetRawText();
        }

        if (!string.IsNullOrWhiteSpace(geometryJson))
        {
            var fallbackPoiId = ExtractPoiIdFromContent(currentContent) ?? currentPoiId;
            return BuildContentJson(name, geometryJson, styleJson, propertiesJson, fallbackPoiId);
        }

        return RenamePoiContent(currentContent, name);
    }

    /// <summary>
    /// 同步 POI content 内部的多个名称字段。
    /// POI 前端显示可能读取 name、positionName 或 label，所以重命名时需要一起更新。
    /// </summary>
    private static string? RenamePoiContent(string? currentContent, string name)
    {
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            return currentContent;
        }

        try
        {
            var node = JsonNode.Parse(currentContent) as JsonObject;
            if (node is null)
            {
                return currentContent;
            }

            // POI 名称在孪易 content 中有多处冗余字段，编辑/重命名时必须一起维护，否则列表名称和场景标签会不一致。
            node["name"] = name;
            node["positionName"] = name;
            node["label"] = name;
            return node.ToJsonString(ContentJsonOptions);
        }
        catch (JsonException)
        {
            return currentContent;
        }
    }

    /// <summary>
    /// 从 POI content 中提取现有 ID。
    /// 编辑坐标时保留原 ID，避免前端把同一个 POI 当成新对象。
    /// </summary>
    private static string? ExtractPoiIdFromContent(string? currentContent)
    {
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            return null;
        }

        try
        {
            using var content = JsonDocument.Parse(currentContent);
            return ExtractString(content.RootElement, "positionID") ?? ExtractString(content.RootElement, "id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 从 POI geometry_json 中解析单点坐标。
    /// 支持 GeoJSON Point、孪易 coord 字段，或直接传 [longitude, latitude, altitude?]。
    /// </summary>
    private static (double Longitude, double Latitude, double Altitude) ExtractPointCoordinate(JsonElement geometry)
    {
        JsonElement coordinates;
        if (geometry.ValueKind == JsonValueKind.Array)
        {
            coordinates = geometry;
        }
        else if (geometry.ValueKind == JsonValueKind.Object &&
                 geometry.TryGetProperty("coordinates", out var geoJsonCoordinates))
        {
            coordinates = geoJsonCoordinates;
        }
        else if (geometry.ValueKind == JsonValueKind.Object &&
                 geometry.TryGetProperty("coord", out var coord))
        {
            coordinates = coord;
        }
        else
        {
            throw new InvalidOperationException("geometry_json 必须是 GeoJSON Point，或直接传坐标数组 [longitude, latitude, altitude?]。");
        }

        if (coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() < 2)
        {
            throw new InvalidOperationException("geometry_json 坐标必须至少包含 longitude 和 latitude。");
        }

        var longitude = coordinates[0].GetDouble();
        var latitude = coordinates[1].GetDouble();
        var altitude = coordinates.GetArrayLength() > 2 ? coordinates[2].GetDouble() : 0.0;
        return (longitude, latitude, altitude);
    }

    /// <summary>
    /// 生成 POI 默认 ID。
    /// POI 当前沿用早期实现的 16 位 Guid 片段，保证同次创建唯一。
    /// </summary>
    private static string CreatePoiId()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>
    /// 生成路线/区域默认 ID。
    /// 手动创建示例是 16 位大小写字母数字混合 ID，这里生成同长度的可读随机 ID。
    /// </summary>
    private static string CreateObjectId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return string.Create(16, Guid.NewGuid(), (buffer, seed) =>
        {
            var bytes = seed.ToByteArray();
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = chars[bytes[i % bytes.Length] % chars.Length];
            }
        });
    }

    /// <summary>
    /// 同步路线/区域 content 内部的 name 字段。
    /// 路线和区域的显示名称主要保存在 content.name。
    /// </summary>
    private static string? RenameFlatContent(string? currentContent, string name)
    {
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            return currentContent;
        }

        try
        {
            var node = JsonNode.Parse(currentContent) as JsonObject;
            if (node is null)
            {
                return currentContent;
            }

            // 路线和区域 content 的显示名主要是 name；重命名时保持外层 name 与 content.name 一致。
            node["name"] = name;
            return node.ToJsonString(ContentJsonOptions);
        }
        catch (JsonException)
        {
            return currentContent;
        }
    }

    /// <summary>
    /// 从 content JSON 中提取 name。
    /// 当后端列表对象没有返回 pathName/regionName 时，用它作为名称兜底。
    /// </summary>
    private static string? ExtractNameFromContent(string? currentContent)
    {
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            return null;
        }

        try
        {
            using var content = JsonDocument.Parse(currentContent);
            return ExtractString(content.RootElement, "name");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 从 content JSON 中提取 id。
    /// 编辑路线/区域坐标时保留原 id，避免前端对象引用变化。
    /// </summary>
    private static string? ExtractIdFromContent(string? currentContent)
    {
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            return null;
        }

        try
        {
            using var content = JsonDocument.Parse(currentContent);
            return ExtractString(content.RootElement, "id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 从 JSON 对象中读取数字字段。
    /// 兼容数字和可解析的字符串，便于 style_json/properties_json 传参。
    /// </summary>
    private static double? ExtractDouble(JsonElement? element, string propertyName)
    {
        if (element is null ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    /// <summary>
    /// 从 JSON 对象中读取布尔字段。
    /// 兼容布尔值和可解析的字符串。
    /// </summary>
    private static bool? ExtractBool(JsonElement? element, string propertyName)
    {
        if (element is null ||
            element.Value.ValueKind != JsonValueKind.Object ||
            !element.Value.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
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

    /// <summary>
    /// 从路线/区域 geometry_json 中解析点序列。
    /// 支持 GeoJSON LineString/Polygon、孪易 points 字段，或直接传坐标数组。
    /// </summary>
    private static IReadOnlyList<(double Longitude, double Latitude, double Altitude)> ExtractCoordinatePoints(JsonElement geometry)
    {
        var coordinates = ResolveCoordinateArray(geometry);
        if (coordinates.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("geometry_json 坐标必须是数组。");
        }

        var points = new List<(double Longitude, double Latitude, double Altitude)>();
        foreach (var item in coordinates.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Array)
            {
                points.Add(ReadCoordinateTuple(item));
            }
            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("coord", out var coord))
            {
                var (longitude, latitude, altitude) = ReadCoordinateTuple(coord);
                var coordZ = item.TryGetProperty("coordZ", out var coordZElement) && coordZElement.ValueKind == JsonValueKind.Number
                    ? coordZElement.GetDouble()
                    : altitude;
                points.Add((longitude, latitude, coordZ));
            }
        }

        return points;
    }

    /// <summary>
    /// 归一化几何坐标数组。
    /// Polygon 的三层 coordinates 会自动取第一个外环，单点坐标会包成一组点数组。
    /// </summary>
    private static JsonElement ResolveCoordinateArray(JsonElement geometry)
    {
        if (geometry.ValueKind == JsonValueKind.Array)
        {
            return LooksLikeSingleCoordinate(geometry) ? WrapSingleCoordinate(geometry) : geometry;
        }

        if (geometry.ValueKind == JsonValueKind.Object && geometry.TryGetProperty("points", out var points))
        {
            return points;
        }

        if (geometry.ValueKind != JsonValueKind.Object || !geometry.TryGetProperty("coordinates", out var coordinates))
        {
            throw new InvalidOperationException("geometry_json 必须包含 coordinates/points，或直接传坐标数组。");
        }

        // Polygon 的 GeoJSON coordinates 是三层数组，这里默认取第一个外环。
        if (coordinates.ValueKind == JsonValueKind.Array &&
            coordinates.GetArrayLength() > 0 &&
            coordinates[0].ValueKind == JsonValueKind.Array &&
            coordinates[0].GetArrayLength() > 0 &&
            coordinates[0][0].ValueKind == JsonValueKind.Array)
        {
            return coordinates[0];
        }

        return coordinates;
    }

    /// <summary>
    /// 判断数组是否是单个坐标点，而不是坐标点集合。
    /// </summary>
    private static bool LooksLikeSingleCoordinate(JsonElement coordinates)
    {
        return coordinates.GetArrayLength() >= 2 &&
               coordinates[0].ValueKind == JsonValueKind.Number &&
               coordinates[1].ValueKind == JsonValueKind.Number;
    }

    /// <summary>
    /// 把单个坐标点包装成坐标点集合。
    /// 主要用于兼容用户直接传 [lng, lat, z?] 的情况。
    /// </summary>
    private static JsonElement WrapSingleCoordinate(JsonElement coordinate)
    {
        var json = $"[{coordinate.GetRawText()}]";
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 读取一个 [longitude, latitude, altitude?] 坐标点。
    /// altitude 可选，未传时默认 0。
    /// </summary>
    private static (double Longitude, double Latitude, double Altitude) ReadCoordinateTuple(JsonElement coordinate)
    {
        if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
        {
            throw new InvalidOperationException("坐标点必须是 [longitude, latitude, altitude?]。");
        }

        var longitude = coordinate[0].GetDouble();
        var latitude = coordinate[1].GetDouble();
        var altitude = coordinate.GetArrayLength() > 2 && coordinate[2].ValueKind == JsonValueKind.Number
            ? coordinate[2].GetDouble()
            : 0.0;
        return (longitude, latitude, altitude);
    }

    /// <summary>
    /// 从 JSON 对象中读取字符串字段。
    /// 非对象、字段不存在或字段为 null 时返回 null。
    /// </summary>
    private static string? ExtractString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    /// <summary>
    /// 从可空 JSON 对象中读取字符串字段。
    /// 用于简化 style/properties 这类可选 JSON 的字段读取。
    /// </summary>
    private static string? ExtractString(JsonElement? element, string propertyName)
    {
        return element is null ? null : ExtractString(element.Value, propertyName);
    }

    /// <summary>
    /// 对后端返回的 JSON 数组做本地关键字过滤。
    /// 后端部分列表接口没有 keyword 参数，所以 MCP Server 统一做 contains 过滤。
    /// </summary>
    private static IReadOnlyList<JsonElement> FilterJsonArray(JsonElement element, string keyword)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<JsonElement>();
        }

        return element.EnumerateArray()
            .Where(item => item.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Clone())
            .ToArray();
    }

    /// <summary>
    /// 在后端返回的 JSON 数组中按指定字段查找单个对象。
    /// 用于弥补 POI、路线、区域没有单对象详情接口的问题。
    /// </summary>
    private static JsonElement? FindByProperty(JsonElement element, string propertyName, string expectedValue)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty(propertyName, out var property) &&
                string.Equals(property.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                return item.Clone();
            }
        }

        return null;
    }

    /// <summary>
    /// 判断当前 Tool 是否只做预览。
    /// 写操作默认 preview 时不调用后端变更接口。
    /// </summary>
    private static bool IsPreview(string? mode)
    {
        return string.Equals(mode, "preview", StringComparison.OrdinalIgnoreCase);
    }
}
