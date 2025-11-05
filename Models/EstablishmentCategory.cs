
using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Models;

[Index(nameof(Name), IsUnique = true)]
public class EstablishmentCategory
{
    // UUID como PK (Guid mapeado para uuid)
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = default!;

    [Required, MaxLength(140)]
    public string Slug { get; set; } = default!; // opcional: útil para URLs/SEO

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
