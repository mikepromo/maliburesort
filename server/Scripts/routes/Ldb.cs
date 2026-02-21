using Microsoft.EntityFrameworkCore;

public static class Ldb
{
	public static async Task<IResult> GetLeaderboard(string id, MainDbContext db)
	{
		DateTime oneHourAgo = DateTime.UtcNow.AddHours(-1);

		var leaderboard = await db.Bets
			.Include(b => b.Player)
			.Where(b => b.TableId == id && b.IsResolved && b.ResolvedAt >= oneHourAgo)
			.GroupBy(b => new { b.PlayerId, b.Player.Name })
			.Select(g => new
			{
				PlayerId = g.Key.PlayerId,
				PlayerName = g.Key.Name,
				NetProfit = g.Sum(b => b.Payout - b.Amount)
			})
			.OrderByDescending(x => x.NetProfit)
			.ToListAsync();

		return Results.Ok(leaderboard);
	}
}