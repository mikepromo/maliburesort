using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public static class Tables
{
	public static async Task<IResult> ListTables(MainDbContext db)
	{
		List<TableDto> tables = await db.Tables
			.Select(t => new TableDto
			{
				Id = t.Id,
				Name = t.Name,
				Tier = t.Tier,
				PlayerCount = t.Players.Count
			})
			.ToListAsync();

		return Results.Ok(tables);
	}

	public static async Task<IResult> GetTableState(string tableId, MainDbContext db)
	{
		GameStateDto? state = await db.Tables
			.Where(t => t.Id == tableId)
			.Select(t => new GameStateDto
			{
				table = new TableDto
				{
					Id = t.Id,
					Name = t.Name,
					Tier = t.Tier,
					PlayerCount = t.Players.Count
				},
				spinResult = new SpinResultDto
				{
					NextSpinTime = t.NextSpinTime,
					WinningNumber = t.LastWinningNumber
				},
				Players = t.Players.Select(p => new PlayerDto
				{
					Id = p.Id,
					Name = p.Name,
					Balance = p.Balance,
					CurrentTableId = p.CurrentTableId
				}).ToList(),
				Bets = t.Bets.Where(b => !b.IsResolved).Select(b => new BetDto
				{
					Id = b.Id,
					TableId = b.TableId,
					PlayerId = b.PlayerId,
					PlayerName = b.Player.Name,
					ChosenNumber = b.ChosenNumber,
					Amount = b.Amount
				}).ToList()
			})
			.FirstOrDefaultAsync();

		if (state == null) return Results.NotFound("Table not found".Err());
		return Results.Ok(state);
	}

	public static async Task<IResult> JoinTable(string tableId, ClaimsPrincipal user, MainDbContext db,
		IHubContext<GameHub, IGameClient> hub)
	{
		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == tableId);

		if (table == null)
			return Results.NotFound("Table not found".Err());

		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found".Err());

		Player? player = await db.Players.FindAsync(playerId);
		if (player == null)
			return Results.NotFound("Player not found".Err());

		if (player.CurrentTableId == tableId)
			return Results.Conflict("Player already at this table".Err());

		if (player.CurrentTableId != null)
			return Results.Conflict("You are already at another table. Leave it first.".Err());

		if (table.Players.Count >= table.Tier.MaxSeats())
			return Results.BadRequest("Table is full".Err());

		player.CurrentTableId = tableId;
		player.LastActiveAt = DateTime.UtcNow;

		table.Players.Add(player);

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(tableId).PlayerJoined(player.Wrap());

		return Results.Ok(table.Wrap());
	}

	public static async Task<IResult> LeaveTable(string tableId, ClaimsPrincipal user, MainDbContext db,
		IHubContext<GameHub, IGameClient> hub)
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
			return Results.NotFound("Player not at this table".Err());

		table.Players.Remove(player);

		player.CurrentTableId = null;
		player.LastActiveAt = DateTime.UtcNow;

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(tableId).PlayerLeft(player.Wrap());

		return Results.Ok(table.Wrap());
	}

	public static async Task<IResult> PlaceBet(string tableId, ClaimsPrincipal user, PlaceBetRequest request,
		MainDbContext db, IHubContext<GameHub, IGameClient> hub)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found".Err());

		Player? player = await user.GetPlayerSecure(db);
		if (player == null)
			return Results.Unauthorized();

		Table? table = await db.Tables
			.Include(t => t.Players)
			.FirstOrDefaultAsync(t => t.Id == tableId);

		if (table == null)
			return Results.NotFound("Table not found".Err());

		if (!table.Players.Any(p => p.Id == playerId))
			return Results.BadRequest("You must join the table first".Err());

		if (request.Amount < table.Tier.MinBet() ||
		    request.Amount > table.Tier.MaxBet())
			return Results.BadRequest($"Bet must be between {table.Tier.MinBet()} and {table.Tier.MaxBet()}".Err());

		if (request.ChosenNumber < 0 || request.ChosenNumber > 36)
			return Results.BadRequest("Number must be between 0 and 36".Err());

		if (request.Amount <= 0)
			return Results.BadRequest("Bet amount must be positive".Err());

		if (player.Balance < request.Amount)
			return Results.BadRequest("Insufficient funds".Err());

		double timeUntilSpin = (table.NextSpinTime - DateTime.UtcNow).TotalSeconds;
		if (timeUntilSpin < 2)
			return Results.BadRequest("Too close to next spin, wait a moment".Err());

		player.Balance -= request.Amount;
		player.LastActiveAt = DateTime.UtcNow;

		Bet bet = new()
		{
			Id = Guid.NewGuid().ToString(),
			PlayerId = player.Id,
			TableId = tableId,
			ChosenNumber = request.ChosenNumber,
			Amount = request.Amount,
			PlacedAt = DateTime.UtcNow,
			IsResolved = false
		};

		db.Bets.Add(bet);

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.Group(tableId).BetPlaced(bet.Wrap());

		await hub.Clients.User(bet.PlayerId).BalanceUpdate(bet.Player.Balance);

		return Results.Ok(bet.Wrap());
	}
}