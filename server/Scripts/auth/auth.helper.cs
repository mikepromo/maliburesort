using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

partial class auth
{
	public const int NameMin = 4;
	public const int NameMax = 16;

	public const int PassMin = 4;
	public const int PassMax = 16;

	public const string NamePattern = @"^[a-zA-Z]+$";
	public const string NamePatternDescription = "letters only (a-z, A-Z)";
	public const string PassPattern = @"^[a-zA-Z0-9]+$";
	public const string PassPatternDescription = "letters and numbers only (a-z, A-Z, 0-9)";

	static bool IsValidName(string val)
	{
		return !string.IsNullOrEmpty(val) &&
		       val.Length >= NameMin &&
		       val.Length <= NameMax &&
		       Regex.IsMatch(val, NamePattern);
	}

	static bool IsValidPass(string val)
	{
		return !string.IsNullOrEmpty(val) &&
		       val.Length >= PassMin &&
		       val.Length <= PassMax &&
		       Regex.IsMatch(val, PassPattern);
	}
}


public static class ClaimsExtensions
{
	public static bool GetPlayerId(this ClaimsPrincipal user, out string id)
	{
		id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
		return id != string.Empty;
	}

	public static async Task<Player?> GetPlayerSecure(this ClaimsPrincipal user, MainDbContext db)
	{
		if (!user.GetPlayerId(out string playerId))
			return null;

		string? tokenVersion = user.FindFirst(ClaimTypes.Version)?.Value;

		if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(tokenVersion))
			return null;

		Player? player = await db.Players.FindAsync(playerId);
		if (player == null || player.JWTVersion != tokenVersion)
			return null;

		return player;
	}
}