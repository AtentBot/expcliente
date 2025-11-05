using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data;
using Models;
using Isopoh.Cryptography.Argon2;
using System.Security.Cryptography;
using System.Net.Http;
using System.Net.Http.Json;

namespace Controllers;

[ApiController]
[Route("api/establishments")]
public class EstablishmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public EstablishmentsController(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Establishment>>> List(
        [FromQuery] string? city = null,
        [FromQuery] string? state = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var q = _db.Establishments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(e => e.City == city);
        if (!string.IsNullOrWhiteSpace(state)) q = q.Where(e => e.State == state);

        var data = await q.OrderBy(e => e.NomeFantasia)
                          .Skip(Math.Max(0, skip))
                          .Take(Math.Clamp(take, 1, 200))
                          .ToListAsync();
        return data;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Establishment>> Get(Guid id)
        => await _db.Establishments.FindAsync(id) is { } e ? Ok(e) : NotFound();

    // 1) CADASTRO: cria establishment, gera 6 dígitos, salva client_onboarding e envia WhatsApp
    [HttpPost]
    public async Task<ActionResult<Establishment>> Create([FromBody] Establishment input, CancellationToken ct)
    {
        // ✅ 1. Verifica se já existe CNPJ igual
        if (!string.IsNullOrWhiteSpace(input.Cnpj))
        {
            var exists = await _db.Establishments
                .AnyAsync(e => e.Cnpj == input.Cnpj.Trim(), ct);

            if (exists)
                return Conflict(new
                {
                    error = "duplicate_cnpj",
                    message = "Já temos um cliente cadastrado utilizando este CNPJ. Verifique seus dados ou entre em contato com o suporte."
                });
        }

        // ✅ 2. Metadados
        input.Id = Guid.Empty;
        input.CreatedAt = DateTime.UtcNow;
        input.UpdatedAt = input.CreatedAt;
        input.PasswordCreatedAt = DateTime.UtcNow;
        input.PasswordLastRehash = null;

        // ✅ 3. Senha em texto puro → gera hash antes de usar o EF
        if (!string.IsNullOrWhiteSpace(input.PasswordHash) && !input.PasswordHash.StartsWith("$argon2id$"))
        {
            var plain = input.PasswordHash;
            input.PasswordHash = Argon2.Hash(plain);
            input.PasswordAlgorithm = "argon2id-v1";
        }

        // ✅ 4. Novos cadastros
        input.OnboardingCompleted = false;
        input.IsActive = true;

        // ✅ 5. Persistência
        _db.Establishments.Add(input);
        await _db.SaveChangesAsync(ct);

        // Gera 6 dígitos (100000..999999)
        var six = RandomNumberGenerator.GetInt32(100000, 1000000);

        var cliente = await _db.Establishments
            .FirstOrDefaultAsync(x => x.Cnpj == input.Cnpj, ct);

        // Salva em client_onboarding
        var co = new ClientOnboarding
        {
            Id = Guid.NewGuid(),
            EstablishmentId = cliente.Id,
            WhatsApp = cliente.WhatsApp ?? string.Empty,
            Numero = six,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            OnboardingCompleted = false
        };
        _db.ClientOnboardings.Add(co);
        await _db.SaveChangesAsync(ct);

        // Envia WhatsApp via AtentBot
        var msg = $"Olá equipe do {input.NomeFantasia}, estamos felizes com sua chegada, " +
                  $"seu numero de confirmação é {six}.";

        await SendWhatsAppAsync(co.WhatsApp, msg, ct);

        return CreatedAtAction(nameof(Get), new { id = input.Id }, input);
    }

    // 2) CONFIRMAÇÃO: recebe código de 6 dígitos e finaliza onboarding
    public record ConfirmRequest(int Numero);

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] ConfirmRequest req, CancellationToken ct)
    {
        if (req.Numero < 100000 || req.Numero > 999999)
            return BadRequest(new { error = "invalid_code_format" });

        // Busca o registro mais recente com esse número
        var co = await _db.ClientOnboardings
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.Numero == req.Numero, ct);

        if (co is null)
            return NotFound(new { error = "code_not_found" });

        // Atualiza client_onboarding
        co.OnboardingCompleted = true;
        co.UpdatedAt = DateTime.UtcNow;

        // Atualiza establishment correspondente
        var est = await _db.Establishments.FirstOrDefaultAsync(e => e.Id == co.EstablishmentId, ct);
        if (est is null)
            return NotFound(new { error = "establishment_not_found" });

        est.OnboardingCompleted = true;
        est.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(new { status = "confirmed", establishmentId = est.Id });
    }

    // helper: envia mensagem WhatsApp direto ao provedor
    private async Task<bool> SendWhatsAppAsync(string number, string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(number)) return false;

        var apiKey = _config["AtentBot:ApiKey"];
        var baseUrl = _config["AtentBot:BaseUrl"] ?? "https://api.atentbot.com";
        if (string.IsNullOrWhiteSpace(apiKey)) return false;

        var client = _httpClientFactory.CreateClient();
        var url = $"{baseUrl.TrimEnd('/')}/message/sendText/crescer";
        var payload = new { number, text };

        using var http = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        http.Headers.Add("apikey", apiKey);

        var resp = await client.SendAsync(http, ct);
        return resp.IsSuccessStatusCode;
    }
}
