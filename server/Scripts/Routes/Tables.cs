using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public static class Tables
{
	public static async Task<IResult> ListTables(MainDbContext db)
	{
		List<TableDto> tables = (await db.Tables.Include(t => t.Players).ToListAsync())
			.Select(t => new TableDto
			{
				Id = t.Id,
				Name = t.Name,
				Tier = t.Tier,
				PlayerCount = t.Players.Count
			}).ToList();

		return Results.Ok(tables);
	}

	public static async Task<IResult> GetTableState(string id, MainDbContext db)
	{
		TableStateDto? table = await db.Tables
			.Include(t => t.Players)
			.Include(t => t.Bets)
			.Where(t => t.Id == id)
			.Select(t => new TableStateDto
			{
				Id = t.Id,
				Name = t.Name,
				Tier = t.Tier,
				NextSpinTime = t.NextSpinTime,
				Players = t.Players.Select(p => new PlayerDTO { Id = p.Id, Name = p.Name, Balance = p.Balance })
					.ToList(),
				Bets = t.Bets.Where(b => !b.IsResolved).Select(b => new BetDTO
				{
					PlayerId = b.PlayerId,
					PlayerName = b.Player.Name,
					ChosenNumber = b.ChosenNumber,
					Amount = b.Amount
				}).ToList()
			})
			.FirstOrDefaultAsync();

		if (table == null) return Results.NotFound("Table not found");

		return Results.Ok(table);
	}

	public static async Task<IResult> JoinTable(string id, ClaimsPrincipal user, MainDbContext db,
		IHubContext<GameHub> hub)
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

		if (table.Players.Count >= table.Tier.MaxSeats())
			return Results.BadRequest("Table is full");

		table.Players.Add(player);

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(id).SendAsync(RPC.PlayerJoined, player.Id);

		return Results.Ok();
	}

	public static async Task<IResult> LeaveTable(string id, ClaimsPrincipal user, MainDbContext db,
		IHubContext<GameHub> hub)
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

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(id).SendAsync(RPC.PlayerLeft, player.Id);

		return Results.Ok();
	}

	public static async Task<IResult> PlaceBet(string id, ClaimsPrincipal user, PlaceBetRequest request,
		MainDbContext db, IHubContext<GameHub> hub)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");

		Player? player = await user.GetPlayerSecure(db); // Secure DB verification
		if (player == null) return Results.Unauthorized();

		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == id);

		if (table == null)
			return Results.NotFound("Table not found");

		if (!table.Players.Any(p => p.Id == playerId))
			return Results.BadRequest("You must join the table first");

		if (request.Amount < table.Tier.MinBet() ||
		    request.Amount > table.Tier.MaxBet())
			return Results.BadRequest($"Bet must be between {table.Tier.MinBet()} and {table.Tier.MaxBet()}");

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

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(id)
			.SendAsync(RPC.BetPlaced, new { player.Id, player.Name, request.Amount, request.ChosenNumber });

		await hub.Clients.User(player.Id).SendAsync(RPC.BalanceUpdate, player.Balance);

		return Results.Ok();
	}
}