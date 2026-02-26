using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using shared;

public class Bot
{
	readonly HttpClient _http;
	readonly Random _rng = new();

	public Bot(string baseAddress)
	{
		_http = new HttpClient { BaseAddress = new Uri(baseAddress) };
	}

	public async Task Boot(string username, string password)
	{
		if (!await Step("REG", await _http.PostAsJsonAsync("/auth/register", new PlayerCredentials(username, password)),
			    true)) return;

		HttpResponseMessage loginRes =
			await _http.PostAsJsonAsync("/auth/login", new PlayerCredentials(username, password));
		if (!await Step("LGN", loginRes)) return;

		JWTResponse? auth = await loginRes.Content.ReadFromJsonAsync<JWTResponse>();
		_http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.JWT);

		if (!await Step("DEP",
			    await _http.PostAsJsonAsync("/players/deposit", new WalletTransaction(Validation.MAX_DEPOSIT / 2))))
			return;


		List<TableDto>? tables = await _http.GetFromJsonAsync<List<TableDto>>("/tables");
		if (tables == null || tables.Count == 0)
		{
			Console.WriteLine($"[ERR] No tables found on network.");
			return;
		}

		TableDto target = tables[_rng.Next(tables.Count)];
		string tableId = target.Id;

		if (!await Step("JOIN", await _http.PostAsJsonAsync($"/tables/{tableId}/join", new { }))) return;

		Console.WriteLine($"[OK] {username} -> {target.Name} (Tier {target.Tier})");


		Console.WriteLine($"[OK] {username} active on {tableId}");

		while (true)
		{
			if (_rng.NextDouble() > 0.15)
			{
				await Bet(tableId);
			}
			// else
			// {
			// 	if (_rng.NextDouble() > 0.7)
			// 		await _http.PostAsJsonAsync($"/tables/{tableId}/chat",
			// 			new SendChatRequest("Just watching this round."));
			// }

			await Task.Delay(_rng.Next(5000, 12000));
		}
	}

	async Task<bool> Step(string op, HttpResponseMessage res, bool allowConflict = false)
	{
		if (res.IsSuccessStatusCode) return true;
		if (allowConflict && res.StatusCode == HttpStatusCode.Conflict) return true;

		string body = await res.Content.ReadAsStringAsync();
		Console.WriteLine($"[FAIL] {op,-10} | {res.StatusCode} | {body}");
		return false;
	}

	async Task Bet(string tableId)
	{
		await Task.Delay(_rng.Next(1000, 5000));

		int number = _rng.Next(0, 37);
		decimal amount = _rng.Next(5, 50);

		await _http.PostAsJsonAsync($"/tables/{tableId}/bet", new PlaceBetRequest(number, amount));

		Console.WriteLine($"Betting on {number}!");

		// if (_rng.NextDouble() > 0.8)
		{
			await _http.PostAsJsonAsync($"/tables/{tableId}/chat", new SendChatRequest($"Betting USD {amount} on {number}!"));
		}
	}
}