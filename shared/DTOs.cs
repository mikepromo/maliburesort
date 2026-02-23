namespace shared;

public record VersionResponse(string Version);

// --- AUTH ---
public record PlayerCredentials(string Name, string Pass);
public record JWTResponse(string JWT, PlayerDTO Player);

// --- WALLET ---
public record WalletTransaction(decimal Amount);

// --- GAME ACTIONS ---
public record PlaceBetRequest(int ChosenNumber, decimal Amount);
public record SendChatRequest(string Message);

public class PlayerDTO
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public decimal Balance { get; set; }
}

public class BetDTO
{
	public string PlayerId { get; set; } = "";
	public string PlayerName { get; set; } = "";
	public int ChosenNumber { get; set; }
	public decimal Amount { get; set; }
}

public class LdbEntryDTO
{
	public string PlayerId { get; set; } = "";
	public string PlayerName { get; set; } = "";
	public decimal NetProfit { get; set; }
}

public class ChatMessageDTO
{
	public string? Id { get; set; }
	public string? PlayerId { get; set; }
	public string? PlayerName { get; set; }
	public string? Message { get; set; }
	public DateTime SentAt { get; set; }
}

public class TableDto
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public TableTier Tier { get; set; }
	public int PlayerCount { get; set; }
}

public class TableStateDto
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public TableTier Tier { get; set; }
	public DateTime NextSpinTime { get; set; }
	public List<PlayerDTO> Players { get; set; } = new();
	public List<BetDTO> Bets { get; set; } = new();
}