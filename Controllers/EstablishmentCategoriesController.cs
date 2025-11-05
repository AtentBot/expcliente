using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Data;
using Models;

namespace Controllers;

[ApiController]
[Route("api/categories")]
public class EstablishmentCategoriesController(AppDbContext db) : ControllerBase
{
    // 🔒 Função auxiliar para verificar nível de acesso
    private bool IsAdmin()
        => HttpContext.Items["AccessLevel"]?.ToString() == "adm";


    [HttpGet]
    public async Task<ActionResult<IEnumerable<EstablishmentCategory>>> List([FromQuery] int skip = 0, [FromQuery] int take = 50)
        => await db.EstablishmentCategories
                   .OrderBy(c => c.Name)
                   .Skip(Math.Max(0, skip))
                   .Take(Math.Clamp(take, 1, 200))
                   .ToListAsync();

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EstablishmentCategory>> Get(Guid id)
        => await db.EstablishmentCategories.FindAsync(id) is { } cat ? Ok(cat) : NotFound();

    // 🔒 Somente administradores podem criar
    [HttpPost]
    public async Task<ActionResult<EstablishmentCategory>> Create([FromBody] EstablishmentCategory input)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "access_denied",
                message = "Seu nível de acesso é insuficiente para realizar esta operação. Somente administradores podem conceder créditos de cortesia."
            });


        input.Id = Guid.Empty; // deixa o DB gerar (uuid default)
        input.CreatedAt = DateTime.UtcNow;
        input.UpdatedAt = input.CreatedAt;

        db.EstablishmentCategories.Add(input);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = input.Id }, input);
    }

    // 🔒 Somente administradores podem atualizar
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] EstablishmentCategory input)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "access_denied",
                message = "Seu nível de acesso é insuficiente para realizar esta operação. Somente administradores podem conceder créditos de cortesia."
            });


        var existing = await db.EstablishmentCategories.FindAsync(id);
        if (existing is null) return NotFound();

        existing.Name = input.Name;
        existing.Slug = input.Slug;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // 🔒 Somente administradores podem excluir
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "access_denied",
                message = "Seu nível de acesso é insuficiente para realizar esta operação. Somente administradores podem conceder créditos de cortesia."
            });


        var existing = await db.EstablishmentCategories.FindAsync(id);
        if (existing is null) return NotFound();

        db.EstablishmentCategories.Remove(existing);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // 🔒 Somente administradores podem inserir em massa
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkInsert([FromBody] List<EstablishmentCategory> categories)
    {
        if (!IsAdmin())
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "access_denied",
                message = "Seu nível de acesso é insuficiente para realizar esta operação. Somente administradores podem conceder créditos de cortesia."
            });


        if (categories == null || categories.Count == 0)
            return BadRequest(new { error = "Empty category list" });

        foreach (var c in categories)
        {
            c.Id = Guid.Empty;
            c.CreatedAt = DateTime.UtcNow;
            c.UpdatedAt = c.CreatedAt;
        }

        await db.EstablishmentCategories.AddRangeAsync(categories);
        await db.SaveChangesAsync();

        return Ok(new { inserted = categories.Count });
    }
}
