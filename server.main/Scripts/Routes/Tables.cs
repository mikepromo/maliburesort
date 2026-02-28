using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using shared;

public static class Tables
{
	public static async Task<IResult> ListTables(MainDbContext db)
	{
		List<TableDto> tables = await db.Tables
			.Include(t => t.Players)
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
		GameBoardDto? state = await db.Tables
			.Where(t => t.Id == tableId)
			.Include(t => t.Players)
			.Select(t => new GameBoardDto
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
		MainDbContext db, IHubContext<GameHub, IGameClient> hub, IHttpClientFactory httpClientFactory)
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

		double timeUntilSpin = (table.NextSpinTime - DateTime.UtcNow).TotalSeconds;
		if (timeUntilSpin < 2)
			return Results.BadRequest("Too close to next spin, wait a moment".Err());

		TxProcRes balProcRes = await Pay.GetPlayerBalance(playerId, httpClientFactory);
		if(!balProcRes.IsSuccess(out string balError, out TxValue balVal))
			return Results.BadRequest(balError.Err());

		if (request.Amount > balVal.Value)
			return Results.BadRequest("Insufficient funds".Err());
		
		string betId = Guid.NewGuid().ToString();

		PendingTx pending = new()
		{
			Id = $"BetId_{betId}",
			Type = PendingTx.SPEND,
			Status = PendingTx.PENDING,
			PlayerId = playerId,
			Amount = request.Amount,
			CreatedAt = DateTime.UtcNow
		};
		db.PendingTxs.Add(pending);

		IResult? txSaveError = await db.TrySaveAsync_HTTP();
		if (txSaveError != null) 
			return txSaveError;
		
		TxProcRes spendProcRes = await Pay.ProcessPendingTx(pending, db, httpClientFactory, hub);
		if (spendProcRes.Error != null)
			return Results.BadRequest(spendProcRes.Error.Err());

		Bet bet = new()
		{
			Id = betId,
			PlayerId = player.Id,
			TableId = tableId,
			ChosenNumber = request.ChosenNumber,
			Amount = request.Amount,
			PlacedAt = DateTime.UtcNow,
			IsResolved = false
		};
		db.Bets.Add(bet);
			
		player.LastActiveAt = DateTime.UtcNow;

		IResult? betSaveError = await db.TrySaveAsync_HTTP();
		if (betSaveError is not null)
		{
			//; we charged, but failed to place -> log
			//; potentially such cases should be automatically refunded
			//; but for this project the user report and the log will suffice
			return betSaveError;
		}

		await hub.Clients.Group(tableId).BetPlaced(bet.Wrap());

		return Results.NoContent();
	}
}