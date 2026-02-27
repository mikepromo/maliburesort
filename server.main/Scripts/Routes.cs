using System.Reflection;

public static class Routes
{
	public const string NORMAL = nameof(NORMAL);
	public const string AUTH = nameof(AUTH);
	public const string BILLING = nameof(BILLING);

	public static void MapRouters(WebApplication app)
	{
		app.MapGet("/", () => "Welcome to the Malibu Resort API.")
			.RequireRateLimiting(NORMAL);

		app.MapGet("/version", () => new { Version = Version.VersionOf(Assembly.GetExecutingAssembly()) })
			.AllowAnonymous()
			.RequireRateLimiting(NORMAL);

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
		playerGroup.MapGet("/me", Auth.Me)
			.RequireRateLimiting(NORMAL);

		RouteGroupBuilder tablesGroup = app.MapGroup("/tables")
			.RequireAuthorization();

		tablesGroup.MapGet("/", Tables.ListTables)
			.RequireRateLimiting(NORMAL);
		tablesGroup.MapGet("/{tableId}/state", Tables.GetTableState)
			.RequireRateLimiting(NORMAL);
		tablesGroup.MapPost("/{tableId}/join", Tables.JoinTable)
			.RequireRateLimiting(NORMAL);
		tablesGroup.MapPost("/{tableId}/leave", Tables.LeaveTable)
			.RequireRateLimiting(NORMAL);
		tablesGroup.MapPost("/{tableId}/bet", Tables.PlaceBet)
			.RequireRateLimiting(BILLING);
		tablesGroup.MapGet("/{tableId}/chat", Chat.GetChat)
			.RequireRateLimiting(NORMAL);
		tablesGroup.MapPost("/{tableId}/chat", Chat.SendInChat)
			.RequireRateLimiting(NORMAL);
		tablesGroup.MapGet("/{tableId}/leaderboard", Ldb.GetLeaderboard)
			.RequireRateLimiting(NORMAL);
	}
}