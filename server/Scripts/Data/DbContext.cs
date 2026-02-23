using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using shared;

public enum DbSaveResult
{
	Success,
	ConcurrencyConflict,
	DbError,
	Fatal
}

public class MainDbContext : DbContext
{
	public MainDbContext(DbContextOptions<MainDbContext> options) : base(options) { }

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Player>()
			.HasIndex(p => p.NameNormalized)
			.IsUnique();

		modelBuilder.Entity<Player>()
			.Property(p => p.Version)
			.IsRowVersion();

		foreach (IMutableProperty property in modelBuilder.Model.GetEntityTypes()
			         .SelectMany(t => t.GetProperties())
			         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
		{
			property.SetColumnType("decimal(18,2)");
		}
	}

	public DbSet<Player> Players => Set<Player>();
	public DbSet<Table> Tables => Set<Table>();
	public DbSet<Bet> Bets => Set<Bet>();
	public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

	public async Task<IResult?> TrySaveAsync_HTTP()
	{
		DbSaveResult saveResult = await TrySaveAsync();
		if (saveResult is DbSaveResult.Success) return null;
		return saveResult switch
		{
			DbSaveResult.ConcurrencyConflict => Results.Conflict("Please try again."),
			DbSaveResult.DbError             => Results.UnprocessableEntity("Database constraint violation"),
			DbSaveResult.Fatal               => Results.InternalServerError(),
			_                                => throw new ArgumentOutOfRangeException()
		};
	}

	public async Task<DbSaveResult> TrySaveAsync()
	{
		try
		{
			await SaveChangesAsync();
			return DbSaveResult.Success;
		}
		catch (DbUpdateConcurrencyException)
		{
			return DbSaveResult.ConcurrencyConflict;
		}
		catch (DbUpdateException)
		{
			return DbSaveResult.DbError;
		}
		catch
		{
			return DbSaveResult.Fatal;
		}
	}

	public async Task SeedTables()
	{
		if (!Tables.Any())
		{
			List<Table> tables =
			[
				ArrangeTable("Silver Shells", TableTier.Tier1, 5),
				ArrangeTable("Playful Breeze", TableTier.Tier1, 10),
				ArrangeTable("Sunlight Lounge", TableTier.Tier2, 15),
				ArrangeTable("Golden Sands", TableTier.Tier2, 20),
				ArrangeTable("Secret Rendezvous", TableTier.Tier3, 25),
				ArrangeTable("Dolphin's Breath", TableTier.Tier3, 30),
				ArrangeTable("Eternal Bliss", TableTier.Tier4, 35)
			];
			Tables.AddRange(tables);

			if (await TrySaveAsync() is not DbSaveResult.Success)
				throw new Exception();
		}
	}

	static Table ArrangeTable(string title, TableTier tier, int shift_sec)
	{
		return new Table
		{
			Id = Guid.NewGuid().ToString(), Name = title, Tier = tier,
			NextSpinTime = DateTime.UtcNow.AddSeconds(shift_sec)
		};
	}
}

public class Player
{
	public string Id { get; set; } = null!;
	public string Name { get; set; } = null!;
	public string NameNormalized { get; set; } = null!;

	public string PasswordHash { get; set; } = null!;
	public string JWTVersion { get; set; } = null!;
	public string? RefreshToken { get; set; }
	public DateTime RefreshTokenExpiry { get; set; }

	public decimal Balance { get; set; }

	public List<Bet> Bets { get; set; } = new();
	public List<ChatMessage> ChatMessages { get; set; } = new();
	public uint Version { get; set; }

	public PlayerDTO GetDTO()
	{
		return new PlayerDTO
		{
			Id = Id,
			Name = Name,
			Balance = Balance
		};
	}
}

public class Table
{
	public string Id { get; set; } = null!;
	public string Name { get; set; } = null!;
	public TableTier Tier { get; set; }
	public DateTime NextSpinTime { get; set; }

	public List<Player> Players { get; set; } = new();
	public List<ChatMessage> ChatMessages { get; set; } = new();
	public List<Bet> Bets { get; set; } = new();
}

public class Bet
{
	public string Id { get; set; } = null!;
	public string PlayerId { get; set; } = null!;
	public string TableId { get; set; } = null!;

	public int ChosenNumber { get; set; }
	public decimal Amount { get; set; }
	public DateTime PlacedAt { get; set; }

	public int? WinningNumber { get; set; }
	public decimal Payout { get; set; }
	public bool IsResolved { get; set; }
	public DateTime ResolvedAt { get; set; }

	public Player Player { get; set; } = null!;
	public Table Table { get; set; } = null!;
}

public class ChatMessage
{
	public string Id { get; set; } = null!;
	public string TableId { get; set; } = null!;
	public string PlayerId { get; set; } = null!;

	public string Message { get; set; } = null!;
	public DateTime SentAt { get; set; }

	public Player Player { get; set; } = null!;
	public Table Table { get; set; } = null!;
}