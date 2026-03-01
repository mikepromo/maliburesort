namespace shared;

public static class LedgerConf
{
	public const string SYS_PREFIX = "SYS_";
	public const string PLAYER_PREFIX = "PLAYER_";
	public const string EXTERNAL = SYS_PREFIX + nameof(EXTERNAL);
	public const string HOUSE = SYS_PREFIX + nameof(HOUSE);

	public static string GetAccountId(this string playerId)
	{
		return PLAYER_PREFIX + playerId;
	}

	public static bool IsSys(this string accountId)
	{
		return accountId.StartsWith(SYS_PREFIX);
	}

	public const string NON_ZERO_SUM = nameof(NON_ZERO_SUM);
	public const string INVALID_LEGS = nameof(INVALID_LEGS);
	public const string INSUFFICIENT_FUNDS = nameof(INSUFFICIENT_FUNDS);
	public const string CONCURRENCY_CONFLICT = nameof(CONCURRENCY_CONFLICT);
	public const string IDEMPOTENT_REPLAY = nameof(IDEMPOTENT_REPLAY);
}