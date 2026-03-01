public enum TableTier
{
	Tier1,
	Tier2,
	Tier3,
	Tier4
}

public static class TableSchema
{
	public static decimal MinBet(this TableTier tier)
	{
		return tier switch
		{
			TableTier.Tier1 => 10,
			TableTier.Tier2 => 20,
			TableTier.Tier3 => 30,
			TableTier.Tier4 => 50,
			_               => throw new ArgumentOutOfRangeException()
		};
	}

	public static decimal MaxBet(this TableTier tier)
	{
		return tier switch
		{
			TableTier.Tier1 => 1000,
			TableTier.Tier2 => 2000,
			TableTier.Tier3 => 3000,
			TableTier.Tier4 => 5000,
			_               => throw new ArgumentOutOfRangeException()
		};
	}

	public static int MaxSeats(this TableTier tier)
	{
		return tier switch
		{
			TableTier.Tier1 => 400,
			TableTier.Tier2 => 200,
			TableTier.Tier3 => 100,
			TableTier.Tier4 => 40,
			_               => throw new ArgumentOutOfRangeException()
		};
	}

	public static int SpinInterval_sec(this TableTier tier)
	{
		return 30;
	}
}