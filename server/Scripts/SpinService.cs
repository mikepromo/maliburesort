using Microsoft.EntityFrameworkCore;

public class SpinService(IServiceScopeFactory scopeFactory) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			await DoSpinsAsync();
			await Task.Delay(1000, stoppingToken);
		}
	}

	async Task DoSpinsAsync()
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
		DateTime now = DateTime.UtcNow;

		List<Table> tables = await db.Tables
			.Where(t => t.NextSpinTime <= now)
			.ToListAsync();

		foreach (Table table in tables)
		{
			await ProcessTableSpin(table, db);
		}

		await db.SaveChangesAsync();
	}

	async Task ProcessTableSpin(Table table, MainDbContext db)
	{
		table.NextSpinTime = DateTime.UtcNow.AddSeconds(table.SpinInterval_sec());

		List<Bet> bets = await db.Bets
			.Include(b => b.Player)
			.Where(b => b.TableId == table.Id && !b.IsResolved)
			.ToListAsync();

		if (bets.Count == 0) return;

		int winningNumber = Random.Shared.Next(0, 37);

		foreach (Bet bet in bets)
		{
			bool win = bet.ChosenNumber == winningNumber;
			decimal payout = win ? bet.Amount + bet.Amount * 35 : 0;

			bet.WinningNumber = winningNumber;
			bet.Payout = payout;
			bet.IsResolved = true;
			bet.ResolvedAt = DateTime.UtcNow;

			bet.Player.Balance += payout;
		}
	}
}