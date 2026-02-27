using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public class PayDbContext(DbContextOptions<PayDbContext> options) : DbContext(options)
{
	public DbSet<Account> Accounts => Set<Account>();
	public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
	public DbSet<LedgerLine> LedgerLines => Set<LedgerLine>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Account>()
			.Property(a => a.Version)
			.IsRowVersion();

		modelBuilder.Entity<JournalEntry>()
			.HasIndex(j => j.IdempotencyKey)
			.IsUnique();

		foreach (IMutableProperty property in modelBuilder.Model.GetEntityTypes()
			         .SelectMany(t => t.GetProperties())
			         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
		{
			property.SetColumnType("decimal(18,2)");
		}
	}
}

public class Account
{
	public string Id { get; set; } = null!;
	public decimal Balance { get; set; }
	public uint Version { get; set; }

	public bool IsSys()
	{
		return Id.StartsWith(LedgerAccounts.SYS_PREFIX);
	}
}

public class JournalEntry
{
	public string Id { get; set; } = null!;
	//; e.g. "bet_table1_spin42_player99"
	public string IdempotencyKey { get; set; } = null!;
	public string Reason { get; set; } = null!;
	public DateTime CreatedAt { get; set; }

	public List<LedgerLine> Lines { get; set; } = new();
}

public class LedgerLine
{
	public string Id { get; set; } = null!;
	public string JournalEntryId { get; set; } = null!;
	public string AccountId { get; set; } = null!;

	public decimal Amount { get; set; }

	public JournalEntry JournalEntry { get; set; } = null!;
	public Account Account { get; set; } = null!;
}