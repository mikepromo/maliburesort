using shared;

public static class Routes
{
	public static void MapRouters(WebApplication app)
	{
		app.MapPost("/internal/transfer", async (TxRequest req, Ledger ledger) =>
		{
			try
			{
				string? errorCode = await ledger.ExecuteTransfer(req);

				if (errorCode != null && errorCode != LedgerConf.IDEMPOTENT_REPLAY)
					return Results.BadRequest(errorCode.Err());

				Dictionary<string, decimal> newBalances = req.Legs
					.Select(l => l.AccountId)
					.Distinct()
					.ToDictionary(id => id, id => ledger.GetBalance(id).Result);

				return Results.Ok(new TxValueDict(newBalances));
			}
			catch (Exception ex)
			{
				return Results.BadRequest($"[FATAL] ExecuteTransfer threw: {ex}".Err());
			}
		});

		app.MapGet("/internal/balance/{accountId}", async (string accountId, Ledger ledger) =>
		{
			try
			{
				decimal balance = await ledger.GetBalance(accountId);
				//; we ALWAYS return legit value here
				return Results.Ok(new TxValue(balance));
			}
			catch (Exception ex)
			{
				return Results.BadRequest($"[FATAL] GetBalance threw: {ex}".Err());
			}
		});
	}
}