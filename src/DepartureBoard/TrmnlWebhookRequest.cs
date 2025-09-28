using System.Text.Json.Serialization;

namespace DepartureBoard;

public class TrmnlWebhookRequest<T>
{
    [JsonPropertyName("merge_variables")]
    public T MergeVariables { get; set; }
}