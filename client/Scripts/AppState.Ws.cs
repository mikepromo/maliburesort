using Microsoft.AspNetCore.SignalR.Client;
using shared;

partial class AppState
{
	HubConnection? _hub;
	public HubConnection Hub => _hub!;

	public event Action<ChatMessageDto>? OnChatReceived;
	public event Action<BetDto>? OnBetPlaced;
	public event Action<SpinResultDto>? OnSpinResulted;
	public event Action? OnLdbUpdated;

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

		_hub.On<decimal>(nameof(IGameClient.BalanceUpdate), bal =>
		{
			Player!.Balance = bal;
		});

		_hub.On<PlayerDto>(nameof(IGameClient.PlayerJoined),
			d => Cinf($"[SYS] {d.Name} connected."));

		_hub.On<PlayerDto>(nameof(IGameClient.PlayerLeft),
			d => Cinf($"[SYS] {d.Name} disconnected."));

		_hub.On<BetDto>(nameof(IGameClient.BetPlaced),
			d =>
			{
				OnBetPlaced?.Invoke(d);
				Cinf($"[SYS] Bet {d.Amount} on {d.ChosenNumber} placed.");
			});

		_hub.On<SpinResultDto>(nameof(IGameClient.ReceiveSpin),
			d =>
			{
				OnSpinResulted?.Invoke(d);
				OnLdbUpdated?.Invoke();
				Cinf($"[SYS] Winning number is {d.WinningNumber}!");
			});

		_hub.On<ChatMessageDto>(nameof(IGameClient.ReceiveChat),
			d => { OnChatReceived?.Invoke(d); });

		await _hub.StartAsync();
	}
}