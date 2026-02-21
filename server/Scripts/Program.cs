using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

ConnectDb();

builder.Services.AddHostedService<SpinService>();

if (builder.Environment.IsDevelopment())
{
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen();
}

InitAuth();

WebApplication app = builder.Build();

await InitDb();

if (builder.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

routes.MapRouters(app);

app.Run();

void ConnectDb()
{
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

async Task InitDb()
{
	using (IServiceScope scope = app.Services.CreateScope())
	{
		MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
		db.Database.Migrate();

		await tables.SeedTables(db);
	}
}