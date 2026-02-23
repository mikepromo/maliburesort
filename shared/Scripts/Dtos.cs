namespace shared;

//; HTTP section
public static class DtoExtensions
{
	public static ErrorResponse Err(this string str, string? code = null)
	{
		return new ErrorResponse(str, code);
	}
}

public record ErrorResponse(string Message, string? Code);
public record VersionResponse(string Version);
public record PlayerCredentials(string Name, string Pass);
public record JWTResponse(string JWT, PlayerDto Player);
public record WalletTransaction(decimal Amount);
public record PlaceBetRequest(int ChosenNumber, decimal Amount);
public record SendChatRequest(string Message);

public class PlayerDto
{
	public required string Id { get; set; }
	public required string Name { get; set; }
	public decimal Balance { get; set; }
}

public class BetDto
{
	public required string Id { get; set; }
	public required string TableId { get; set; }
	public required string PlayerId { get; set; }
	public required string PlayerName { get; set; }
	public int ChosenNumber { get; set; }
	public decimal Amount { get; set; }
}

public class LdbEntryDto
{
	public required string PlayerId { get; set; }
	public required string PlayerName { get; set; }
	public decimal NetProfit { get; set; }
}

public class ChatMessageDto
{
	public required string Id { get; set; }
	public required string TableId { get; set; }
	public required string PlayerId { get; set; }
	public required string PlayerName { get; set; }
	public required string Message { get; set; }
	public DateTime SentAt { get; set; }
}

public class TableDto
{
	public required string Id { get; set; }
	public required string Name { get; set; }
	public TableTier Tier { get; set; }
	public int PlayerCount { get; set; }

	public decimal MinBet => Tier.MinBet();
	public decimal MaxBet => Tier.MaxBet();
	public decimal MaxSeats => Tier.MaxSeats();
}

public class GameStateDto
{
	public required TableDto table { get; set; }
	public required SpinResultDto spinResult { get; set; }
	public List<PlayerDto> Players { get; set; } = new();
	public List<BetDto> Bets { get; set; } = new();
}

//; RPC section
public class SpinResultDto
{
	public int? WinningNumber { get; set; }
	public DateTime NextSpinTime { get; set; }
}