using Microsoft.AspNetCore.Mvc;
using IdentificacionPagos.Services;

namespace IdentificacionPagos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SincronizacionController : ControllerBase
{
    private readonly SincronizacionPagosService _sincronizacionService;

    public SincronizacionController(SincronizacionPagosService sincronizacionService)
    {
        _sincronizacionService = sincronizacionService;
    }

    [HttpPost("sincronizar-pagos")]
    public async Task<ActionResult> SincronizarPagos()
    {
        try
        {
            var resultado = await _sincronizacionService.SincronizarPagosAsync();
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al sincronizar pagos", error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}
