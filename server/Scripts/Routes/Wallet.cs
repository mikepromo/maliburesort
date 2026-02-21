using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class Wallet
{
	public const decimal MIN_DEPOSIT = 1_000;
	public const decimal MAX_DEPOSIT = 1_000_000;
	public const decimal MIN_WITHDRAWAL = 1_000;

	public static async Task<IResult> Balance(ClaimsPrincipal user, MainDbContext db)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");

		Player? player = await db.Players.FindAsync(playerId);
		if (player is null) return Results.NotFound();
		decimal balance = player.Balance;

		return Results.Ok(new { PlayerId = playerId, Balance = balance });
	}

	public static async Task<IResult> Deposit(ClaimsPrincipal user, WalletTransaction request, MainDbContext db)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");

		Player? player = await db.Players.FindAsync(playerId);
		if (player is null)
			return Results.NotFound();

		decimal amount = request.Amount;

		if (amount < MIN_DEPOSIT || amount > MAX_DEPOSIT)
			return Results.BadRequest(new
				{ Message = $"Invalid amount. Min/max deposit is {MIN_DEPOSIT}/{MAX_DEPOSIT}" });

		player.Balance += amount;
	
		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		return Results.Ok(new { Message = "Deposit successful", Id = playerId, NewBalance = player.Balance });
	}

	public static async Task<IResult> Withdraw(ClaimsPrincipal user, WalletTransaction request, MainDbContext db)
	{
		Player? player = await user.GetPlayerSecure(db);
		if (player == null) return Results.Unauthorized();

		decimal amount = request.Amount;

		if (amount < MIN_WITHDRAWAL)
			return Results.BadRequest(new
				{ Message = $"Invalid amount. Min withdrawal is {MIN_WITHDRAWAL}" });

		if (amount > player.Balance)
			return Results.BadRequest(new
				{ Message = $"Insufficient funds" });

		player.Balance -= amount;

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		return Results.Ok(new { Message = "Withdrawal successful", PlayerId = player.Id, NewBalance = player.Balance });
	}
}