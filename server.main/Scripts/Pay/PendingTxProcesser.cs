using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

public class PendingTxProcesser(
	IServiceScopeFactory scopeFactory,
	IHttpClientFactory httpClientFactory,
	IHubContext<GameHub, IGameClient> hub)
	: BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await TryCompletePendingTx(stoppingToken);
			}
			catch (Exception ex)
			{
				//; log
			}

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
		}
	}

	async Task TryCompletePendingTx(CancellationToken stoppingToken)
	{
		using IServiceScope scope = scopeFactory.CreateScope();
		MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();

		DateTime cutoff = DateTime.UtcNow.AddSeconds(-1); //; does it give any safety buffer?
		List<PendingTx> stuck = await db.PendingTxs
			.Where(pt => pt.Status == PendingTx.PENDING && pt.CreatedAt < cutoff)
			.ToListAsync(stoppingToken);

		foreach (PendingTx tx in stuck)
		{
			try
			{
				TxProcRes res = await Pay.ProcessPendingTx(tx, db, httpClientFactory, hub);

				if (res.Error != null)
				{
					//; log
				}
			}
			catch (Exception ex)
			{
				//; log
			}
		}
	}
}