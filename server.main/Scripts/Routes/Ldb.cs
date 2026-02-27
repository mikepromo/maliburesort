using Microsoft.EntityFrameworkCore;
using shared;

public static class Ldb
{
	const int DEFAULT_PAGINATION = 50;

	public static async Task<IResult> GetLeaderboard(string tableId, MainDbContext db)
	{
		DateTime oneDayAgo = DateTime.UtcNow.AddHours(-24);

		List<LdbEntryDto> leaderboard = await db.Bets
			.Include(b => b.Player)
			.Where(b => b.TableId == tableId && b.IsResolved && b.ResolvedAt >= oneDayAgo)
			.GroupBy(b => new { b.PlayerId, b.Player.Name })
			.Select(g => new LdbEntryDto
			{
				PlayerId = g.Key.PlayerId,
				PlayerName = g.Key.Name,
				NetProfit = g.Sum(b => b.Payout - b.Amount)
			})
			.OrderByDescending(x => x.NetProfit)
			.Where(x=>x.NetProfit>0)
			.Take(DEFAULT_PAGINATION)
			.ToListAsync();

		return Results.Ok(leaderboard);
	}
}