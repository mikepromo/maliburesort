using Microsoft.AspNetCore.SignalR.Client;
using shared;

partial class MalibuState
{
	HubConnection? _hub;
	public HubConnection Hub => _hub!;
	
	public event Action<ChatMessageDto>? OnChatReceived;
	public event Action<SpinResultDto>? OnSpinResulted;
	public event Action<BetDto>? OnBetPlaced;

	async Task ConnectHub()
	{
		_hub = new HubConnectionBuilder()
			.WithUrl($"{_http.BaseAddress}hubs/game?access_token={Jwt}")
			.WithAutomaticReconnect()
			.Build();

		_hub.On<decimal>(nameof(IGameClient.BalanceUpdate), bal =>
		{
			Player!.Balance = bal;
			Notify();
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
				Cinf($"[SYS] Winning number is {d.WinningNumber}!");
			});

		_hub.On<ChatMessageDto>(nameof(IGameClient.ReceiveChat),
			d => { OnChatReceived?.Invoke(d); });

		await _hub.StartAsync();
	}
}