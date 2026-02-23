using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public static class Chat
{
	const int DEFAULT_PAGINATION = 50;

	public static async Task<IResult> GetChat(string id, int? count, MainDbContext db)
	{
		count ??= DEFAULT_PAGINATION;
		List<ChatMessageDTO> messages = await db.ChatMessages
			.Where(cm => cm.TableId == id)
			.OrderByDescending(cm => cm.SentAt)
			.Take(count.Value)
			.Select(cm => new ChatMessageDTO
			{
				Id = cm.Id,
				PlayerId = cm.Player.Id,
				PlayerName = cm.Player.Name,
				Message = cm.Message,
				SentAt = cm.SentAt
			})
			.ToListAsync();

		return Results.Ok(messages);
	}

	public static async Task<IResult> SendInChat(string id, ClaimsPrincipal user, SendChatRequest request,
		MainDbContext db, IHubContext<GameHub> hub)
	{
		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == id);

		if (table == null)
			return Results.NotFound("Table not found");

		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");

		Player? player = table.Players.FirstOrDefault(p => p.Id == playerId);
		if (player == null)
			return Results.BadRequest("You must join the table first");

		ChatMessage cm = new()
		{
			Id = Guid.NewGuid().ToString(),
			TableId = id,
			PlayerId = player.Id,
			Message = request.Message,
			SentAt = DateTime.UtcNow
		};

		db.ChatMessages.Add(cm);

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(id).SendAsync(RPC.ReceiveChat,
			new ChatMessageDTO
			{
				Id = cm.Id,
				PlayerId = player.Id,
				PlayerName = player.Name,
				Message = cm.Message,
				SentAt = cm.SentAt
			});

		return Results.Ok();
	}
}