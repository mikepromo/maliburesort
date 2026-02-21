using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public static partial class auth
{
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
			JWTVersion = Guid.NewGuid().ToString(),
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

	public static async Task<IResult> Login(PlayerCredentials request, MainDbContext db, HttpContext context,
		IConfiguration config)
	{
		//; ensure name uniqueness
		string normalized = request.Name.ToLowerInvariant();
		Player? player = await db.Players
			.FirstOrDefaultAsync(p => p.NameNormalized == normalized);

		//; verify password
		if (player == null || !BCrypt.Net.BCrypt.Verify(request.Pass, player.PasswordHash))
			return Results.Unauthorized();

		//; generate JWT
		string? key = config["Jwt:Key"];

		if (key is null || !GenerateJWT(player, key, out string jwt))
			return Results.InternalServerError();

		//; generate refresh token
		string refreshToken = Guid.NewGuid().ToString();
		player.RefreshToken = refreshToken;
		player.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
		await db.SaveChangesAsync();

		//; send refresh token back to client
		context.Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = true,
			SameSite = SameSiteMode.Strict,
			Expires = player.RefreshTokenExpiry
		});

		//; reply with token
		return Results.Ok(new
		{
			JWT = jwt,
			Player = new { player.Id, player.Name, player.Balance, Message = "Logged in" }
		});
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

		return Results.Ok(new
		{
			JWT = jwt,
			Message = "Token refreshed"
		});
	}

	public static async Task<IResult> Logout(ClaimsPrincipal user, MainDbContext db, HttpContext context)
	{
		if (!user.GetPlayerId(out string playerId))
			return Results.NotFound("Player not found");

		Player? player = await db.Players.FindAsync(playerId);
		if (player is null)
			return Results.NotFound();

		player.JWTVersion = Guid.NewGuid().ToString();
		player.RefreshToken = null;
		await db.SaveChangesAsync();

		context.Response.Cookies.Delete("refreshToken");
		return Results.Ok(new { message = "Logged out from all devices" });
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
}