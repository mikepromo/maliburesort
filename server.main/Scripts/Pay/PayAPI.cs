using Microsoft.AspNetCore.SignalR;
using shared;

public class TxProcRes
{
	public TxValue? TxValue { get; set; }
	public string? Error { get; set; }

	public bool IsSuccess(out string error, out TxValue txValue)
	{
		error = Error ?? string.Empty;
		txValue = TxValue ?? new TxValue(0);
		return Error == null && TxValue != null;
	}
}

public static class Pay
{
	const int maxRetries = 3;
	const int delayMs = 100;

	public static async Task<TxProcRes> ProcessPendingTx(PendingTx pending, MainDbContext db,
		IHttpClientFactory httpClientFactory, IHubContext<GameHub, IGameClient> hub)
	{
		if (!pending.Crack())
			return new TxProcRes { Error = "Internal Error" };

		DbSaveResult crackSaveResult = await db.TrySaveAsync();
		if (await db.TrySaveAsync() is not DbSaveResult.Success)
			return new TxProcRes { Error = $"Database failure: {crackSaveResult}" };

		TxRequest txReq = pending.FormTxRequest();
		TxProcRes result = await ProcessTxRequest(pending.PlayerId, txReq, httpClientFactory);

		if (result.Error == null) pending.Complete();
		else pending.Fail(result.Error);

		DbSaveResult paySaveResult = await db.TrySaveAsync();
		if (paySaveResult is not DbSaveResult.Success)
		{
			return new TxProcRes { Error = $"Database failure: {paySaveResult}" };
		}

		if (result.TxValue != null)
			await hub.Clients.User(pending.PlayerId).BalanceUpdate(result.TxValue);

		return result;
	}

	static async Task<TxProcRes> ProcessTxRequest(string playerId, TxRequest txReq,
		IHttpClientFactory httpClientFactory)
	{
		for (int i = 0; i < maxRetries; i++)
		{
			TxProcRes result = new();

			try
			{
				HttpClient payClient = httpClientFactory.CreateClient("PayService");
				HttpResponseMessage response = await payClient.PostAsJsonAsync("/internal/transfer", txReq);

				if (response.IsSuccessStatusCode)
				{
					Dictionary<string, decimal>? balances =
						await response.Content.ReadFromJsonAsync<Dictionary<string, decimal>>();

					if (balances == null || !balances.TryGetValue(playerId.GetAccountId(), out decimal newBalance))
					{
						result.Error = $"Communication schema mismatch.";
						return result;
					}

					result.TxValue = new TxValue(newBalance);
					return result;
				}

				ErrorResponse? error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
				if (error?.Code == "IDEMPOTENT_REPLAY")
				{
					//; log idempotent retry
					return await GetPlayerBalance(playerId, httpClientFactory);
				}

				if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
				{
					result.Error = $"{response.StatusCode.ToString()}";
					return result;
				}
			}
			catch (Exception ex)
			{
				result.Error = ex.Message;
				continue;
			}

			if (i < maxRetries - 1)
				await Task.Delay(delayMs);
		}

		return new TxProcRes { Error = "Time Out." };
	}

	public static async Task<TxProcRes> GetPlayerBalance(string playerId,
		IHttpClientFactory httpClientFactory)
	{
		for (int i = 0; i < maxRetries; i++)
		{
			TxProcRes result = new();

			try
			{
				HttpClient payClient = httpClientFactory.CreateClient("PayService");
				HttpResponseMessage response = await payClient.GetAsync($"/internal/balance/{playerId.GetAccountId()}");

				if (response.IsSuccessStatusCode)
				{
					TxValue? txValue = await response.Content.ReadFromJsonAsync<TxValue>();
					result.TxValue = txValue;
					if (txValue == null)
					{
						result.Error = $"Communication schema mismatch.";
					}

					return result;
				}

				if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
				{
					result.Error = $"{response.StatusCode.ToString()}";
					return result;
				}
			}
			catch (Exception ex)
			{
				result.Error = ex.Message;
				continue;
			}

			if (i < maxRetries - 1)
				await Task.Delay(delayMs);
		}

		return new TxProcRes { Error = "Time Out." };
	}
}