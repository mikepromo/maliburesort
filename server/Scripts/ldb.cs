using Microsoft.EntityFrameworkCore;

public static class ldb
{
	public static async Task<IResult> GetLeaderboard(string id, MainDbContext db)
	{
		DateTime oneHourAgo = DateTime.UtcNow.AddHours(-1);

		List<Bet> bets = await db.Bets
			.Where(b => b.TableId == id && b.IsResolved && b.ResolvedAt >= oneHourAgo)
			.ToListAsync();

		var leaderboard = bets
			.GroupBy(b => b.PlayerId)
			.Select(g => new
			{
				PlayerId = g.Key,
				PlayerName = db.Players.Find(g.Key)?.Name,
				NetProfit = g.Sum(b => b.Payout - b.Amount)
			})
			.OrderByDescending(x => x.NetProfit)
			.ToList();

		return Results.Ok(leaderboard);
	}
}