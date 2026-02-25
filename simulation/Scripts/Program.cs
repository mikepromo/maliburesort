string baseAddress = "http://localhost:5078";
int botCount = 100;

Console.WriteLine($"Spawning {botCount} bots...");

List<Bot> bots = new();
List<Task> bootTasks = new();

Random _rng = new();

for (int i = 0; i < botCount; i++)
{
	Bot bot = new(baseAddress);
	bots.Add(bot);

	await Task.Delay(_rng.Next(100, 500));
	bootTasks.Add(bot.Boot($"Bot{i}", "password123"));
}

await Task.WhenAll(bootTasks);

Console.WriteLine("Swarm active.");
Console.ReadLine();