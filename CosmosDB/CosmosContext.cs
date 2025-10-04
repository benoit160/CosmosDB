namespace CosmosDB;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ValueGeneration;

public class CosmosContext : DbContext
{
    public DbSet<Person> Persons { get; set; }
    
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

        // Configure this entity to be in it's own contaner, with no discriminator.
        modelBuilder.Entity<Person>()
            .HasNoDiscriminator()
            .ToContainer(nameof(Persons));
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseCosmos(
            "https://localhost:8081",
            "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
            databaseName: "Sandbox");
}