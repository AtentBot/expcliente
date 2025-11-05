using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using Stripe;
using Services;
using Microsoft.EntityFrameworkCore;
using Data;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Stripe.V2.Core;


// aliases para evitar conflito com Stripe.V2
using StripeEvent = global::Stripe.Event;
using StripeSession = global::Stripe.Checkout.Session;
using StripeEvents = global::Stripe.Events;

[ApiController]
[Route("api/stripe")]
public class StripeController : ControllerBase
{
    private readonly CreditService _credits;
    private readonly IConfiguration _cfg;
    private readonly AppDbContext _db;
    private readonly ILogger<StripeController> _logger;

    public StripeController(CreditService credits, IConfiguration cfg, AppDbContext db, ILogger<StripeController> logger)
    {
        _credits = credits;
        _cfg = cfg;
        _db = db;
        _logger = logger;
    }

    public record CheckoutRequest(Guid EstablishmentId, decimal Amount);

    // 1) Criação de sessão de checkout
    [HttpPost("create-checkout-session")]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest req, CancellationToken ct)
    {
        if (req.Amount < 50)
            return BadRequest(new { error = "invalid_minimum_amount", message = "O valor mínimo para compra de créditos é R$ 50,00." });

        var est = await _db.Establishments.FirstOrDefaultAsync(e => e.Id == req.EstablishmentId, ct);
        if (est is null) return NotFound(new { error = "establishment_not_found" });

        var origin = $"{Request.Scheme}://{Request.Host}";
        var amountInCents = (long)(req.Amount * 100m);

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            SuccessUrl = $"{origin}/api/stripe/success?sid={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{origin}/api/stripe/cancel",
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "brl",
                        UnitAmount = amountInCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Créditos ExpCliente - {est.NomeFantasia}",
                            Description = $"Compra de créditos para {est.NomeFantasia}"
                        }
                    },
                    Quantity = 1
                }
            },
            CustomerEmail = est.Email,
            Metadata = new Dictionary<string, string>
            {
                ["establishmentId"] = est.Id.ToString(),
                ["amount"] = req.Amount.ToString("F2")
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);
        return Ok(new { checkoutUrl = session.Url });
    }

    // 2) Webhook Stripe → crédito automático
    [HttpPost("webhook")]
    [AllowAnonymous] // já estamos ignorando API Key nesse endpoint
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        var secret = _cfg["StripeSettings:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "stripe_webhook_secret_missing" });

        // 1) Lê o corpo cru
        string json;
        using (var reader = new StreamReader(Request.Body))
            json = await reader.ReadToEndAsync();

        // 2) Valida assinatura mas NÃO quebra por versão diferente
        Stripe.Event stripeEvent;
        try
        {
            var sigHeader = Request.Headers["Stripe-Signature"].ToString();
            // tolerância padrão (300s); a flag abaixo evita a exceção de versão
            stripeEvent = Stripe.EventUtility.ConstructEvent(
                json, sigHeader, secret, tolerance: 300, throwOnApiVersionMismatch: false
            );

            // Log informativo caso a versão do evento seja diferente
            if (!string.IsNullOrEmpty(stripeEvent.ApiVersion) &&
                !stripeEvent.ApiVersion.Contains("2025"))
            {
                _logger.LogWarning("Stripe webhook com API antiga: {ApiVersion}", stripeEvent.ApiVersion);
            }
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook invalid signature/version");
            return Unauthorized(new { error = "invalid_signature" });
        }

        // 3) Processa pagamento concluído
        if (stripeEvent.Type == "checkout.session.completed")
        {
            var evtSession = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (evtSession is null)
                return Ok(new { received = true });

            // Sempre buscamos a sessão COMPLETA na API (evita problemas de versão)
            var sessionService = new Stripe.Checkout.SessionService();
            var full = await sessionService.GetAsync(
                evtSession.Id,
                new Stripe.Checkout.SessionGetOptions { Expand = new List<string> { "payment_intent" } },
                requestOptions: null,
                cancellationToken: ct
            );

            var amountTotal = (full.AmountTotal ?? 0) / 100m;
            full.Metadata.TryGetValue("establishmentId", out var estIdStr);

            if (Guid.TryParse(estIdStr, out var establishmentId) && amountTotal > 0m)
            {
                await _credits.AddCreditAsync(
                    establishmentId,
                    amountTotal,
                    "stripe",
                    $"Stripe checkout {full.Id} (PI:{full.PaymentIntentId})",
                    full.PaymentIntentId // ← último argumento é string? (externalRef)
                );

                _logger.LogInformation("Crédito lançado: Estab={Est}, Valor={Val}, Intent={Intent}",
                    establishmentId, amountTotal, full.PaymentIntentId);
            }
            else
            {
                _logger.LogWarning("Webhook incompleto: estId={EstId}, amount={Amt}", estIdStr, amountTotal);
            }
        }

        return Ok(new { received = true });
    }


    // 3) Retornos JSON pós-pagamento
    [HttpGet("success")]
    [AllowAnonymous]
    public IActionResult Success([FromQuery] string sid)
        => Ok(new { status = "success", message = "Pagamento concluído!", sessionId = sid });

    [HttpGet("cancel")]
    [AllowAnonymous]
    public IActionResult Cancel()
        => Ok(new { status = "canceled", message = "Pagamento cancelado!" });
}
