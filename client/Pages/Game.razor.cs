using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using shared;

namespace client.Pages;

public partial class Game
{
	[Parameter]
	public string TableId { get; set; }
	GameStateDto? gameState;
	List<string> logs = new();
	Dictionary<int, decimal> MyBets = new();
	int timeLeft;
	Timer? timer;

	protected override async Task OnInitializedAsync()
	{
		State.OnConsoleCommand += HandleConsoleCommand;

		await Http.PostAsJsonAsync($"/tables/{TableId}/join", new { });

		await State.Hub.InvokeAsync(RPC.SubscribeToTable, TableId);
		State.Hub.On<string>(RPC.PlayerJoined, p => Log($"[SYS] {p} connected."));
		State.Hub.On<string>(RPC.PlayerLeft, p => Log($"[SYS] {p} disconnected."));
		State.Hub.On<dynamic>(RPC.BetPlaced,
			(data) => Log($"[GAME] {data.name} bet ${data.amount} on {data.chosenNumber}"));
		State.Hub.On<dynamic>(RPC.ReceiveSpin, OnSpin);
		State.Hub.On<dynamic>(RPC.ReceiveChat, (data) => Log($"[{data.playerName}]: {data.message}"));

		gameState = await Http.GetFromJsonAsync<GameStateDto>($"/tables/{TableId}/state");

		StartTimer();
	}

	async void HandleConsoleCommand(string cmd, string[] args)
	{
		await InvokeAsync(async () =>
		{
			switch (cmd)
			{
				case "BET":
					if (args.Length == 2 && int.TryParse(args[0], out int n) &&
					    decimal.TryParse(args[1], out decimal amt))
					{
						await PlaceBet(n, amt);
					}
					else
					{
						Log("[CLI] SYNTAX ERROR: BET <NUM> <AMT>");
					}
					break;

				case "CHAT":
				case "MSG":
					string msg = string.Join(" ", args);
					await SendChat(msg);
					break;
			}
		});
	}

	async Task PlaceBet(int num, decimal amount)
	{
		HttpResponseMessage res =
			await Http.PostAsJsonAsync($"/tables/{TableId}/bet", new { ChosenNumber = num, Amount = amount });
		if (res.IsSuccessStatusCode)
		{
			if (!MyBets.ContainsKey(num)) MyBets[num] = 0;
			MyBets[num] += amount;
		}
		else
		{
			await State.Chttp(res);
		}
	}

	async Task SendChat(string message)
	{
		await Http.PostAsJsonAsync($"/tables/{TableId}/chat", new { Message = message });
	}

	void OnSpin(dynamic data)
	{
		int win = data.winningNumber;
		Log($"*** WINNER: {win} ***");
		MyBets.Clear();

		DateTime next = data.nextSpinTime;
		gameState!.NextSpinTime = next;
		StartTimer();
		StateHasChanged();
	}

	void StartTimer()
	{
		timer?.Dispose();
		timer = new Timer(_ =>
		{
			double sec = (gameState!.NextSpinTime - DateTime.UtcNow).TotalSeconds;
			timeLeft = sec > 0 ? (int)sec : 0;
			InvokeAsync(StateHasChanged);
		}, null, 0, 1000);
	}

	void Log(string msg)
	{
		logs.Insert(0, msg);
		InvokeAsync(StateHasChanged);
	}

	public void Dispose()
	{
		State.OnConsoleCommand -= HandleConsoleCommand;
		timer?.Dispose();
		State.Hub.InvokeAsync(RPC.UnsubscribeFromTable, TableId);
		Http.PostAsJsonAsync($"/tables/{TableId}/leave", new { });
	}
}