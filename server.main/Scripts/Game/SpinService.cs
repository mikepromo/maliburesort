using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public class TableManager(
	IServiceScopeFactory scopeFactory,
	IHubContext<GameHub, IGameClient> hub)
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
			await ProcessSpin(table.Id, db);
		}
	}

	public async Task ProcessSpin(string tableId, MainDbContext db)
	{
		int winningNumber = Random.Shared.Next(0, 37);
		string spinId = Guid.NewGuid().ToString();

		Table? table = await db.Tables.FindAsync(tableId);
		table!.NextSpinTime = DateTime.UtcNow.AddSeconds(table.Tier.SpinInterval_sec());
		table.LastWinningNumber = winningNumber;

		List<Bet> bets = await db.Bets
			.Include(b => b.Player)
			.Where(b => b.TableId == table.Id && !b.IsResolved)
			.ToListAsync();

		foreach (Bet bet in bets)
		{
			bool win = bet.ChosenNumber == winningNumber;
			decimal payout = win ? bet.Amount + bet.Amount * 36 : 0;

			bet.WinningNumber = winningNumber;
			bet.Payout = payout;
			bet.IsResolved = true;
			bet.ResolvedAt = DateTime.UtcNow;

			if (payout > 0)
			{
				PendingTx pending = new()
				{
					Id = $"{spinId}_{bet.Id}",
					Type = PendingTx.PAYOUT,
					Status = PendingTx.PENDING,
					PlayerId = bet.PlayerId,
					Amount = payout,
					CreatedAt = DateTime.UtcNow
				};
				db.PendingTxs.Add(pending);
			}
		}

		if (await db.TrySaveAsync() is not DbSaveResult.Success)
		{
			//; all pending payouts, bets resolution markers, and the table spin time itself - reverted
			//; we rely on natural retry to attempt complete again
			return;
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

		if (await db.TrySaveAsync() is DbSaveResult.Success)
		{
			foreach (Player player in idles)
			{
				await hub.Clients.Group(tableId).PlayerLeft(player.Wrap());
			}
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
			}

			await Task.Delay(1000, stoppingToken);
		}
	}
}