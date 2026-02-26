using Microsoft.AspNetCore.SignalR.Client;
using shared;

partial class AppState
{
	HubConnection? _hub;
	string? _activeSubscribedTableId;

	async Task ConnectHub()
	{
		if (_hub != null)
		{
			if (_hub.State != HubConnectionState.Disconnected) return;
			await _hub.DisposeAsync();
		}
		
		string hubUrl = http.BaseAddress!.ToString().TrimEnd('/') + "/hubs/game";

		_hub = new HubConnectionBuilder()
			.WithUrl($"{hubUrl}?access_token={Jwt}")
			.WithAutomaticReconnect()
			.Build();

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
			});

		_hub.On<SpinResultDto>(nameof(IGameClient.ReceiveSpin),
			async d =>
			{
				if (GameContext?.Board != null && Player != null)
				{
					var myBets = GameContext.Board.Bets
						.Where(b => b.PlayerId == Player.Id)
						.ToList();

					if (myBets.Count > 0)
					{
						decimal totalWagerOnWinner = myBets
							.Where(b => b.ChosenNumber == d.WinningNumber)
							.Sum(b => b.Amount);

						if (totalWagerOnWinner > 0)
						{
							decimal netProfit = totalWagerOnWinner * 36; 
							Cinf($"[SUCCESS] +USD {netProfit:N2} (NODE {d.WinningNumber})");
						}
						else
						{
							decimal totalLoss = myBets.Sum(b => b.Amount);
							Serr($"[LOSS] -USD {totalLoss:N2}");
						}
					}
				}

				GameContext?.HandleSpin(d);
				if (GameContext != null) await GameContext.FetchLdb();
				Dirty();
			});

		_hub.On<ChatMessageDto>(nameof(IGameClient.ReceiveChat),
			d =>
			{
				GameContext?.HandleNewMsg(d);
				Dirty();
			});




		_hub.Reconnecting += (error) =>
		{
			_activeSubscribedTableId = null;
			Cinf("[SYS] DATA LINK FLICKER. RESETTING DATA LINK.");
			return Task.CompletedTask;
		};

		_hub.Closed += (error) =>
		{
			_activeSubscribedTableId = null;
			return Task.CompletedTask;
		};

		_hub.Reconnected += async (connectionId) =>
		{
			Cinf("[SYS] NETWORK RECOVERED. RE-ESTABLISHING DATA LINK...");
			await EnsureTableSubscription();
		};



		try
		{
			await _hub.StartAsync();
			Cinf("[SYS] CONNECTED.");

			await EnsureTableSubscription();
		}
		catch (Exception ex)
		{
			Cex(ex);
		}
	}


	public async Task EnsureTableSubscription()
	{
		if (_hub == null || _hub.State != HubConnectionState.Connected) return;

		string? targetId = Player?.CurrentTableId;

		//; if we are already subscribed to the correct target, do nothing.
		if (_activeSubscribedTableId == targetId) return;

		try
		{
			//; if we are subscribed to something ELSE, kill it first. 
			if (!string.IsNullOrEmpty(_activeSubscribedTableId))
			{
				await _hub.InvokeAsync(ServerRPC.UnsubscribeFromTable, _activeSubscribedTableId);
				_activeSubscribedTableId = null;
			}

			//; if we have a new target, establish the link.
			if (!string.IsNullOrEmpty(targetId))
			{
				await _hub.InvokeAsync(ServerRPC.SubscribeToTable, targetId);
				_activeSubscribedTableId = targetId;
			}
		}
		catch (Exception ex)
		{
			Cex(new Exception("WS DESYNC FAILURE: " + ex.Message));
		}
	}

	public async Task EnsureTableUnsubscription(string tableId)
	{
		if (_hub == null || _hub.State != HubConnectionState.Connected) return;

		try
		{
			await _hub.InvokeAsync(ServerRPC.UnsubscribeFromTable, tableId);
			if (_activeSubscribedTableId == tableId) _activeSubscribedTableId = null;
			Cinf("[SYS] LINK SEVERED.");
		}
		catch (Exception ex)
		{
			Cex(new Exception("WS DESYNC FAILURE: " + ex.Message));
		}
	}
}