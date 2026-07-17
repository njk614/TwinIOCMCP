using System.Text.Json;
using System.Text.Json.Serialization;

namespace TwinEasy.McpServer.Models;

/// <summary>
/// 孪易后端的标准响应包：{ code, msg, data }。
/// </summary>
public sealed class TwinBackendEnvelope
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("msg")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public JsonElement Data { get; set; }

    /// <summary>后端约定 code=10000 表示成功。</summary>
    public bool Success => Code == 10000;
}
