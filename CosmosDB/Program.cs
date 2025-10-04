namespace CosmosDB;

class Program
{
    static async Task Main(string[] args)
    {
        CosmosContext context = new();
        
        List<string> greetings = await context.QueryUserDefinedFunction();

        foreach (string greeting in greetings)
        {
            Console.WriteLine(greeting);
        }
    }
}
