using Microsoft.EntityFrameworkCore;
using shared;

public static class Ldb
{
	public static async Task<IResult> GetLeaderboard(string tableId, MainDbContext db)
	{
		const int DEFAULT_PAGINATION = 50;

		DateTime oneHourAgo = DateTime.UtcNow.AddHours(-1);

		List<LdbEntryDto> leaderboard = await db.Bets
			.Include(b => b.Player)
			.Where(b => b.TableId == tableId && b.IsResolved && b.ResolvedAt >= oneHourAgo)
			.GroupBy(b => new { b.PlayerId, b.Player.Name })
			.Select(g => new LdbEntryDto
			{
				PlayerId = g.Key.PlayerId,
				PlayerName = g.Key.Name,
				NetProfit = g.Sum(b => b.Payout - b.Amount)
			})
			.OrderByDescending(x => x.NetProfit)
			.Take(DEFAULT_PAGINATION)
			.ToListAsync();

		return Results.Ok(leaderboard);
	}
}