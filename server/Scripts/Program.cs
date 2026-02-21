using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

if (builder.Configuration.GetValue<bool>("UseInMemoryDatabase"))
{
	builder.Services.AddDbContext<MainDbContext>(options =>
		options.UseSqlite("DataSource=file::memory:?cache=shared"));
}
else
{
	builder.Services.AddDbContext<MainDbContext>(options =>
		options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")
		                  ?? "Host=localhost;Database=maliburesort_server_db;Username=postgres;Password=pass123"));
}

builder.Services.AddHostedService<SpinService>();

if (builder.Environment.IsDevelopment())
{
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen();
}

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
	MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
	db.Database.Migrate();

	await tables.SeedTables(db);
}

if (builder.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.MapGet("/", () => "Welcome to the Malibu Resort.\n" +
                      "Where the sun laughs and monkeys walk in gold.");

app.MapPost("/auth/register", auth.Register);
app.MapPost("/auth/login", auth.Login);
app.MapPost("/auth/logout", auth.Logout);

app.MapPost("/players/{id}/deposit", wallet.Deposit);
app.MapPost("/players/{id}/withdraw", wallet.Withdraw);
app.MapGet("/players/{id}/balance", wallet.Balance);

app.MapGet("/tables", tables.ListTables);
app.MapPost("/tables/{id}/join", tables.JoinTable);
app.MapPost("/tables/{id}/leave", tables.LeaveTable);

app.MapPost("/tables/{id}/bet", tables.PlaceBet);

app.MapGet("/tables/{id}/chat", chat.GetChat);
app.MapPost("/tables/{id}/chat", chat.SendInChat);
app.MapGet("/tables/{id}/leaderboard", ldb.GetLeaderboard);

app.Run();


public record PlayerCredentials(string Name, string Pass);

public record WalletTransaction(decimal Amount);

public record JoinTableRequest(string PlayerId);

public record LeaveTableRequest(string PlayerId);

public record PlaceBetRequest(string PlayerId, int ChosenNumber, decimal Amount);

public record SendChatRequest(string PlayerId, string Message);