using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using shared;

public class Ledger(PayDbContext db)
{
	public async Task<string?> ExecuteTransfer(TxRequest request)
	{
		if (request.Legs.Sum(l => l.Amount) != 0)
			return "NON_ZERO_SUM";

		if (request.Legs.Count < 2)
			return "INVALID_LEGS";

		using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync();

		try
		{
			JournalEntry journal = new()
			{
				Id = Guid.NewGuid().ToString(),
				IdempotencyKey = request.IdempotencyKey,
				Reason = request.Reason,
				CreatedAt = DateTime.UtcNow
			};
			db.JournalEntries.Add(journal);

			foreach (TxLeg leg in request.Legs)
			{
				Account account = await GetOrCreateAccount(leg.AccountId);

				account.Balance += leg.Amount;

				if (account.Balance < 0 && !account.IsSys())
					return $"INSUFFICIENT_FUNDS_{account.Id}";

				db.LedgerLines.Add(new LedgerLine
				{
					Id = Guid.NewGuid().ToString(),
					JournalEntryId = journal.Id,
					AccountId = account.Id,
					Amount = leg.Amount
				});
			}

			await db.SaveChangesAsync();
			await transaction.CommitAsync();

			return null;
		}
		catch (DbUpdateConcurrencyException)
		{
			await transaction.RollbackAsync();
			return "CONCURRENCY_CONFLICT";
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
		{
			await transaction.RollbackAsync();
			return "IDEMPOTENT_REPLAY";
		}
		catch (Exception ex)
		{
			await transaction.RollbackAsync();
			return $"FATAL_ERROR: {ex}";
		}
	}

	public async Task<decimal> GetBalance(string accountId)
	{
		Account? account = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId);
		return account?.Balance ?? 0;
	}

	async Task<Account> GetOrCreateAccount(string accountId)
	{
		Account? account = await db.Accounts.FindAsync(accountId);
		if (account == null)
		{
			account = new Account { Id = accountId, Balance = 0 };
			db.Accounts.Add(account);
		}
		return account;
	}
}