public static class Routes
{
	public static void MapRouters(WebApplication app)
	{
		app.MapGet("/", () => "Welcome to the Malibu Resort.\n" +
		                      "Where the sun laughs and monkeys walk in gold.");

		app.MapPost("/auth/register", Auth.Register);
		app.MapPost("/auth/login", Auth.Login);
		app.MapPost("/auth/refresh", Auth.Refresh);
		app.MapPost("/auth/logout", Auth.Logout).RequireAuthorization();

		app.MapPost("/players/deposit", Wallet.Deposit).RequireAuthorization();
		app.MapPost("/players/withdraw", Wallet.Withdraw).RequireAuthorization();
		app.MapGet("/players/balance", Wallet.Balance).RequireAuthorization();

		app.MapGet("/tables", Tables.ListTables).RequireAuthorization();
		app.MapPost("/tables/{id}/join", Tables.JoinTable).RequireAuthorization();
		app.MapPost("/tables/{id}/leave", Tables.LeaveTable).RequireAuthorization();

		app.MapPost("/tables/{id}/bet", Tables.PlaceBet).RequireAuthorization();

		app.MapGet("/tables/{id}/chat", Chat.GetChat).RequireAuthorization();
		app.MapPost("/tables/{id}/chat", Chat.SendInChat).RequireAuthorization();
		app.MapGet("/tables/{id}/leaderboard", Ldb.GetLeaderboard).RequireAuthorization();
	}
}

public record PlayerCredentials(string Name, string Pass);

public record WalletTransaction(decimal Amount);

public record PlaceBetRequest(int ChosenNumber, decimal Amount);

public record SendChatRequest(string Message);