using System.Security.Cryptography;
using System.Text;
using Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;


namespace Service
{
    public sealed class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IOptionsMonitor<ApiKey.Options> _optionsMonitor;

        public ApiKeyMiddleware(RequestDelegate next, IOptionsMonitor<ApiKey.Options> optionsMonitor)
        {
            _next = next;
            _optionsMonitor = optionsMonitor;
        }

        public async Task Invoke(HttpContext ctx)
        {
            // --- WHITELIST (não exige X-API-KEY) ---
            if (ctx.Request.Method == HttpMethods.Options ||
                (ctx.Request.Method == HttpMethods.Post && ctx.Request.Path.StartsWithSegments("/api/stripe/webhook")) ||
                (ctx.Request.Method == HttpMethods.Get && ctx.Request.Path.StartsWithSegments("/api/stripe/success")) ||
                (ctx.Request.Method == HttpMethods.Get && ctx.Request.Path.StartsWithSegments("/api/stripe/cancel")) ||
                ctx.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(ctx);
                return;
            }

            var opts = _optionsMonitor.CurrentValue;

            // Cabeçalho ausente
            if (!ctx.Request.Headers.TryGetValue(opts.HeaderName, out var providedKey) ||
                StringValues.IsNullOrEmpty(providedKey))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "missing_api_key" });
                return;
            }

            // (Opcional) exigir ID da chave num header separado
            string? requiredId = null;
            if (opts.RequireIdHeader)
            {
                if (!ctx.Request.Headers.TryGetValue(opts.IdHeaderName, out var idVal) ||
                    StringValues.IsNullOrEmpty(idVal))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await ctx.Response.WriteAsJsonAsync(new { error = "missing_api_key_id" });
                    return;
                }
                requiredId = idVal.ToString();
            }

            var now = DateTimeOffset.UtcNow;
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey.ToString()));

            bool ok = false;
            foreach (var rec in opts.Keys)
            {
                if (!rec.Active) continue;
                if (rec.NotBefore.HasValue && now < rec.NotBefore.Value) continue;
                if (rec.NotAfter.HasValue && now > rec.NotAfter.Value) continue;
                if (requiredId is not null && !string.Equals(rec.Id, requiredId, StringComparison.Ordinal))
                    continue;

                if (TryGetBytes(rec.HashBase64, out var storedHash) &&
                    storedHash.Length == providedHash.Length &&
                    CryptographicOperations.FixedTimeEquals(storedHash, providedHash))
                {
                    ok = true;
                    break;
                }
            }

            if (!ok)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "invalid_api_key" });
                return;
            }

            await _next(ctx);
        }

        private static bool TryGetBytes(string b64, out byte[] bytes)
        {
            try { bytes = Convert.FromBase64String(b64); return true; }
            catch { bytes = Array.Empty<byte>(); return false; }
        }

        public static string NewApiKey(int bytes = 32) // 256 bits
        {
            var b = RandomNumberGenerator.GetBytes(bytes);
            return Convert.ToBase64String(b); // chave para fornecer ao cliente
        }

        public static string HashApiKeyToBase64(string apiKey)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToBase64String(hash);
        }

    }
}
