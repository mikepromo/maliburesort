using System.Text.RegularExpressions;

namespace shared;

public static class Validation
{
	public const decimal MIN_DEPOSIT = 1_000;
	public const decimal MAX_DEPOSIT = 1_000_000;
	public const decimal MIN_WITHDRAWAL = 1_000;

	public static string? IsValidDeposit(decimal val)
	{
		bool fine = val >= MIN_DEPOSIT && val <= MAX_DEPOSIT;

		if (fine) return null;

		return $"Invalid amount [{val}]. Min/max deposit is {MIN_DEPOSIT}/{MAX_DEPOSIT}";
	}

	public static string? IsValidWithdrawal(decimal val)
	{
		bool fine = val >= MIN_WITHDRAWAL;

		if (fine) return null;

		return $"Invalid amount [{val}]. Min withdrawal is {MIN_WITHDRAWAL}";
	}

	public const int NameMin = 4;
	public const int NameMax = 16;
	public const int PassMin = 4;
	public const int PassMax = 16;

	public const string NamePattern = @"^[a-zA-Z0-9]+$";
	public const string NamePatternDescription = "letters only numbers only (a-z, A-Z, 0-9)";

	public const string PassPattern = @"^[a-zA-Z0-9]+$";
	public const string PassPatternDescription = "letters and numbers only (a-z, A-Z, 0-9)";

	public static string? IsValidName(string val)
	{
		bool fine = !string.IsNullOrEmpty(val) &&
		            val.Length >= NameMin &&
		            val.Length <= NameMax &&
		            Regex.IsMatch(val, NamePattern);

		if (fine) return null;

		return $"Invalid Name format [{val}]. Must be {NameMin}-{NameMax} {NamePatternDescription}";
	}

	public static string? IsValidPass(string val)
	{
		bool fine = !string.IsNullOrEmpty(val) &&
		            val.Length >= PassMin &&
		            val.Length <= PassMax &&
		            Regex.IsMatch(val, PassPattern);

		if (fine) return null;

		return $"Invalid Password format [{val}]. Must be {PassMin}-{PassMax} {PassPatternDescription}";
	}
}