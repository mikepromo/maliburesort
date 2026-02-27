using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using shared;

public static class Wallet
{
	public static async Task<IResult> Balance(ClaimsPrincipal user,
		IHttpClientFactory httpClientFactory)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found".Err());
		
		TxProcRes balProcRes = await Pay.GetPlayerBalance(playerId, httpClientFactory);
		if(!balProcRes.IsSuccess(out string balError, out TxValue balVal))
			return Results.BadRequest(balError.Err());

		return Results.Ok(balVal);
	}

	public static async Task<IResult> Deposit(ClaimsPrincipal user, TxValue request, MainDbContext db,
		IHubContext<GameHub, IGameClient> hub, IHttpClientFactory httpClientFactory)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found".Err());

		decimal amount = request.Value;

		string? depositError = Validation.IsValidDeposit(amount);
		if (depositError != null)
			return Results.BadRequest(depositError.Err());

		PendingTx pending = new()
		{
			Id = Guid.NewGuid().ToString(), //; instead get this from Stripe or Bank
			Type = PendingTx.DEPOSIT,
			Status = PendingTx.PENDING,
			PlayerId = playerId,
			Amount = amount,
			CreatedAt = DateTime.UtcNow
		};
		db.PendingTxs.Add(pending);

		IResult? saveError = await db.TrySaveAsync_HTTP();
		if (saveError != null)
		{
			//; log: Deposit was not registered
			return saveError;
		}

		return Results.NoContent();
	}

	public static async Task<IResult> Withdraw(ClaimsPrincipal user, TxValue request, MainDbContext db,
		IHubContext<GameHub, IGameClient> hub, IHttpClientFactory httpClientFactory)
	{
		Player? player = await user.GetPlayerSecure(db);
		if (player == null)
			return Results.Unauthorized();

		string playerId = player.Id;
		decimal amount = request.Value;

		string? withdrawalError = Validation.IsValidWithdrawal(amount);
		if (withdrawalError != null)
			return Results.BadRequest(withdrawalError.Err());

		TxProcRes balProcRes = await Pay.GetPlayerBalance(playerId, httpClientFactory);
		if(!balProcRes.IsSuccess(out string balError, out TxValue balVal))
			return Results.BadRequest(balError.Err());

		if (amount > balVal.Value)
			return Results.BadRequest("Insufficient funds".Err());

		PendingTx pending = new()
		{
			Id = Guid.NewGuid().ToString(), //; we generate it here, its our own
			Type = PendingTx.WITHDRAWAL,
			Status = PendingTx.PENDING,
			PlayerId = playerId,
			Amount = amount,
			CreatedAt = DateTime.UtcNow
		};
		db.PendingTxs.Add(pending);

		IResult? txSaveError = await db.TrySaveAsync_HTTP();
		if (txSaveError != null)
		{
			//; log: Withdrawal was not registered
			return txSaveError;
		}

		return Results.NoContent();
	}
}