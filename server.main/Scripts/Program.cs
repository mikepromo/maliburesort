using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

public class Program
{
	public static async Task Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

		ConnectDb();

		builder.Services.AddSingleton<TableManager>();
		builder.Services.AddHostedService<SpinService>();
		builder.Services.AddHostedService<PendingTxProcesser>();
		builder.Services.AddSignalR();

		ConnectPay();

		if (builder.Environment.IsDevelopment())
		{
			builder.Services.AddEndpointsApiExplorer();
		}

		if (!builder.Environment.IsDevelopment())
		{
			InitRateLimiting();
		}

		InitAuth();

		WebApplication app = builder.Build();

		InitExHandling();

		await InitDb();

		app.UseAuthentication();
		app.UseAuthorization();

		if (!builder.Environment.IsDevelopment())
		{
			app.UseRateLimiter();
		}

		Routes.MapRouters(app);

		app.MapHub<GameHub>("/hubs/game");

		app.Run();

		void ConnectDb()
		{
			builder.Services.AddDbContext<MainDbContext>(options =>
			{
				options.UseNpgsql(
					builder.Configuration.GetConnectionString("DefaultConnection")
					?? throw new Exception("DefaultConnection is missing!"));
			});
		}

		void ConnectPay()
		{
			builder.Services.AddHttpClient("PayService", client =>
			{
				client.BaseAddress = new Uri(builder.Configuration["PayService:BaseUrl"]
				                             ?? throw new Exception("PayService:BaseUrl not configured"));
				client.Timeout = TimeSpan.FromSeconds(30);
			});
		}

		void InitRateLimiting()
		{
			builder.Services.AddRateLimiter(options =>
			{
				options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

				RateLimitPartition<string> GetIpPartition(HttpContext context, int limit, int seconds = 60)
				{
					return RateLimitPartition.GetFixedWindowLimiter(
						context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
						_ => new FixedWindowRateLimiterOptions
						{
							PermitLimit = limit,
							Window = TimeSpan.FromSeconds(seconds)
						});
				}

				options.AddPolicy(Routes.BILLING, context => GetIpPartition(context, 1));
				options.AddPolicy(Routes.AUTH, context => GetIpPartition(context, 10));
				options.AddPolicy(Routes.NORMAL, context => GetIpPartition(context, 100));
			});
		}

		void InitAuth()
		{
			string? jwtkey = builder.Configuration["Jwt:Key"];
			if (jwtkey is null)
				throw new Exception("Jwt Key is not specified in your system env vars");

			byte[] key = Encoding.ASCII.GetBytes(jwtkey);
			builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
				.AddJwtBearer(options =>
				{
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidateIssuerSigningKey = true,
						IssuerSigningKey = new SymmetricSecurityKey(key),
						ValidateIssuer = false,
						ValidateAudience = false
					};

					options.Events = new JwtBearerEvents
					{
						OnMessageReceived = context =>
						{
							StringValues accessToken = context.Request.Query["access_token"];
							PathString path = context.HttpContext.Request.Path;
							if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
							{
								context.Token = accessToken;
							}
							return Task.CompletedTask;
						}
					};
				});

			builder.Services.AddAuthorization();
		}

		void InitExHandling()
		{
			app.UseExceptionHandler(exceptionHandlerApp =>
			{
				exceptionHandlerApp.Run(async context =>
				{
					context.Response.StatusCode = StatusCodes.Status500InternalServerError;
					context.Response.ContentType = "application/json";
					await context.Response.WriteAsJsonAsync(new { Message = "An unexpected error occurred" });
				});
			});
		}

		async Task InitDb()
		{
			using IServiceScope scope = app.Services.CreateScope();
			MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();

			if (args.Contains("--reset-db"))
				await db.Database.EnsureDeletedAsync();

			await db.Database.MigrateAsync();

			await db.SeedTables();
		}
	}
}