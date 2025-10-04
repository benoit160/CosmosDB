namespace CosmosDB;

public abstract class BaseDocument
{
    public Guid Id { get; init; }
    
    /// <summary>
    /// Logical partition key.
    /// </summary>
    public required string PartitionKey { get; init; }

    /// <summary>
    /// Timestamp of the last modification.
    /// </summary>
    /// <remarks>This property is managed by the Database engine.</remarks>
    public long TimeStamp { get; init; }
}