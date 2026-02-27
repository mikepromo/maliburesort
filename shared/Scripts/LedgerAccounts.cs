public static class LedgerAccounts
{
	public const string SYS_PREFIX = "SYS_";
	public const string PLAYER_PREFIX = "PLAYER_";
	public const string EXTERNAL = SYS_PREFIX+nameof(EXTERNAL);
	public const string HOUSE = SYS_PREFIX+nameof(HOUSE);

	public static string GetAccountId(this string playerId)
	{
		return PLAYER_PREFIX + playerId;
	}
}