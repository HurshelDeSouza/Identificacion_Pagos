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

    [HttpGet("previsualizar-pagos")]
    public async Task<ActionResult> PrevisualizarPagos()
    {
        try
        {
            var resultado = await _sincronizacionService.PrevisualizarPagosAsync();
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al previsualizar pagos", error = ex.Message, stackTrace = ex.StackTrace });
        }
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

    [HttpGet("verificar-bases-datos")]
    public async Task<ActionResult> VerificarBasesDatos()
    {
        try
        {
            var resultado = await _sincronizacionService.VerificarBasesDatosAsync();
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al verificar bases de datos", error = ex.Message });
        }
    }

    [HttpGet("listar-bases-datos")]
    public async Task<ActionResult> ListarBasesDatos()
    {
        try
        {
            var resultado = await _sincronizacionService.ListarBasesDatosAsync();
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al listar bases de datos", error = ex.Message });
        }
    }
}
