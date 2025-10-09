namespace CosmosDB;

using Microsoft.Azure.Cosmos;

using Newtonsoft.Json;

/// <summary>
/// The base properties necessary for a correct CosmosDB document.
/// </summary>
/// <remarks>The <see cref="JsonPropertyAttribute"/> are used to indicate to the Newtonsoft serializer the name of the properties when using the <see cref="Container"/> directly for queries.</remarks>
public abstract class BaseDocument
{
    [JsonProperty("id")]
    public Guid Id { get; init; }

    /// <summary>
    /// Logical partition key.
    /// </summary>
    public required string PartitionKey { get; init; }

    /// <summary>
    /// Timestamp of the last modification.
    /// </summary>
    /// <remarks>This property is managed by the database engine.</remarks>
    [JsonProperty("_ts")]
    public long TimeStamp { get; init; }
}