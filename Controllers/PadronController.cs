using Microsoft.AspNetCore.Mvc;
using IdentificacionPagos.Services;

namespace IdentificacionPagos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PadronController : ControllerBase
{
    private readonly ActualizacionPadronService _padronService;

    public PadronController(ActualizacionPadronService padronService)
    {
        _padronService = padronService;
    }

    [HttpPost("actualizar-padron")]
    [HttpGet("actualizar-padron")]
    public async Task<ActionResult> ActualizarPadron()
    {
        try
        {
            var resultado = await _padronService.ActualizarPadronAsync();
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new 
            { 
                mensaje = "Error al actualizar padrón", 
                error = ex.Message,
                stackTrace = ex.StackTrace
            });
        }
    }
}
