using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public class TxProcesser(
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

		DateTime cutoff = DateTime.UtcNow.AddSeconds(-1);
		List<PendingTx> pending = await db.PendingTxs
			.Where(pt => pt.Status == PendingTx.PENDING && pt.CreatedAt < cutoff)
			.ToListAsync(stoppingToken);

		foreach (PendingTx tx in pending)
		{
			try
			{
				TxProcRes res = await Pay.ProcessPendingTx(tx, db, httpClientFactory, hub);

				if (res.Error != null)
				{
					//; log
				}
			}
			catch (Exception)
			{
				//; log
			}
		}
	}
}