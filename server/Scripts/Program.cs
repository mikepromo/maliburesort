using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

public class Program
{
	public static async Task Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

		ConnectDb();

		builder.Services.AddSingleton<TableManager>();
		builder.Services.AddHostedService<SpinService>();

		if (builder.Environment.IsDevelopment())
		{
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();
		}

		if (!builder.Environment.IsDevelopment())
		{
			InitRateLimiting();
		}

		InitAuth();

		WebApplication app = builder.Build();

		InitExHandling();

		await InitDb();

		if (builder.Environment.IsDevelopment())
		{
			app.UseSwagger();
			app.UseSwaggerUI();
		}

		app.UseAuthentication();
		app.UseAuthorization();

		if (!builder.Environment.IsDevelopment())
		{
			app.UseRateLimiter();
		}

		Routes.MapRouters(app);

		app.Run();

		void ConnectDb()
		{
			builder.Services.AddDbContext<MainDbContext>(options =>
			{
				options.UseNpgsql(
					builder.Configuration.GetConnectionString("DefaultConnection") 
					?? throw new Exception("Database Connection String is missing!"));
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
				options.AddPolicy(Routes.AUTH, context => GetIpPartition(context, 5));
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
			using (IServiceScope scope = app.Services.CreateScope())
			{
				MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
				await db.Database.MigrateAsync();

				await db.SeedTables();
			}
		}
	}
}