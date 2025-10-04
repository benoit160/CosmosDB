namespace CosmosDB;

using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

class Program
{
    static async Task Main()
    {
        CosmosContext context = new CosmosContext();
        CosmosClient client = context.Database.GetCosmosClient();

        Container sourceContainer = client.GetContainer("Sandbox", "Container");
        Container leaseContainer = client.GetContainer("Sandbox", "ChangeFeed");

        Container.ChangesHandler<Person> changeHandlerDelegate = async (changes, _) => 
        {
            foreach(Person person in changes)
            {
                await ValueTask.CompletedTask;
                Console.WriteLine($"change processor is processing person with name : {person.Name}");
            }
        };

        ChangeFeedProcessorBuilder builder = sourceContainer.GetChangeFeedProcessorBuilder("Processor", changeHandlerDelegate);

        ChangeFeedProcessor processor = builder
            .WithInstanceName("desktopApplication")
            .WithLeaseContainer(leaseContainer)
            .Build();
        
        await processor.StartAsync();
        
        Person person = new Person
        {
            PartitionKey = nameof(Person),
            Name = "Bob" 
        };
        
        context.Persons.Add(person);
        await context.SaveChangesAsync();
        
        Console.ReadLine();
        
        await processor.StopAsync();
    }
}