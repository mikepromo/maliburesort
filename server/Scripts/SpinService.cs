using Microsoft.EntityFrameworkCore;

public class TableManager(IServiceScopeFactory scopeFactory)
{
	public async Task DoSpinsAsync()
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
		DateTime now = DateTime.UtcNow;
		List<Table> tables = await db.Tables.Where(t => t.NextSpinTime <= now).ToListAsync();

		foreach (Table table in tables)
		{
			int winningNumber = Random.Shared.Next(0, 37);
			await ProcessSpin(table, db, winningNumber);
			await db.SaveChangesAsync();
		}
	}

	public async Task ProcessSpin(Table table, MainDbContext db, int winningNumber)
	{
		table.NextSpinTime = DateTime.UtcNow.AddSeconds(table.SpinInterval_sec());

		List<Bet> bets = await db.Bets
			.Include(b => b.Player)
			.Where(b => b.TableId == table.Id && !b.IsResolved)
			.ToListAsync();

		if (bets.Count == 0) return;

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

public class SpinService(TableManager manager) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await manager.DoSpinsAsync();
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}

			await Task.Delay(1000, stoppingToken);
		}
	}
}