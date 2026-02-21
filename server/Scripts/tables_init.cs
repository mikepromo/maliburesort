partial class tables
{
	public static async Task InitTables(WebApplication app)
	{
		using (IServiceScope scope = app.Services.CreateScope())
		{
			MainDbContext db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
			if (!db.Tables.Any())
			{
				List<Table> tables = new()
				{
					ArrangeTable("Silver Shells", TableTier.Tier1, 5),
					ArrangeTable("Playful Breeze", TableTier.Tier1, 10),
					ArrangeTable("Sunlight Lounge", TableTier.Tier2, 15),
					ArrangeTable("Golden Sands", TableTier.Tier2, 20),
					ArrangeTable("Secret Rendezvous", TableTier.Tier3, 25),
					ArrangeTable("Dolphin's Breath", TableTier.Tier3, 30),
					ArrangeTable("Eternal Bliss", TableTier.Tier4, 35)
				};
				db.Tables.AddRange(tables);
				await db.SaveChangesAsync();
			}
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