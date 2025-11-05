using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Models;

[Index(nameof(Code), IsUnique = true)]
public class AccessLevel
{
    [Key]
    public Guid Id { get; set; }

    // Código curto e estável para usar em regras: "user", "adm"
    [Required, MaxLength(20)]
    public string Code { get; set; } = default!;

    // Nome legível (ex.: "Usuário", "Administrador")
    [Required, MaxLength(60)]
    public string Name { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
