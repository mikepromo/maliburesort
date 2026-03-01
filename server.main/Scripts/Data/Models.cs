using shared;

public class Player
{
	public string Id { get; set; } = null!;
	public string Name { get; set; } = null!;
	public string NameNormalized { get; set; } = null!;

	public string PasswordHash { get; set; } = null!;
	public string JWTVersion { get; set; } = null!;
	public string? RefreshToken { get; set; }
	public DateTime RefreshTokenExpiry { get; set; }

	public DateTime LastActiveAt { get; set; }
	public string? CurrentTableId { get; set; }
	public Table? CurrentTable { get; set; }

	public List<Bet> Bets { get; set; } = new();
	public List<ChatMessage> ChatMessages { get; set; } = new();
	public uint Version { get; set; }

	public PlayerDto Wrap()
	{
		return new PlayerDto
		{
			Id = Id,
			Name = Name,
			CurrentTableId = CurrentTableId
		};
	}
}

public class Table
{
	public string Id { get; set; } = null!;
	public string Name { get; set; } = null!;
	public TableTier Tier { get; set; }
	public DateTime NextSpinTime { get; set; }
	public int? LastWinningNumber { get; set; }

	public List<Player> Players { get; set; } = new();
	public List<ChatMessage> ChatMessages { get; set; } = new();
	public List<Bet> Bets { get; set; } = new();

	public TableDto Wrap()
	{
		return new TableDto
		{
			Id = Id,
			Name = Name,
			PlayerCount = Players.Count,
			MinBet = Tier.MinBet(),
			MaxBet = Tier.MaxBet(),
			MaxSeats= Tier.MaxSeats(),
		};
	}
}

public class Bet
{
	public string Id { get; set; } = null!;
	public string PlayerId { get; set; } = null!;
	public string TableId { get; set; } = null!;

	public int ChosenNumber { get; set; }
	public decimal Amount { get; set; }
	public DateTime PlacedAt { get; set; }

	public int? WinningNumber { get; set; }
	public decimal Payout { get; set; }
	public bool IsResolved { get; set; }
	public DateTime ResolvedAt { get; set; }

	public Player Player { get; set; } = null!;
	public Table Table { get; set; } = null!;

	public BetDto Wrap()
	{
		return new BetDto
		{
			Id = Id,
			TableId = TableId,
			PlayerId = PlayerId,
			PlayerName = Player.Name,
			ChosenNumber = ChosenNumber,
			Amount = Amount
		};
	}
}

public class ChatMessage
{
	public string Id { get; set; } = null!;
	public string TableId { get; set; } = null!;
	public string PlayerId { get; set; } = null!;

	public string Message { get; set; } = null!;
	public DateTime SentAt { get; set; }

	public Player Player { get; set; } = null!;
	public Table Table { get; set; } = null!;

	public ChatMessageDto Wrap()
	{
		return new ChatMessageDto
		{
			Id = Id,
			TableId = TableId,
			PlayerId = PlayerId,
			PlayerName = Player.Name,
			Message = Message,
			SentAt = SentAt
		};
	}
}