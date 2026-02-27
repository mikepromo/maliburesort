using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

[Authorize]
public class GameHub : Hub<IGameClient>
{
	public async Task SubscribeToTable(string tableId)
	{
		await Groups.AddToGroupAsync(Context.ConnectionId, tableId);
	}

	public async Task UnsubscribeFromTable(string tableId)
	{
		await Groups.RemoveFromGroupAsync(Context.ConnectionId, tableId);
	}
}