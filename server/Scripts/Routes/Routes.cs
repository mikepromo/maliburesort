public static class Routes
{
	public const string NORMAL = nameof(NORMAL);
	public const string AUTH = nameof(AUTH);
	public const string BILLING = nameof(BILLING);

	public static void MapRouters(WebApplication app)
	{
		app.MapGet("/", () => "Welcome to the Malibu Resort.\n" +
		                      "Where the sun laughs and monkeys walk in gold.");

		RouteGroupBuilder authGroup = app.MapGroup("/auth")
			.RequireRateLimiting(AUTH);

		authGroup.MapPost("/register", Auth.Register);
		authGroup.MapPost("/login", Auth.Login);
		authGroup.MapPost("/refresh", Auth.Refresh);
		authGroup.MapPost("/logout", Auth.Logout)
			.RequireAuthorization();

		RouteGroupBuilder playerGroup = app.MapGroup("/players")
			.RequireAuthorization();

		playerGroup.MapPost("/deposit", Wallet.Deposit)
			.RequireRateLimiting(BILLING);
		playerGroup.MapPost("/withdraw", Wallet.Withdraw)
			.RequireRateLimiting(BILLING);
		playerGroup.MapGet("/balance", Wallet.Balance)
			.RequireRateLimiting(NORMAL);

		RouteGroupBuilder tablesGroup = app.MapGroup("/tables")
			.RequireAuthorization();

		tablesGroup.MapGet("/", Tables.ListTables)
			.RequireRateLimiting(NORMAL);

		tablesGroup.MapPost("/{id}/join", Tables.JoinTable)
			.RequireRateLimiting(NORMAL);

		tablesGroup.MapPost("/{id}/leave", Tables.LeaveTable)
			.RequireRateLimiting(NORMAL);

		tablesGroup.MapPost("/{id}/bet", Tables.PlaceBet)
			.RequireRateLimiting(BILLING);

		tablesGroup.MapGet("/{id}/chat", Chat.GetChat)
			.RequireRateLimiting(NORMAL);

		tablesGroup.MapPost("/{id}/chat", Chat.SendInChat)
			.RequireRateLimiting(NORMAL);

		tablesGroup.MapGet("/{id}/leaderboard", Ldb.GetLeaderboard)
			.RequireRateLimiting(NORMAL);
	}
}

public record PlayerCredentials(string Name, string Pass);
public record WalletTransaction(decimal Amount);
public record PlaceBetRequest(int ChosenNumber, decimal Amount);
public record SendChatRequest(string Message);