using Microsoft.AspNetCore.Mvc;
using ERP.CONTEXTPV;
using Microsoft.EntityFrameworkCore;

namespace IdentificacionPagos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly DbErpPuntoVentaContext _context;

    public DebugController(DbErpPuntoVentaContext context)
    {
        _context = context;
    }

    [HttpGet("solicitud-sample")]
    public async Task<ActionResult> GetSolicitudSample()
    {
        var solicitud = await _context.Solicitud.FirstOrDefaultAsync();
        if (solicitud == null)
            return NotFound(new { mensaje = "No se encontraron solicitudes" });

        var properties = solicitud.GetType().GetProperties()
            .Select(p => new { 
                Nombre = p.Name, 
                Valor = p.GetValue(solicitud)?.ToString() ?? "null",
                Tipo = p.PropertyType.Name
            })
            .ToList();

        return Ok(new { 
            mensaje = "Propiedades de la entidad Solicitud",
            propiedades = properties 
        });
    }

    [HttpGet("concepto-solicitud-sample")]
    public async Task<ActionResult> GetConceptoSolicitudSample()
    {
        var concepto = await _context.ConceptoSolicitud.FirstOrDefaultAsync();
        if (concepto == null)
            return NotFound(new { mensaje = "No se encontraron conceptos de solicitud" });

        var properties = concepto.GetType().GetProperties()
            .Select(p => new { 
                Nombre = p.Name, 
                Valor = p.GetValue(concepto)?.ToString() ?? "null",
                Tipo = p.PropertyType.Name
            })
            .ToList();

        return Ok(new { 
            mensaje = "Propiedades de la entidad ConceptoSolicitud",
            propiedades = properties 
        });
    }

    [HttpGet("concepto-sample")]
    public async Task<ActionResult> GetConceptoSample()
    {
        var concepto = await _context.Concepto.FirstOrDefaultAsync();
        if (concepto == null)
            return NotFound(new { mensaje = "No se encontraron conceptos" });

        var properties = concepto.GetType().GetProperties()
            .Select(p => new { 
                Nombre = p.Name, 
                Valor = p.GetValue(concepto)?.ToString() ?? "null",
                Tipo = p.PropertyType.Name
            })
            .ToList();

        return Ok(new { 
            mensaje = "Propiedades de la entidad Concepto",
            propiedades = properties 
        });
    }
}
