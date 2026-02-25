using System.Net.Http.Json;
using shared;

public interface IRazorContext
{
	public Task Load();
}

public class LobbyContext(HttpClient http) : IRazorContext
{
	public List<TableDto>? Tables { get; set; }

	public async Task Load()
	{
		HttpResponseMessage res = await http.GetAsync("/tables");
		if (res.IsSuccessStatusCode)
		{
			Tables = await res.Content.ReadFromJsonAsync<List<TableDto>>();
		}
	}
}

public class GameContext(HttpClient http, string tableId) : IRazorContext
{
	public List<ChatMessageDto>? Chat { get; set; }
	public List<LdbEntryDto>? Ldb { get; set; }
	public GameBoardDto? Board { get; set; }

	public async Task Load()
	{
		HttpResponseMessage stateRes = await http.GetAsync($"/tables/{tableId}/state");
		if (stateRes.IsSuccessStatusCode)
		{
			Board = await stateRes.Content.ReadFromJsonAsync<GameBoardDto>();
		}

		HttpResponseMessage chatRes = await http.GetAsync($"/tables/{tableId}/chat");
		if (chatRes.IsSuccessStatusCode)
		{
			List<ChatMessageDto>? history = await chatRes.Content.ReadFromJsonAsync<List<ChatMessageDto>>();
			history?.ForEach(HandleNewMsg);
		}

		await FetchLdb();
	}

	public async Task FetchLdb()
	{
		HttpResponseMessage ldbRes = await http.GetAsync($"/tables/{tableId}/leaderboard");
		if (ldbRes.IsSuccessStatusCode)
		{
			Ldb = await ldbRes.Content.ReadFromJsonAsync<List<LdbEntryDto>>();
		}
	}

	public void HandleNewMsg(ChatMessageDto cm)
	{
		Chat ??= new List<ChatMessageDto>();
		if (Chat.All(x => x.Id != cm.Id))
		{
			Chat.Add(cm);
			if (Chat.Count > 100)
				Chat.RemoveAt(0);
		}
	}

	public void HandleSpin(SpinResultDto spin)
	{
		if (Board != null)
		{
			Board.spinResult = spin;
			Board.Bets.Clear();
		}
	}

	public void HandleRemoteBet(BetDto bet)
	{
		if (Board != null && Board.Bets.All(b => b.Id != bet.Id))
		{
			Board.Bets.Add(bet);
		}
	}
}

partial class AppState
{
	public PlayerDto? Player { get; private set; }
	public LobbyContext? LobbyContext { get; private set; }
	public GameContext? GameContext { get; private set; }

	public async Task RefreshLogin()
	{
		LobbyContext = null;
		GameContext = null;
	}

	public async Task RefreshLobby()
	{
		GameContext = null;
		LobbyContext = new LobbyContext(http);
		Dirty();

		await LobbyContext.Load();

		Dirty();
	}

	public async Task RefreshGame(string tableId)
	{
		LobbyContext = null;
		GameContext = new GameContext(http, tableId);
		Dirty();

		await GameContext.Load();

		Dirty();
	}
}