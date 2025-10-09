namespace CosmosDB;

using System.Reflection;

using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;

public class CosmosContext : DbContext
{
    public const string DatabaseName = "Sandbox";
    public const string LeasesContainerName = "Leases";

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
    /// <returns>A <see cref="UserDefinedFunctionProperties"/> object containing the definition of the UDF, to be added to the scripts of a container.</returns>
    /// <remarks>
    /// <para>User-defined functions (UDFs) are used to extend the Azure Cosmos DB for NoSQL’s query language grammar and implement custom business logic.</para>
    /// <para>UDFs can only be called from inside queries as they enhance and extend the SQL query language.</para>
    /// </remarks>
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
        string udfBody = """
                         function SayHi(name) {
                             return "Hi " + name
                         }
                         """;

        UserDefinedFunctionProperties udf = CreateUserDefinedFunction("SayHi", udfBody);

        Container container = GetContainer<Person>();

        await container.Scripts.CreateUserDefinedFunctionAsync(udf);
    }

    public async Task AddPreTrigger()
    {
        string preTrigger = """
                            function addLabel() {
                                var context = getContext();
                                var request = context.getRequest();
                                
                                var pendingItem = request.getBody();

                                if (!('label' in pendingItem))
                                    pendingItem['label'] = 'new';

                                request.setBody(pendingItem);
                            }
                            """;

        TriggerProperties trigger = CreatePreTrigger("addLabel", preTrigger);
        Container container = GetContainer<Person>();

        await container.Scripts.CreateTriggerAsync(trigger);
    }

    /// <summary>
    /// Creates a Pre-Trigger from a name and body.
    /// </summary>
    /// <param name="id">The identifier of the trigger, used to invoke it in queries.</param>
    /// <param name="body">The JavaScript implementation of the trigger.</param>
    /// <returns>A <see cref="TriggerProperties"/> object containing the definition of the trigger, to be added to the scripts of a container.</returns>
    /// <remarks>
    /// <para>Triggers are the core way that Azure Cosmos DB for NoSQL can inject business logic both before and after operations.</para>
    /// <para>Triggers are defined as JavaScript functions. The function is then executed when the trigger is invoked.</para>
    /// </remarks>
    /// <example>
    /// The following example shows how to create a trigger that adds a property 'label' if it is not already present.
    /// <code>
    /// function addLabel() {
    ///     var context = getContext();
    ///     var request = context.getRequest();
    /// 
    ///     var pendingItem = request.getBody();
    /// 
    ///     if (!('label' in pendingItem))
    ///     pendingItem['label'] = 'new';
    /// 
    ///     request.setBody(pendingItem);
    /// </code>
    /// </example>
    public TriggerProperties CreatePreTrigger(string id, string body)
    {
        return new TriggerProperties
        {
            Id = id,
            Body = body,
            TriggerOperation = TriggerOperation.Create,
            TriggerType = TriggerType.Pre
        };
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

    public async Task UsePreTrigger()
    {
        Person bob = new Person
        {
            Id = Guid.NewGuid(),
            Name = "Bob",
            PartitionKey = nameof(Person),
        };

        Container container = GetContainer<Person>();

        ItemRequestOptions options = new()
        {
            PreTriggers = new List<string> { "addLabel" },
        };

        await container.CreateItemAsync(bob, requestOptions: options);
    }

    /// <summary>
    /// Create a change feed processor for this entity.
    /// </summary>
    /// <param name="changeHandlerDelegate">The delegate that will process the change feed.</param>
    /// <typeparam name="T">The entity, inheriting from <see cref="BaseDocument"/></typeparam>
    /// <returns>A <see cref="ChangeFeedProcessor"/>.</returns>
    /// <remarks>Once created, the processor must be started with <see cref="ChangeFeedProcessor.StartAsync()"/>.</remarks>
    /// <example>
    /// The following example shows how to create the ChangesEstimationHandler.
    /// <code>
    /// Container.ChangesHandler&lt;MyEntity&gt; changeHandlerDelegate = async (changes, cancellationToken) => 
    /// {
    ///     foreach (MyEntity entity in changes)
    ///     {
    ///         Console.WriteLine($"processing entity with id : {entity.Id}");
    ///     }
    /// };
    /// </code>
    /// </example>
    public ChangeFeedProcessor GetChangeFeedProcessor<T>(Container.ChangesHandler<T> changeHandlerDelegate)
        where T : BaseDocument
    {
        Container sourceContainer = GetContainer<T>();
        Container leaseContainer = GetLeaseContainer();

        string processorName = $"{typeof(T).Name}-Processor";

        ChangeFeedProcessorBuilder builder = sourceContainer.GetChangeFeedProcessorBuilder(processorName, changeHandlerDelegate);

        ChangeFeedProcessor processor = builder
            .WithInstanceName("desktopApplication")
            .WithLeaseContainer(leaseContainer)
            .Build();

        return processor;
    }

    /// <summary>
    /// Create a change feed estimator for this entity.
    /// </summary>
    /// <param name="changeHandlerDelegate">The delegate that will process the change estimation.</param>
    /// <typeparam name="T">The entity, inheriting from <see cref="BaseDocument"/></typeparam>
    /// <returns>A <see cref="ChangeFeedProcessor"/>.</returns>
    /// <remarks>Once created, the processor must be started with <see cref="ChangeFeedProcessor.StartAsync()"/>.</remarks>
    /// <example>
    /// The following example shows how to create the ChangesEstimationHandler.
    /// <code>
    /// ChangesEstimationHandler changeEstimationDelegate = async (
    ///     long estimation, 
    ///     CancellationToken cancellationToken
    /// ) => {
    ///     // Do something with the estimation
    /// };
    /// </code>
    /// </example>
    public ChangeFeedProcessor GetChangeFeedEstimator<T>(Container.ChangesEstimationHandler changeHandlerDelegate)
        where T : BaseDocument
    {
        Container sourceContainer = GetContainer<T>();
        Container leaseContainer = GetLeaseContainer();

        string processorName = $"{typeof(T).Name}-Estimator";

        ChangeFeedProcessorBuilder builder = sourceContainer.GetChangeFeedEstimatorBuilder(processorName, changeHandlerDelegate);

        ChangeFeedProcessor processor = builder
            .WithInstanceName("desktopApplication")
            .WithLeaseContainer(leaseContainer)
            .Build();

        return processor;
    }

    /// <summary>
    /// Checks if the container exists.
    /// </summary>
    /// <param name="containerId">The name of the container.</param>
    /// <returns>True if the container exists, otherwise false.</returns>
    public async Task<bool> ContainerExistsAsync(string containerId)
    {
        CosmosClient client = Database.GetCosmosClient();
        Container container = client.GetContainer(DatabaseName, containerId);

        try
        {
            // Triggers a read to verify existence
            await container.ReadContainerAsync();
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
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

    private Container GetLeaseContainer()
    {
        CosmosClient client = Database.GetCosmosClient();

        Database database = client.GetDatabase(DatabaseName);

        Container container = database.GetContainer(id: LeasesContainerName);

        return container;
    }

    private async Task<Container> CreateLeaseContainer()
    {
        CosmosClient client = Database.GetCosmosClient();

        Database database = client.GetDatabase(DatabaseName);

        Container container = await database.CreateContainerIfNotExistsAsync(id: LeasesContainerName, partitionKeyPath: "/id");

        return container;
    }
}