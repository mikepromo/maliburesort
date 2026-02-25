using Microsoft.AspNetCore.SignalR.Client;
using shared;

partial class AppState
{
	HubConnection? _hub;
	public HubConnection Hub => _hub!;

	async Task ConnectHub()
	{
		_hub = new HubConnectionBuilder()
			.WithUrl($"{http.BaseAddress}hubs/game?access_token={Jwt}")
			.WithAutomaticReconnect()
			.Build();

		_hub.Reconnected += async (connectionId) =>
		{
			Cinf("[SYS] NETWORK RECOVERED. RE-ESTABLISHING DATA LINK...");
			if (!string.IsNullOrEmpty(Player?.CurrentTableId))
			{
				await _hub.InvokeAsync(ServerRPC.SubscribeToTable, Player.CurrentTableId);
			}
		};

		_hub.On<decimal>(nameof(IGameClient.BalanceUpdate), bal => { Player!.Balance = bal; });

		_hub.On<PlayerDto>(nameof(IGameClient.PlayerJoined),
			d => Cinf($"[SYS] {d.Name} connected."));

		_hub.On<PlayerDto>(nameof(IGameClient.PlayerLeft),
			d => Cinf($"[SYS] {d.Name} disconnected."));

		_hub.On<BetDto>(nameof(IGameClient.BetPlaced),
			d =>
			{
				GameContext?.HandleRemoteBet(d);
				Dirty();
				Cinf($"[SYS] Bet {d.Amount} on {d.ChosenNumber} placed.");
			});

		_hub.On<SpinResultDto>(nameof(IGameClient.ReceiveSpin),
			async d =>
			{
				GameContext?.HandleSpin(d);
				if (GameContext != null) await GameContext.FetchLdb();
				Dirty();
				Cinf($"[SYS] Winning number is {d.WinningNumber}!");
			});

		_hub.On<ChatMessageDto>(nameof(IGameClient.ReceiveChat),
			d =>
			{
				GameContext?.HandleNewMsg(d);
				Dirty();
			});

		await _hub.StartAsync();
	}
}