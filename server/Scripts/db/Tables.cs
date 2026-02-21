using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static partial class Tables
{
	public static async Task<IResult> ListTables(MainDbContext db)
	{
		var tables = (await db.Tables.Include(t => t.Players).ToListAsync())
			.Select(t => new
			{
				t.Id,
				t.Name,
				MinBet = t.MinBet(),
				MaxBet = t.MaxBet(),
				MaxSeats = t.MaxSeats(),
				PlayerCount = t.Players.Count,
				t.NextSpinTime
			}).ToList();

		return Results.Ok(tables);
	}

	public static async Task<IResult> JoinTable(string id, ClaimsPrincipal user , MainDbContext db)
	{
		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == id);

		if (table == null)
			return Results.NotFound("Table not found");

		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");
		
		Player? player = await db.Players.FindAsync(playerId);
		if (player == null)
			return Results.NotFound("Player not found");

		if (table.Players.Any(p => p.Id == playerId))
			return Results.Conflict("Player already at this table");

		if (table.Players.Count >= table.MaxSeats())
			return Results.BadRequest("Table is full");

		table.Players.Add(player);
		await db.SaveChangesAsync();

		return Results.Ok(new { Message = "Joined table", TableId = id, PlayerId = player.Id });
	}

	public static async Task<IResult> LeaveTable(string id, ClaimsPrincipal user, MainDbContext db)
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
			return Results.NotFound("Player not at this table");

		table.Players.Remove(player);
		await db.SaveChangesAsync();

		return Results.Ok(new { Message = "Left table", TableId = id, PlayerId = player.Id });
	}

	public static async Task<IResult> PlaceBet(string id, ClaimsPrincipal user, PlaceBetRequest request, MainDbContext db)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");
		
		var player = await user.GetPlayerSecure(db); // Secure DB verification
		if (player == null) return Results.Unauthorized();
		
		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == id);

		if (table == null)
			return Results.NotFound("Table not found");

		if (!table.Players.Any(p=> p.Id == playerId))
			return Results.BadRequest("You must join the table first");

		if (request.Amount < table.MinBet() || request.Amount > table.MaxBet())
			return Results.BadRequest($"Bet must be between {table.MinBet()} and {table.MaxBet()}");

		if (request.ChosenNumber < 0 || request.ChosenNumber > 36)
			return Results.BadRequest("Number must be between 0 and 36");

		if (request.Amount <= 0)
			return Results.BadRequest("Bet amount must be positive");

		if (player.Balance < request.Amount)
			return Results.BadRequest("Insufficient funds");

		double timeUntilSpin = (table.NextSpinTime - DateTime.UtcNow).TotalSeconds;
		if (timeUntilSpin < 2)
			return Results.BadRequest("Too close to next spin, wait a moment");

		player.Balance -= request.Amount;

		Bet bet = new()
		{
			Id = Guid.NewGuid().ToString(),
			PlayerId = player.Id,
			TableId = id,
			ChosenNumber = request.ChosenNumber,
			Amount = request.Amount,
			PlacedAt = DateTime.UtcNow,
			IsResolved = false
		};

		db.Bets.Add(bet);
		await db.SaveChangesAsync();

		return Results.Ok(new
		{
			Message = "Bet placed",
			BetId = bet.Id,
			RemainingBalance = player.Balance
		});
	}

	public static decimal MinBet(this Table table)
	{
		return table.Tier switch
		{
			TableTier.Tier1 => 10,
			TableTier.Tier2 => 20,
			TableTier.Tier3 => 30,
			TableTier.Tier4 => 50,
			_               => throw new ArgumentOutOfRangeException()
		};
	}

	public static decimal MaxBet(this Table table)
	{
		return table.Tier switch
		{
			TableTier.Tier1 => 100,
			TableTier.Tier2 => 200,
			TableTier.Tier3 => 300,
			TableTier.Tier4 => 500,
			_               => throw new ArgumentOutOfRangeException()
		};
	}

	public static int MaxSeats(this Table table)
	{
		return table.Tier switch
		{
			TableTier.Tier1 => 40,
			TableTier.Tier2 => 20,
			TableTier.Tier3 => 10,
			TableTier.Tier4 => 4,
			_               => throw new ArgumentOutOfRangeException()
		};
	}

	public static int SpinInterval_sec(this Table table)
	{
		return 30;
	}
}