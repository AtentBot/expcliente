using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services;
using Data;

[ApiController]
[Route("api/credits")]
public class CreditsController(CreditService creditService, AppDbContext db) : ControllerBase
{
    bool IsAdmin() => HttpContext.Items["AccessLevel"]?.ToString() == "adm";
    Guid? SessionEstablishmentId =>
        Guid.TryParse(HttpContext.Items["EstablishmentId"]?.ToString(), out var id) ? id : null;

    public record CourtesyRequest(Guid EstablishmentId, decimal? Amount);

    // ADM concede crédito de cortesia
    [HttpPost("courtesy")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Courtesy([FromBody] CourtesyRequest req, CancellationToken ct)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "access_denied",
                message = "Seu nível de acesso é insuficiente para realizar esta operação. Somente administradores podem conceder créditos de cortesia."
            });

        var exists = await db.Establishments.AnyAsync(e => e.Id == req.EstablishmentId, ct);
        if (!exists) return NotFound(new { error = "establishment_not_found" });

        var amount = req.Amount is null or <= 0 ? 10m : decimal.Round(req.Amount.Value, 2);
        var acc = await creditService.AddCreditAsync(
            req.EstablishmentId,
            amount,
            "courtesy",
            "Crédito de cortesia",
            ct: ct
        );

        return Ok(new { status = "credited", amountBrl = amount, balanceBrl = acc.BalanceBrl });
    }

    // Consulta de saldo
    [HttpGet("{establishmentId:guid}/balance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Balance(Guid establishmentId, CancellationToken ct)
    {
        // Não-adm só pode consultar o próprio saldo
        if (!IsAdmin() && SessionEstablishmentId is Guid sid && sid != establishmentId)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "access_denied",
                message = "Você não tem permissão para consultar o saldo de outro estabelecimento."
            });

        var bal = await creditService.GetBalanceAsync(establishmentId, ct);
        return Ok(new { balanceBrl = bal });
    }
}

