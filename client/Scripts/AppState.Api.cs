using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;
using shared;

public partial class AppState
{
	public async Task GetServerVersion()
	{
		VersionResponse? response = await http.GetFromJsonAsync<VersionResponse>("/version");
		ServerVersion = response?.Version;

		Version.VersionOf(Assembly.GetExecutingAssembly());
	}

	public async Task SyncPlayer()
	{
		HttpResponseMessage res = await http.GetAsync("/players/me");
		if (res.IsSuccessStatusCode)
		{
			Player = await res.Content.ReadFromJsonAsync<PlayerDto>();
			Dirty();
			Cinf($"[SYS] IDENTITY RESTORED: {Player?.Name.ToUpper()}");
		}
		else
		{
			await ClearJWT();
		}
	}

	public async Task<bool> Register(string name, string pass)
	{
		Cinf("PROCESSING REGISTRATION...");

		try
		{
			PlayerCredentials payload = new(name, pass);
			HttpResponseMessage res = await http.PostAsJsonAsync("/auth/register", payload);

			if (res.IsSuccessStatusCode)
			{
				Cinf("IDENTITY CREATED. PLEASE AUTHENTICATE");
				return true;
			}

			await Chttp(res);
			return false;
		}
		catch (Exception ex)
		{
			Cex(ex);
			return false;
		}
	}

	public async Task Login(string name, string pass)
	{
		Cinf("VERIFYING CREDENTIALS...");

		try
		{
			HttpResponseMessage res = await http.PostAsJsonAsync("/auth/login", new { Name = name, Pass = pass });
			if (res.IsSuccessStatusCode)
			{
				JWTResponse? data = await res.Content.ReadFromJsonAsync<JWTResponse>();

				if (data == null)
				{
					Cerr("AUTH ERROR");
					return;
				}

				Player = data.Player;

				await SetJWT(data.JWT);
				await ConnectHub();
				Dirty();

				if (!string.IsNullOrEmpty(Player.CurrentTableId))
				{
					Cinf($"SESSION RECOVERED.");
					nav.NavigateTo($"/game/{Player.CurrentTableId}");
				}
				else
				{
					Cinf("SUCCESSFUL AUTHENTICATION.");
					nav.NavigateTo("/lobby");
				}
			}
			else
			{
				await Chttp(res);
			}
		}
		catch (Exception ex)
		{
			Cex(ex);
		}
	}

	public async Task Logout()
	{
		Cinf("TERMINATING SESSION...");

		if (Player?.CurrentTableId != null)
		{
			await LeaveTable();
		}

		await http.PostAsJsonAsync("/auth/logout", new { });

		await ClearJWT();

		if (_hub != null)
		{
			await _hub.StopAsync();
			await _hub.DisposeAsync();
			_hub = null;
		}

		Dirty();
		nav.NavigateTo("/");
		Cinf("SESSION TERMINATED. SYSTEM READY.");
	}

	public async Task PlaceBet(int nmb, decimal amt)
	{
		if (Player?.CurrentTableId == null)
		{
			Cerr($"NO TABLE ASSIGNED");
			return;
		}

		Cinf($"PROCESSING BET: USD {amt:N2} ON NUMBER {nmb}...");

		HttpResponseMessage res =
			await http.PostAsJsonAsync($"/tables/{Player.CurrentTableId}/bet", new PlaceBetRequest(nmb, amt));

		if (res.IsSuccessStatusCode)
		{
			WalletTransaction? data = await res.Content.ReadFromJsonAsync<WalletTransaction>();
			Cinf($"BET ACCEPTED. NEW BALANCE: USD {data?.Amount:N2}");
		}
		else
		{
			await Chttp(res);
		}
	}

	public async Task Deposit(decimal amount)
	{
		Cinf($"PROCESSING DEPOSIT: USD {amount:N2}...");

		HttpResponseMessage res = await http.PostAsJsonAsync("/players/deposit", new WalletTransaction(amount));

		if (res.IsSuccessStatusCode)
		{
			WalletTransaction? data = await res.Content.ReadFromJsonAsync<WalletTransaction>();
			Cinf($"DEPOSIT APPROVED. NEW BALANCE: USD {data?.Amount:N2}");
		}
		else
		{
			await Chttp(res);
		}
	}

	public async Task Withdraw(decimal amount)
	{
		Cinf($"PROCESSING WITHDRAWAL: USD {amount:N2}...");

		HttpResponseMessage res = await http.PostAsJsonAsync("/players/withdraw", new WalletTransaction(amount));

		if (res.IsSuccessStatusCode)
		{
			WalletTransaction? data = await res.Content.ReadFromJsonAsync<WalletTransaction>();
			Cinf($"WITHDRAWAL APPROVED. NEW BALANCE: USD {data?.Amount:N2}");
		}
		else
		{
			await Chttp(res);
		}
	}

	public async Task CheckBalance()
	{
		Cinf("FETCHING ACCOUNT BALANCE...");
		HttpResponseMessage res = await http.GetAsync("/players/balance");

		if (res.IsSuccessStatusCode)
		{
			WalletTransaction? data = await res.Content.ReadFromJsonAsync<WalletTransaction>();
			if (data != null && Player != null)
			{
				Player.Balance = data.Amount;
				Dirty();
				Cinf($"ACCOUNT BALANCE: USD {data.Amount:N2}");
			}
		}
		else
		{
			await Chttp(res);
		}
	}

	public async Task JoinTable(string tableId)
	{
		if (Player?.CurrentTableId != null)
		{
			Cerr($"ALREADY ASSIGNED TO TABLE {Player.CurrentTableId}");
			return;
		}

		Cinf($"JOINING TABLE {tableId}...");
		HttpResponseMessage res = await http.PostAsJsonAsync($"/tables/{tableId}/join", new { });

		if (res.IsSuccessStatusCode)
		{
			Player!.CurrentTableId = tableId;

			if (Hub.State == HubConnectionState.Connected)
				await Hub.InvokeAsync(ServerRPC.SubscribeToTable, tableId);

			nav.NavigateTo($"/game/{tableId}");
		}
		else
		{
			await Chttp(res);
		}
	}

	public async Task LeaveTable()
	{
		if (Player?.CurrentTableId == null)
		{
			Cerr($"NO TABLE ASSIGNED");
			return;
		}

		string tableId = Player.CurrentTableId;
		Cinf("LEAVING TABLE...");

		HttpResponseMessage res = await http.PostAsJsonAsync($"/tables/{tableId}/leave", new { });

		if (res.IsSuccessStatusCode)
		{
			Player.CurrentTableId = null;

			if (Hub.State == HubConnectionState.Connected)
				await Hub.InvokeAsync(ServerRPC.UnsubscribeFromTable, tableId);

			nav.NavigateTo("/lobby");
		}
		else
		{
			await Chttp(res);
		}
	}

	public async Task SendInChat(string message)
	{
		if (string.IsNullOrEmpty(message)) return;

		if (Player?.CurrentTableId == null)
		{
			Cerr($"NO TABLE ASSIGNED");
			return;
		}

		if (message.Length > 280)
			message = message[..280];

		HttpResponseMessage res =
			await http.PostAsJsonAsync($"/tables/{Player.CurrentTableId}/chat", new SendChatRequest(message));

		if (!res.IsSuccessStatusCode)
			await Chttp(res);
	}
}