namespace CosmosDB;

using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

class Program
{
    static async Task Main()
    {
        CosmosContext context = new CosmosContext();
        await  context.Database.EnsureCreatedAsync();
        CosmosClient client = context.Database.GetCosmosClient();

        Container.ChangesHandler<Person> changeHandlerDelegate = async (changes, cancellationToken) => 
        {
            foreach (Person person in changes)
            {
                Console.WriteLine($"change processor is processing person with name : {person.Name}");
            }
        };
        
        ChangeFeedProcessor processor = context.GetChangeFeedProcessor<Person>(changeHandlerDelegate);
       
        await processor.StartAsync();
        
        Console.ReadLine();
        
        await processor.StopAsync();
    }
    
    static async Task Main2(string[] args)
    {
        CosmosContext context = new();
        
        List<string> greetings = await context.QueryUserDefinedFunction();

        foreach (string greeting in greetings)
        {
            Console.WriteLine(greeting);
        }

        // await context.AddPreTrigger();

        // await context.UsePreTrigger();
    }
}
