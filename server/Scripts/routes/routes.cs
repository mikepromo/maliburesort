public static class routes
{
	public static void MapRouters(WebApplication app)
	{
		app.MapGet("/", () => "Welcome to the Malibu Resort.\n" +
		                      "Where the sun laughs and monkeys walk in gold.");

		app.MapPost("/auth/register", auth.Register);
		app.MapPost("/auth/login", auth.Login);
		app.MapPost("/auth/refresh", auth.Refresh);
		app.MapPost("/auth/logout", auth.Logout).RequireAuthorization();

		app.MapPost("/players/deposit", wallet.Deposit).RequireAuthorization();
		app.MapPost("/players/withdraw", wallet.Withdraw).RequireAuthorization();
		app.MapGet("/players/balance", wallet.Balance).RequireAuthorization();

		app.MapGet("/tables", tables.ListTables).RequireAuthorization();
		app.MapPost("/tables/{id}/join", tables.JoinTable).RequireAuthorization();
		app.MapPost("/tables/{id}/leave", tables.LeaveTable).RequireAuthorization();

		app.MapPost("/tables/{id}/bet", tables.PlaceBet).RequireAuthorization();

		app.MapGet("/tables/{id}/chat", chat.GetChat).RequireAuthorization();
		app.MapPost("/tables/{id}/chat", chat.SendInChat).RequireAuthorization();
		app.MapGet("/tables/{id}/leaderboard", ldb.GetLeaderboard).RequireAuthorization();
	}
}

public record PlayerCredentials(string Name, string Pass);

public record WalletTransaction(decimal Amount);

public record PlaceBetRequest(int ChosenNumber, decimal Amount);

public record SendChatRequest(string Message);