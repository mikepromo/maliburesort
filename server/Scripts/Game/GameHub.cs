using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

[Authorize]
public class GameHub(IServiceScopeFactory scopeFactory) : Hub
{
	public async Task SubscribeToTable(string tableId)
	{
		await Groups.AddToGroupAsync(Context.ConnectionId, tableId);
	}

	public async Task UnsubscribeFromTable(string tableId)
	{
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, tableId);
	}

	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		string? playerId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

		if (playerId != null)
		{
			//; remove any tables this player is stuck-sitting at
			using IServiceScope scope = scopeFactory.CreateScope();
			MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();

			List<Table> tablesWithPlayer = await db.Tables
				.Include(t => t.Players)
				.Where(t => t.Players.Any(p => p.Id == playerId))
				.ToListAsync();

			foreach (Table table in tablesWithPlayer)
			{
				Player player = table.Players.First(p => p.Id == playerId);
				table.Players.Remove(player);

				await Clients.Group(table.Id).SendAsync(RPC.PlayerLeft, player.Name);
			}

			if (tablesWithPlayer.Any())
			{
				await db.SaveChangesAsync();
			}
		}

		await base.OnDisconnectedAsync(exception);
	}
}