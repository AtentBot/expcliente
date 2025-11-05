using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models;

[Table("credit_ledger")]
public class CreditLedger
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("establishment_id")]
    public Guid EstablishmentId { get; set; }

    // "courtesy" | "stripe"
    [Column("source")]
    [MaxLength(20)]
    public string Source { get; set; } = "courtesy";

    [Column("amount_brl")]
    public decimal AmountBrl { get; set; } // positivo = crédito

    [Column("description")]
    [MaxLength(200)]
    public string? Description { get; set; }

    [Column("stripe_payment_intent_id")]
    [MaxLength(120)]
    public string? StripePaymentIntentId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
