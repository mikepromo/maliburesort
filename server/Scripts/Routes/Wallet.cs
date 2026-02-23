using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using shared;

public static class Wallet
{
	public static async Task<IResult> Balance(ClaimsPrincipal user, MainDbContext db)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");

		Player? player = await db.Players.FindAsync(playerId);
		if (player is null) return Results.NotFound();
		decimal balance = player.Balance;

		return Results.Ok(new WalletTransaction(balance));
	}

	public static async Task<IResult> Deposit(ClaimsPrincipal user, WalletTransaction request, MainDbContext db,
		IHubContext<GameHub> hub)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");

		Player? player = await db.Players.FindAsync(playerId);
		if (player is null)
			return Results.NotFound();

		decimal amount = request.Amount;

		string? depositError = Validation.IsValidDeposit(amount);
		if (depositError != null)
			return Results.BadRequest(new { Message = depositError });

		player.Balance += amount;

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.User(player.Id).SendAsync(RPC.BalanceUpdate, player.Balance);

		return Results.Ok(new WalletTransaction(player.Balance));
	}

	public static async Task<IResult> Withdraw(ClaimsPrincipal user, WalletTransaction request, MainDbContext db,
		IHubContext<GameHub> hub)
	{
		Player? player = await user.GetPlayerSecure(db);
		if (player == null) return Results.Unauthorized();

		decimal amount = request.Amount;

		string? withdrawalError = Validation.IsValidWithdrawal(amount);
		if (withdrawalError != null)
			return Results.BadRequest(new { Message = withdrawalError });

		if (amount > player.Balance)
			return Results.BadRequest(new
				{ Message = $"Insufficient funds" });

		player.Balance -= amount;

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		await hub.Clients.User(player.Id).SendAsync(RPC.BalanceUpdate, player.Balance);

		return Results.Ok(new WalletTransaction(player.Balance));
	}
}