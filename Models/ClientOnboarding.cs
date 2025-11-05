using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Models
{
    [Table("client_onboarding")]
    public class ClientOnboarding
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column("establishment_id")]
        [Required]
        public Guid EstablishmentId { get; set; }

        [Column("whats_app")]
        [MaxLength(30)]
        [Required]
        public string WhatsApp { get; set; } = string.Empty;

        [Column("numero")]
        [Range(100000, 999999, ErrorMessage = "O número deve conter 6 dígitos.")]
        [Required]
        public int Numero { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("onboarding_completed")]
        public bool OnboardingCompleted { get; set; } = false;
    }
}
