using Microsoft.AspNetCore.Mvc;
using IdentificacionPagos.Services;
using IdentificacionPagos.DTOs;

namespace IdentificacionPagos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SolicitudController : ControllerBase
{
    private readonly SolicitudService _solicitudService;

    public SolicitudController(SolicitudService solicitudService)
    {
        _solicitudService = solicitudService;
    }

    [HttpGet("cuenta-predial")]
    public async Task<ActionResult<List<SolicitudConceptoDto>>> ObtenerSolicitudesConCuentaPredial()
    {
        try
        {
            var resultado = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync();
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al obtener las solicitudes", error = ex.Message });
        }
    }

    [HttpGet("campos-formulario/{formularioId}")]
    public async Task<ActionResult> ObtenerCamposFormulario(int formularioId)
    {
        try
        {
            var campos = await _solicitudService.ObtenerCamposFormularioAsync(formularioId);
            return Ok(campos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al obtener los campos", error = ex.Message });
        }
    }

    [HttpGet("respuestas-formulario/{formularioId}")]
    public async Task<ActionResult> ObtenerRespuestasFormulario(int formularioId)
    {
        try
        {
            var respuestas = await _solicitudService.ObtenerRespuestasConValorAsync(formularioId);
            return Ok(respuestas);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al obtener las respuestas", error = ex.Message });
        }
    }
}
