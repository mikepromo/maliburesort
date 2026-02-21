using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

public static class auth
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

	public static async Task<IResult> Register(PlayerCredentials request, MainDbContext db)
	{
		if (!IsValidName(request.Name))
			return Results.Conflict($"Invalid Name format. Must be {NameMin}-{NameMax} {NamePatternDescription}");

		if (!IsValidPass(request.Pass))
			return Results.Conflict($"Invalid Password format. Must be {PassMin}-{PassMax} {PassPatternDescription}");

		string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Pass);

		Player player = new()
		{
			Id = Guid.NewGuid().ToString(),
			Name = request.Name,
			NameNormalized = request.Name.ToLowerInvariant(),
			PasswordHash = passwordHash,
			Balance = 0
		};

		try
		{
			await db.Players.AddAsync(player);
			await db.SaveChangesAsync();
		}
		catch (DbUpdateException)
		{
			return Results.Conflict("Name already taken");
		}

		return Results.Created($"/player/{player.Id}", new { player.Id });
	}

	public static async Task<IResult> Login(PlayerCredentials request, MainDbContext db)
	{
		string normalized = request.Name.ToLowerInvariant();

		Player? player = await db.Players
			.FirstOrDefaultAsync(p => p.NameNormalized == normalized);

		if (player == null || !BCrypt.Net.BCrypt.Verify(request.Pass, player.PasswordHash))
			return Results.Unauthorized();

		return Results.Ok(new { player.Id, player.Name, player.Balance, message = "Logged in" });
	}

	public static async Task<IResult> Logout(string id, MainDbContext db)
	{
		Player? player = await db.Players.FindAsync(id);
		if (player is null)
			return Results.NotFound();

		return Results.Ok(new { message = "Logged out" });
	}
}