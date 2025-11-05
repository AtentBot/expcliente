using Data;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Services;

public class CreditService(AppDbContext db)
{
    public async Task<CreditAccount> EnsureAccountAsync(Guid establishmentId, CancellationToken ct = default)
    {
        var acc = await db.CreditAccounts.FirstOrDefaultAsync(a => a.EstablishmentId == establishmentId, ct);
        if (acc is null)
        {
            acc = new CreditAccount { EstablishmentId = establishmentId };
            db.CreditAccounts.Add(acc);
            await db.SaveChangesAsync(ct);
        }
        return acc;
    }

    public async Task<CreditAccount> AddCreditAsync(Guid establishmentId, decimal amountBrl, string source, string? description = null, string? paymentIntentId = null, CancellationToken ct = default)
    {
        var acc = await EnsureAccountAsync(establishmentId, ct);

        db.CreditLedgers.Add(new CreditLedger
        {
            EstablishmentId = establishmentId,
            Source = source,
            AmountBrl = amountBrl,
            Description = description,
            StripePaymentIntentId = paymentIntentId
        });

        acc.BalanceBrl += amountBrl;
        acc.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return acc;
    }

    public Task<decimal> GetBalanceAsync(Guid establishmentId, CancellationToken ct = default)
        => db.CreditAccounts.Where(a => a.EstablishmentId == establishmentId)
                            .Select(a => a.BalanceBrl)
                            .FirstOrDefaultAsync(ct);
}
