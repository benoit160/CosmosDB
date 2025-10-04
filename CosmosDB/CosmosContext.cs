namespace CosmosDB;

using System.Reflection;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;

public class CosmosContext : DbContext
{
    public const string DatabaseName = "Sandbox";
    
    public DbSet<Person> Persons { get; set; }

    /// <summary>
    /// Returns the container containing the entity type.
    /// </summary>
    /// <typeparam name="T">The type of the entity stored in the container.</typeparam>
    /// <returns>The CosmosDB container.</returns>
    public Container GetContainer<T>()
        where T : BaseDocument
    {
        CosmosClient client = Database.GetCosmosClient();
        string property = GetDbSetPropertyName<T>();
        return client.GetContainer(DatabaseName, property);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Get all types inheriting from BaseDocument
        Type baseEntityType = typeof(BaseDocument);
        IEnumerable<Type> entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => baseEntityType.IsAssignableFrom(t.ClrType) && t.ClrType != baseEntityType)
            .Select(t => t.ClrType);

        // Apply configuration to each entity type
        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType)
                .Property(nameof(BaseDocument.Id))
                .HasValueGenerator<GuidValueGenerator>()
                .ToJsonProperty("id");

            modelBuilder.Entity(entityType)
                .HasPartitionKey(nameof(BaseDocument.PartitionKey));

            modelBuilder.Entity(entityType)
                .Property(nameof(BaseDocument.TimeStamp))
                .ToJsonProperty("_ts");
        }

        // Configure this entity to be in its own container, with no discriminator.
        modelBuilder.Entity<Person>()
            .HasNoDiscriminator()
            .HasQueryFilter(p => p.PartitionKey == nameof(Person))
            .ToContainer(nameof(Persons));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseCosmos(
            accountEndpoint: "https://localhost:8081",
            accountKey: "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            databaseName: DatabaseName);
        
        optionsBuilder.LogTo(Console.WriteLine);
    }

    /// <summary>
    /// Creates a User-Defined Function from a name and body.
    /// </summary>
    /// <param name="id">The identifier of the function, used to invoke it in queries.</param>
    /// <param name="body">The JavaScript implementation of the function.</param>
    /// <remarks>
    /// <para>User-defined functions (UDFs) are used to extend the Azure Cosmos DB for NoSQL’s query language grammar and implement custom business logic.</para>
    /// <para>UDFs can only be called from inside queries as they enhance and extend the SQL query language.</para>
    /// </remarks>
    /// <returns>The object containing the definition of the UDF, to be added to the scripts of a container.</returns>
    /// <example>
    /// The following example shows how to create a UDF that returns a greeting message.
    /// <code>
    /// function SayHi(name)
    /// {
    ///     return "Hi " + name;
    /// }
    /// </code>
    /// </example>
    public UserDefinedFunctionProperties CreateUserDefinedFunction(string id, string body)
    {
        return new UserDefinedFunctionProperties
        {
            Id = id,
            Body = body,
        };
    }

    public async Task AddUserDefinedFunction()
    {
        string udfBody = "function SayHi(name){return \"Hi \" + name;}";
        
        UserDefinedFunctionProperties udf = CreateUserDefinedFunction("SayHi", udfBody);
        
        Container container = GetContainer<Person>();
        
        await container.Scripts.CreateUserDefinedFunctionAsync(udf);
    }
    
    public async Task<List<string>> QueryUserDefinedFunction()
    {
        Container container = GetContainer<Person>();
        string query = $"SELECT VALUE udf.SayHi(p.Name) FROM Persons p";
        FeedIterator<string> iterator = container.GetItemQueryIterator<string>(query, requestOptions: new QueryRequestOptions
        {
            // Limit to 5 items for this example.
            MaxItemCount = 5,
        });

        List<string> results = [];
        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                results.Add(item);
            }
        }

        return results;
    }
    
    /// <summary>
    /// Uses reflection to get the name of the DbSet property where the entity is stored.
    /// </summary>
    /// <typeparam name="T">The entity stored in the DbSet</typeparam>
    /// <returns>The name of the property of this DbContext.</returns>
    /// <exception cref="ArgumentException">There is no DbSet containing this entity.</exception>
    private string GetDbSetPropertyName<T>()
        where T : BaseDocument
    {
        Type dbSetType = typeof(DbSet<T>);
        PropertyInfo? property = GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.PropertyType == dbSetType);

        if (property is null) throw new ArgumentException();

        return property.Name;
    }
}