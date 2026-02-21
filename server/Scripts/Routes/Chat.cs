using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class Chat
{
	const int DEFAULT_PAGINATION = 50;

	public static async Task<IResult> GetChat(string id, int? count, MainDbContext db)
	{
		count ??= DEFAULT_PAGINATION;
		var messages = await db.ChatMessages
			.Where(cm => cm.TableId == id)
			.OrderByDescending(cm => cm.SentAt)
			.Take(count.Value)
			.Select(cm => new
			{
				cm.Id,
				cm.PlayerId,
				PlayerName = cm.Player.Name,
				cm.Message,
				cm.SentAt
			})
			.ToListAsync();

		return Results.Ok(messages);
	}

	public static async Task<IResult> SendInChat(string id, ClaimsPrincipal user, SendChatRequest request,
		MainDbContext db)
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

		ChatMessage msg = new()
		{
			Id = Guid.NewGuid().ToString(),
			TableId = id,
			PlayerId = player.Id,
			Message = request.Message,
			SentAt = DateTime.UtcNow
		};

		db.ChatMessages.Add(msg);
		
		IResult? error = await db.TrySave();
		if (error is not null) return error;

		return Results.Ok(new { msg.Id, msg.SentAt });
	}
}