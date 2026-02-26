using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public class TableManager(IServiceScopeFactory scopeFactory, IHubContext<GameHub, IGameClient> hub)
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

			await RemoveIdlePlayers(table.Id, db);

			int winningNumber = Random.Shared.Next(0, 37);
			await ProcessSpin(table.Id, db, 1);
		}
	}

	public async Task ProcessSpin(string tableId, MainDbContext db, int winningNumber)
	{
		Table? table = await db.Tables.FindAsync(tableId);
		table!.NextSpinTime = DateTime.UtcNow.AddSeconds(table.Tier.SpinInterval_sec());
		table!.LastWinningNumber = winningNumber;

		List<Bet> bets = await db.Bets
			.Include(b => b.Player)
			.Where(b => b.TableId == table.Id && !b.IsResolved)
			.ToListAsync();

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

		foreach (Bet bet in bets)
		{
			if (bet.Payout > 0)
			{
				await hub.Clients.User(bet.PlayerId).BalanceUpdate(bet.Player.Balance);
			}
		}

		SpinResultDto spinResult = new()
		{
			NextSpinTime = table.NextSpinTime,
			WinningNumber = winningNumber
		};

		await hub.Clients.Group(tableId).ReceiveSpin(spinResult);
	}

	public async Task RemoveIdlePlayers(string tableId, MainDbContext db)
	{
		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == tableId);

		IEnumerable<Player> idles = table!.Players.Where(p => p.LastActiveAt < DateTime.UtcNow.AddMinutes(-30))
			.ToList();

		foreach (Player player in idles)
		{
			table.Players.Remove(player);
			player.CurrentTableId = null;
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