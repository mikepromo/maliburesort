using Microsoft.EntityFrameworkCore;

namespace pay;

public class Program
{
	public static async Task Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

		ConnectDb();

		builder.Services.AddScoped<Ledger>();

		WebApplication app = builder.Build();

		InitExHandling();
		Routes.MapRouters(app);
		await InitDb();

		app.Run();

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

		void ConnectDb()
		{
			builder.Services.AddDbContext<PayDbContext>(options =>
			{
				options.UseNpgsql(
					builder.Configuration.GetConnectionString("DefaultConnection")
					?? throw new Exception("DefaultConnection missing!"));
			});
		}

		async Task InitDb()
		{
			using IServiceScope scope = app.Services.CreateScope();
			PayDbContext db = scope.ServiceProvider.GetRequiredService<PayDbContext>();

			if (args.Contains("--reset-db"))
				await db.Database.EnsureDeletedAsync();

			await db.Database.MigrateAsync();
		}
	}
}