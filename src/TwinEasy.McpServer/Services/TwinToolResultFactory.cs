using TwinEasy.McpServer.Models;

namespace TwinEasy.McpServer.Services;

/// <summary>
/// 集中生成 MCP Tool 返回值，避免每个 Tool 自己拼 status/summary/data。
/// </summary>
public sealed class TwinToolResultFactory
{
    /// <summary>生成成功结果。</summary>
    public McpToolResult Success(
        string summary,
        object? data = null,
        IReadOnlyList<object>? affectedObjects = null,
        IReadOnlyList<string>? nextActions = null,
        IReadOnlyList<string>? warnings = null)
    {
        return new McpToolResult(
            Status: "success",
            Summary: summary,
            Data: data,
            Warnings: warnings ?? Array.Empty<string>(),
            FailedItems: Array.Empty<object>(),
            AffectedObjects: affectedObjects ?? Array.Empty<object>(),
            NextActions: nextActions ?? Array.Empty<string>());
    }

    /// <summary>生成失败结果。异常消息放入 warnings，避免直接抛出破坏 MCP 调用。</summary>
    public McpToolResult Failed(string summary, IReadOnlyList<string>? warnings = null, object? data = null)
    {
        return new McpToolResult(
            Status: "failed",
            Summary: summary,
            Data: data,
            Warnings: warnings ?? Array.Empty<string>(),
            FailedItems: Array.Empty<object>(),
            AffectedObjects: Array.Empty<object>(),
            NextActions: Array.Empty<string>());
    }

    /// <summary>写操作预览结果；V1.0 统一要求 execute 时再确认。</summary>
    public McpToolResult Preview(string summary, object diff, IReadOnlyList<string>? warnings = null)
    {
        return Success(
            summary,
            new
            {
                mode = "preview",
                diff,
                risk_level = "low",
                requires_confirm = true
            },
            warnings: warnings);
    }
}
