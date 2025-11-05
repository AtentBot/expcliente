using Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class UserSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EstablishmentId { get; set; }

    // 🔹 Adicione esta propriedade:
    [ForeignKey(nameof(EstablishmentId))]
    public Establishment Establishment { get; set; } = default!;

    [Required, MaxLength(64)]
    public string Token { get; set; } = default!;

    [Required, MaxLength(20)]
    public string AccessLevel { get; set; } = default!; // "user" ou "adm"

    public DateTime ExpiresAt { get; set; }
}


