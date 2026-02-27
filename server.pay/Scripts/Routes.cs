using shared;

public static class Routes
{
	public static void MapRouters(WebApplication app)
	{
		app.MapPost("/internal/transfer", async (TxRequest req, Ledger ledger) =>
		{
			string? errorCode = await ledger.ExecuteTransfer(req);

			if (errorCode != null)
				return Results.BadRequest(errorCode.Err());

			Dictionary<string, decimal> newBalances = req.Legs
				.Select(l => l.AccountId)
				.Distinct()
				.ToDictionary(id => id, id => ledger.GetBalance(id).Result);

			return Results.Ok(new TxValueDict(newBalances));
		});

		app.MapGet("/internal/balance/{accountId}", async (string accountId, Ledger ledger) =>
		{
			decimal balance = await ledger.GetBalance(accountId);
			//; we ALWAYS return legit value here
			return Results.Ok(new TxValue(balance));
		});
	}
}