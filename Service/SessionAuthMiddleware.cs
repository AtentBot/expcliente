using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Service
{
    public class SessionAuthMiddleware
    {
        private readonly RequestDelegate _next;
        public SessionAuthMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext ctx, AppDbContext db)
        {
            // --- WHITELIST (sem exigir X-SESSION-TOKEN) ---
            if (ctx.Request.Method == HttpMethods.Options ||
                (ctx.Request.Method == HttpMethods.Post && ctx.Request.Path.StartsWithSegments("/api/login")) ||
                (ctx.Request.Method == HttpMethods.Post && ctx.Request.Path.StartsWithSegments("/api/categories/bulk")) ||
                (ctx.Request.Method == HttpMethods.Post && ctx.Request.Path.StartsWithSegments("/api/wa")) ||
                (ctx.Request.Method == HttpMethods.Post && ctx.Request.Path.StartsWithSegments("/api/establishments/confirm")) ||
                (ctx.Request.Method == HttpMethods.Post && ctx.Request.Path.StartsWithSegments("/api/establishments")) ||
                // Stripe webhook (POST)
                (ctx.Request.Method == HttpMethods.Post && ctx.Request.Path.StartsWithSegments("/api/stripe/webhook")) ||
                // Stripe success/cancel (GET)
                (ctx.Request.Method == HttpMethods.Get && ctx.Request.Path.StartsWithSegments("/api/stripe/success")) ||
                (ctx.Request.Method == HttpMethods.Get && ctx.Request.Path.StartsWithSegments("/api/stripe/cancel")))
            {
                await _next(ctx);
                return;
            }


            // --- A PARTIR DAQUI, sessão é obrigatória ---
            if (!ctx.Request.Headers.TryGetValue("X-SESSION-TOKEN", out var token) || StringValues.IsNullOrEmpty(token))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "missing_session_token" });
                return;
            }


            var session = await db.Sessions
                .Include(s => s.Establishment)
                .FirstOrDefaultAsync(s => s.Token == token.ToString() && s.ExpiresAt > DateTime.UtcNow);

            if (session is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    error = "session_expired",
                    message = "Sua sessão expirou ou é inválida. Por favor, faça login novamente para continuar usando a plataforma."
                });
                return;
            }

            // Regras extras (opcional): bloquear se a conta desativou após o login
            if (session.Establishment is { IsActive: false })
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "account_inactive" });
                return;
            }

            ctx.Items["AccessLevel"] = session.AccessLevel;
            ctx.Items["EstablishmentId"] = session.EstablishmentId;

            await _next(ctx);
        }
    }


}
