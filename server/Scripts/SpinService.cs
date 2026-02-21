using Microsoft.EntityFrameworkCore;

public class TableManager(IServiceScopeFactory scopeFactory)
{
	public async Task DoSpinsAsync()
	{
		List<Table> tablesDue;
		using (IServiceScope scope = scopeFactory.CreateScope())
		{
			tablesDue = await scope.ServiceProvider.GetRequiredService<MainDbContext>()
				.Tables.Where(t => t.NextSpinTime <= DateTime.UtcNow).ToListAsync();
		}

		foreach (Table table in tablesDue)
		{
			using IServiceScope scope = scopeFactory.CreateScope();
			MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();

			int winningNumber = Random.Shared.Next(0, 37);
			await ProcessSpin(table.Id, db, winningNumber);
		}
	}

	public async Task ProcessSpin(string tableId, MainDbContext db, int winningNumber)
	{
		Table? table = await db.Tables.FindAsync(tableId);
		table!.NextSpinTime = DateTime.UtcNow.AddSeconds(table.SpinInterval_sec());

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

		if (await db.TrySaveAsync() is not DbSaveResult.Success)
			throw new Exception();
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
			}

			await Task.Delay(1000, stoppingToken);
		}
	}
}