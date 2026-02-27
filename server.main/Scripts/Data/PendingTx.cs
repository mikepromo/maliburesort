using shared;

public class PendingTx
{
	//; IdempotencyKey (business‑derived)
	public string Id { get; set; } = null!;
	public string Type { get; set; } = null!;
	public string Status { get; set; } = null!;
	public string PlayerId { get; set; } = null!;
	public decimal Amount { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ProcessedAt { get; set; }
	public string? Log { get; set; }
	public uint Version { get; set; }

	public const string SPEND = nameof(SPEND);
	public const string PAYOUT = nameof(PAYOUT);
	public const string DEPOSIT = nameof(DEPOSIT);
	public const string WITHDRAWAL = nameof(WITHDRAWAL);

	public const string PENDING = nameof(PENDING);
	public const string CRACKED = nameof(CRACKED);
	public const string SUCCESS = nameof(SUCCESS);
	public const string FAILURE = nameof(FAILURE);

	public TxRequest FormTxRequest()
	{
		return Type switch
		{
			DEPOSIT => new TxRequest(
				Id,
				Type,
				[
					new TxLeg(LedgerAccounts.EXTERNAL, -Amount),
					new TxLeg(PlayerId.GetAccountId(), Amount)
				]),

			WITHDRAWAL => new TxRequest(
				Id,
				Type,
				[
					new TxLeg(LedgerAccounts.EXTERNAL, Amount),
					new TxLeg(PlayerId.GetAccountId(), -Amount)
				]),

			SPEND => new TxRequest(
				Id,
				Type,
				[
					new TxLeg(LedgerAccounts.HOUSE, Amount),
					new TxLeg(PlayerId.GetAccountId(), -Amount)
				]),

			PAYOUT => new TxRequest(
				Id,
				Type,
				[
					new TxLeg(LedgerAccounts.HOUSE, -Amount),
					new TxLeg(PlayerId.GetAccountId(), Amount)
				]),

			_ => throw new InvalidOperationException($"Unknown tx type: {Type}")
		};
	}

	public bool Crack()
	{
		if (Status != PENDING) return false;
		Status = CRACKED;
		AppendLog(CRACKED);
		return true;
	}

	public void Complete()
	{
		Status = SUCCESS;
		ProcessedAt = DateTime.UtcNow;
		AppendLog(SUCCESS);
	}

	public void Fail(string err)
	{
		Status = FAILURE;
		ProcessedAt = DateTime.UtcNow;
		AppendLog(err);
	}

	void AppendLog(string line)
	{
		if (!string.IsNullOrEmpty(Log)) Log += '\n';
		else Log = string.Empty;
		Log += $"{DateTime.UtcNow}: {line}";
	}
}