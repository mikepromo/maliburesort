using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public static class Chat
{
	const int DEFAULT_PAGINATION = 50;

	public static async Task<IResult> GetChat(string tableId, int? count, MainDbContext db)
	{count ??= DEFAULT_PAGINATION;
		List<ChatMessageDto> messages = await db.ChatMessages
			.Where(cm => cm.TableId == tableId)
			.OrderByDescending(cm => cm.SentAt)
			.Take(count.Value)
			.Select(cm => cm.Wrap())
			.ToListAsync();
		return Results.Ok(messages);
	}

	public static async Task<IResult> SendInChat(string tableId, ClaimsPrincipal user, SendChatRequest request,
		MainDbContext db, IHubContext<GameHub> hub)
	{
		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == tableId);

		if (table == null)
			return Results.NotFound("Table not found".Err());

		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found".Err());

		Player? player = table.Players.FirstOrDefault(p => p.Id == playerId);
		if (player == null)
			return Results.BadRequest("You must join the table first".Err());

		ChatMessage cm = new()
		{
			Id = Guid.NewGuid().ToString(),
			TableId = tableId,
			PlayerId = player.Id,
			Message = request.Message,
			SentAt = DateTime.UtcNow
		};

		db.ChatMessages.Add(cm);

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(tableId).SendAsync(RPC.ReceiveChat, cm.Wrap());

		return Results.NoContent();
	}
}