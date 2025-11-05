using Microsoft.EntityFrameworkCore;
using Models;

namespace Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<EstablishmentCategory> EstablishmentCategories => Set<EstablishmentCategory>();
    public DbSet<Establishment> Establishments => Set<Establishment>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<ClientOnboarding> ClientOnboardings { get; set; } = null!;
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLedger> CreditLedgers => Set<CreditLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Decimais como money (2 casas)
        modelBuilder.Entity<CreditAccount>().Property(p => p.BalanceBrl).HasPrecision(12, 2);
        modelBuilder.Entity<CreditLedger>().Property(p => p.AmountBrl).HasPrecision(12, 2);

        // Uma conta por estabelecimento
        modelBuilder.Entity<CreditAccount>()
            .HasIndex(x => x.EstablishmentId)
            .IsUnique();

        // Habilita extensão para UUID v4 (gen_random_uuid) — pgcrypto
        modelBuilder.HasPostgresExtension("pgcrypto");

        // ===== EstablishmentCategory =====
        modelBuilder.Entity<EstablishmentCategory>(e =>
        {
            e.ToTable("establishment_category");

            e.Property(p => p.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()"); // UUID no lado do DB

            e.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(120);

            e.Property(p => p.Slug)
                .IsRequired()
                .HasMaxLength(140);

            e.Property(p => p.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(p => p.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");
        });

        // ===== Establishment =====
        modelBuilder.Entity<Establishment>(e =>
        {
            e.ToTable("establishment");

            e.Property(p => p.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(p => p.CategoryId)
                .HasColumnType("uuid");

            e.HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(p => p.RazaoSocial).IsRequired().HasMaxLength(200);
            e.Property(p => p.NomeFantasia).IsRequired().HasMaxLength(200);
            e.Property(p => p.Cnpj).IsRequired().HasMaxLength(14);

            e.Property(p => p.Street).IsRequired().HasMaxLength(200);
            e.Property(p => p.Number).IsRequired().HasMaxLength(20);
            e.Property(p => p.Complement).HasMaxLength(120);
            e.Property(p => p.Neighborhood).IsRequired().HasMaxLength(120);
            e.Property(p => p.City).IsRequired().HasMaxLength(120);
            e.Property(p => p.State).IsRequired().HasMaxLength(2);
            e.Property(p => p.PostalCode).IsRequired().HasMaxLength(8);
            e.Property(p => p.Country).IsRequired().HasMaxLength(60).HasDefaultValue("Brasil");

            e.Property(p => p.Phone).HasMaxLength(20);
            e.Property(p => p.WhatsApp).HasMaxLength(20);
            e.Property(p => p.Email).HasMaxLength(200);
            e.Property(p => p.Instagram).HasMaxLength(200);
            e.Property(p => p.Facebook).HasMaxLength(200);
            e.Property(p => p.TikTok).HasMaxLength(200);

            // Campos de senha / segurança
            e.Property(p => p.PasswordHash)
                .IsRequired()
                .HasColumnType("text");

            e.Property(p => p.PasswordCreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(p => p.PasswordLastRehash)
                .HasColumnType("timestamp with time zone");

            e.Property(p => p.PasswordAlgorithm)
                .IsRequired()
                .HasMaxLength(40)
                .HasDefaultValue("argon2id-v1");

            // Auditoria
            e.Property(p => p.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            e.Property(p => p.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");
        });

        // ===== AccessLevel =====
        modelBuilder.Entity<AccessLevel>(e =>
        {
            e.ToTable("access_level");

            e.Property(p => p.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("gen_random_uuid()");

            e.Property(p => p.Code).IsRequired().HasMaxLength(20);
            e.Property(p => p.Name).IsRequired().HasMaxLength(60);

            e.Property(p => p.CreatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");
            e.Property(p => p.UpdatedAt)
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");
        });

        // GUIDs fixos para seed (assim podemos referenciar como default na FK)
        var ACCESS_USER = new Guid("11111111-1111-1111-1111-111111111111");
        var ACCESS_ADM = new Guid("22222222-2222-2222-2222-222222222222");

        // Seed (HasData precisa de timestamps fixos)
        modelBuilder.Entity<AccessLevel>().HasData(
            new AccessLevel { Id = ACCESS_USER, Code = "user", Name = "Usuário", CreatedAt = DateTime.UnixEpoch, UpdatedAt = DateTime.UnixEpoch },
            new AccessLevel { Id = ACCESS_ADM, Code = "adm", Name = "Administrador", CreatedAt = DateTime.UnixEpoch, UpdatedAt = DateTime.UnixEpoch }
        );

        // ===== Establishment (com FK de nível) =====
        modelBuilder.Entity<Establishment>(e =>
        {
            // ... (mapeamentos que você já tem)

            e.Property(p => p.AccessLevelId).HasColumnType("uuid")
                .HasDefaultValue(ACCESS_USER); // default = "user"

            e.HasOne(p => p.AccessLevel)
                .WithMany()
                .HasForeignKey(p => p.AccessLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

    }



    // Opcional: atualiza UpdatedAt automaticamente
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is EstablishmentCategory ec)
            {
                if (entry.State == EntityState.Added) ec.CreatedAt = now;
                ec.UpdatedAt = now;
            }
            if (entry.Entity is Establishment es)
            {
                if (entry.State == EntityState.Added) es.CreatedAt = now;
                es.UpdatedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

