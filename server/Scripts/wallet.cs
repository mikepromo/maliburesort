public static class wallet
{
	public const decimal MIN_DEPOSIT = 1_000;
	public const decimal MAX_DEPOSIT = 1_000_000;
	public const decimal MIN_WITHDRAWAL = 1_000;

	public static async Task<IResult> Balance(string id, MainDbContext db)
	{
		Player? player = await db.Players.FindAsync(id);
		if (player is null) return Results.NotFound();
		decimal balance = player.Balance;

		return Results.Ok(new
			{ Id = id, Balance = balance });
	}

	public static async Task<IResult> Deposit(string id, WalletTransaction request, MainDbContext db)
	{
		Player? player = await db.Players.FindAsync(id);
		if (player is null)
			return Results.NotFound();

		decimal amount = request.Amount;

		if (amount < MIN_DEPOSIT || amount > MAX_DEPOSIT)
			return Results.BadRequest(new
				{ Message = $"Invalid amount. Min/max deposit is {MIN_DEPOSIT}/{MAX_DEPOSIT}" });

		player.Balance += amount;
		await db.SaveChangesAsync();

		return Results.Ok(new { Message = "Deposit successful", Id = id, NewBalance = player.Balance });
	}

	public static async Task<IResult> Withdraw(string id, WalletTransaction request, MainDbContext db)
	{
		Player? player = await db.Players.FindAsync(id);
		if (player is null)
			return Results.NotFound();

		decimal amount = request.Amount;

		if (amount < MIN_WITHDRAWAL)
			return Results.BadRequest(new
				{ Message = $"Invalid amount. Min withdrawal is {MIN_WITHDRAWAL}" });

		if (amount > player.Balance)
			return Results.BadRequest(new
				{ Message = $"Insufficient funds" });

		player.Balance -= amount;
		await db.SaveChangesAsync();

		return Results.Ok(new { Message = "Withdrawal successful", Id = id, NewBalance = player.Balance });
	}
}