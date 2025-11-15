using Microsoft.AspNetCore.Mvc;
using GRP.ContextCatastro;
using Microsoft.EntityFrameworkCore;

namespace IdentificacionPagos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugCatastroController : ControllerBase
{
    private readonly DbErpCatastroContext _context;

    public DebugCatastroController(DbErpCatastroContext context)
    {
        _context = context;
    }

    [HttpGet("context-properties")]
    public ActionResult GetContextProperties()
    {
        var properties = _context.GetType().GetProperties()
            .Where(p => p.PropertyType.IsGenericType && 
                       p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => new { 
                Nombre = p.Name, 
                Tipo = p.PropertyType.GetGenericArguments()[0].Name
            })
            .ToList();

        return Ok(new {
            mensaje = "DbSets disponibles en DbErpCatastroContext",
            dbSets = properties
        });
    }

    [HttpGet("padron-sample")]
    public async Task<ActionResult> GetPadronSample()
    {
        var padron = await _context.Padrones.FirstOrDefaultAsync();
        if (padron == null)
            return NotFound(new { mensaje = "No se encontraron registros en Padrones" });

        var properties = padron.GetType().GetProperties()
            .Select(p => new { 
                Nombre = p.Name, 
                Valor = p.GetValue(padron)?.ToString() ?? "null",
                Tipo = p.PropertyType.Name
            })
            .ToList();

        return Ok(new { 
            mensaje = "Propiedades de la entidad Padron",
            propiedades = properties 
        });
    }

    [HttpGet("adeudo-sample")]
    public async Task<ActionResult> GetAdeudoSample()
    {
        var adeudo = await _context.Adeudos.FirstOrDefaultAsync();
        if (adeudo == null)
            return NotFound(new { mensaje = "No se encontraron registros en Adeudos" });

        var properties = adeudo.GetType().GetProperties()
            .Select(p => new { 
                Nombre = p.Name, 
                Valor = p.GetValue(adeudo)?.ToString() ?? "null",
                Tipo = p.PropertyType.Name
            })
            .ToList();

        return Ok(new { 
            mensaje = "Propiedades de la entidad Adeudo",
            propiedades = properties 
        });
    }
}
