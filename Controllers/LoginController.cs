using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Isopoh.Cryptography.Argon2;
using Data;
using Models;
using System.Security.Cryptography;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class LoginController : ControllerBase
{
    private readonly AppDbContext _context;

    public LoginController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WhatsApp) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "missing_credentials" });

        var user = await _context.Establishments
            .Include(e => e.AccessLevel)
            .FirstOrDefaultAsync(e => e.WhatsApp == request.WhatsApp);

        if (user is null)
            return Unauthorized(new { error = "user_not_found" });

        // 🔒 NOVO: valida se o usuário está ativo e completou o onboarding
        if (!user.IsActive)
            return Unauthorized(new { error = "account_inactive" });

        if (!user.OnboardingCompleted)
            return Unauthorized(new { error = "onboarding_incomplete" });

        // Verifica hash (argon2id ou pbkdf2)
        bool valid = VerifyPassword(request.Password, user.PasswordHash, user.PasswordAlgorithm);
        if (!valid)
            return Unauthorized(new { error = "invalid_password" });

        // Gera token de sessão simples
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var session = new UserSession
        {
            Token = token,
            EstablishmentId = user.Id,
            AccessLevel = user.AccessLevel!.Code,
            ExpiresAt = DateTime.UtcNow.AddHours(12)
        };

        _context.Add(session);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            session.Token,
            session.ExpiresAt,
            userId = user.Id,
            user.AccessLevel!.Code,
            user.NomeFantasia
        });
    }

    private static bool VerifyPassword(string password, string storedHash, string algorithm)
    {
        if (algorithm.StartsWith("argon2id"))
        {
            return Argon2.Verify(storedHash, password); // usa pacote Isopoh.Cryptography.Argon2
        }
        else if (algorithm.StartsWith("pbkdf2"))
        {
            var parts = storedHash.Split(':');
            var salt = Convert.FromBase64String(parts[1]);
            var hash = Convert.FromBase64String(parts[2]);

            using var derive = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
            return CryptographicOperations.FixedTimeEquals(hash, derive.GetBytes(hash.Length));
        }
        return false;
    }
}

public record LoginRequest(string WhatsApp, string Password);

