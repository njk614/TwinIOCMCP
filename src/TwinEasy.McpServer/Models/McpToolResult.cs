using System.Text.Json.Serialization;

namespace TwinEasy.McpServer.Models;

/// <summary>
/// 所有 MCP Tool 的统一返回结构，方便 Agent 稳定判断执行状态和下一步动作。
/// </summary>
public sealed record McpToolResult(
    // success / failed；后续可扩展 partial_success。
    string Status,
    // 给人看的简短结果说明。
    string Summary,
    // 工具的主要业务数据，通常包含后端原始 data 和关键上下文。
    object? Data = null,
    // 非阻断风险或提示，例如“后端无单查接口，已用列表过滤”。
    IReadOnlyList<string>? Warnings = null,
    // 批量操作失败明细；V1.0 暂时保留为空数组。
    IReadOnlyList<object>? FailedItems = null,
    // 被创建或修改的对象摘要。
    IReadOnlyList<object>? AffectedObjects = null,
    // 建议 Agent 或用户下一步可调用的 Tool。
    IReadOnlyList<string>? NextActions = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; } = Warnings;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object>? FailedItems { get; init; } = FailedItems;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<object>? AffectedObjects { get; init; } = AffectedObjects;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? NextActions { get; init; } = NextActions;
}
