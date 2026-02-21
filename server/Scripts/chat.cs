using Microsoft.EntityFrameworkCore;

public static class chat
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

	public static async Task<IResult> SendInChat(string id, SendChatRequest request, MainDbContext db)
	{
		Table? table = await db.Tables.FindAsync(id);
		
		if (table == null)
			return Results.NotFound("Table not found");

		Player? player = table.Players.FirstOrDefault(p => p.Id == request.PlayerId);
		if (player == null)
			return Results.BadRequest("You must join the table first");

		ChatMessage chat = new()
		{
			TableId = id,
			PlayerId = player.Id,
			Message = request.Message,
			SentAt = DateTime.UtcNow
		};

		db.ChatMessages.Add(chat);
		await db.SaveChangesAsync();

		return Results.Ok(new { chat.Id, chat.SentAt });
	}
}