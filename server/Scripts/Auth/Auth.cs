using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using shared;

public static partial class Auth
{
	public static async Task<IResult> Me(ClaimsPrincipal user, MainDbContext db)
	{
		Player? player = await user.GetPlayerSecure(db);
		if (player is null)
		{
			return Results.Unauthorized();
		}

		return Results.Ok(player.Wrap());
	}

	public static async Task<IResult> Register(PlayerCredentials request, MainDbContext db)
	{
		string? nameError = Validation.IsValidName(request.Name);
		if (nameError != null)
			return Results.Conflict(nameError.Err());

		string? passError = Validation.IsValidPass(request.Pass);
		if (passError != null)
			return Results.Conflict(passError.Err());

		string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Pass);
		string nameNormalised = request.Name.ToLowerInvariant();

		if (await db.Players.AnyAsync(p => p.NameNormalized == nameNormalised))
			return Results.Conflict("Name already taken.".Err());

		Player player = new()
		{
			Id = Guid.NewGuid().ToString(),
			JWTVersion = Guid.NewGuid().ToString(),
			Name = request.Name,
			NameNormalized = nameNormalised,
			PasswordHash = passwordHash,
			Balance = 0
		};

		await db.Players.AddAsync(player);

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		return Results.Ok(player.Wrap());
	}

	public static async Task<IResult> Login(PlayerCredentials request, MainDbContext db, HttpContext context,
		IConfiguration config)
	{
		//; ensure name uniqueness
		string normalized = request.Name.ToLowerInvariant();
		Player? player = await db.Players
			.FirstOrDefaultAsync(p => p.NameNormalized == normalized);

		if (player == null)
			return Results.NotFound("Player not found".Err());

		//; verify password
		if (!BCrypt.Net.BCrypt.Verify(request.Pass, player.PasswordHash))
			return Results.Conflict("Wrong password".Err());

		//; generate JWT
		string? key = config["Jwt:Key"];

		if (key is null || !GenerateJWT(player, key, out string jwt))
			return Results.InternalServerError();

		//; generate refresh token
		string refreshToken = Guid.NewGuid().ToString();
		player.RefreshToken = refreshToken;
		player.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		//; send refresh token back to client
		context.Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.Strict,
			Expires = player.RefreshTokenExpiry
		});

		return Results.Ok(new JWTResponse(jwt, player.Wrap()));
	}

	public static async Task<IResult> Refresh(HttpContext context, MainDbContext db,
		IConfiguration config)
	{
		if (!context.Request.Cookies.TryGetValue("refreshToken", out string? refreshToken))
			return Results.Unauthorized();

		Player? player = await db.Players.FirstOrDefaultAsync(p => p.RefreshToken == refreshToken);

		if (player == null || player.RefreshTokenExpiry <= DateTime.UtcNow)
			return Results.Unauthorized();

		string? key = config["Jwt:Key"];

		if (key is null || !GenerateJWT(player, key, out string jwt))
			return Results.InternalServerError();

		return Results.Ok(new JWTResponse(jwt, player.Wrap()));
	}

	public static async Task<IResult> Logout(ClaimsPrincipal user, MainDbContext db, HttpContext context)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found".Err());

		Player? player = await db.Players.FindAsync(playerId);
		if (player is null)
			return Results.NotFound("Player not found".Err());

		player.JWTVersion = Guid.NewGuid().ToString();
		player.RefreshToken = null;

		IResult? error = await db.TrySaveAsync_HTTP();
		if (error is not null) return error;

		context.Response.Cookies.Delete("refreshToken");
		return Results.NoContent();
	}

	static bool GenerateJWT(Player player, string key, out string tokenString)
	{
		tokenString = string.Empty;

		SecurityTokenDescriptor tokenDescriptor = new()
		{
			Subject = new ClaimsIdentity([
				new Claim(ClaimTypes.NameIdentifier, player.Id),
				new Claim(ClaimTypes.Version, player.JWTVersion)
			]),
			Expires = DateTime.UtcNow.AddMinutes(15),
			SigningCredentials = new SigningCredentials(
				new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key)),
				SecurityAlgorithms.HmacSha256Signature)
		};

		JwtSecurityTokenHandler tokenHandler = new();
		SecurityToken? token = tokenHandler.CreateToken(tokenDescriptor);
		string? tokenString_nullable = tokenHandler.WriteToken(token);

		if (tokenString_nullable == null)
			return false;

		tokenString = tokenString_nullable;
		return true;
	}

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