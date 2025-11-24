using Microsoft.AspNetCore.Mvc;
using IdentificacionPagos.Services;
using IdentificacionPagos.DTOs;

namespace IdentificacionPagos.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SolicitudController : ControllerBase
{
    private readonly SolicitudService _solicitudService;
    private readonly ReporteExcelService _reporteExcelService;

    public SolicitudController(SolicitudService solicitudService, ReporteExcelService reporteExcelService)
    {
        _solicitudService = solicitudService;
        _reporteExcelService = reporteExcelService;
    }

    [HttpGet("cuenta-predial")]
    public async Task<ActionResult<List<SolicitudConceptoDto>>> ObtenerSolicitudesConCuentaPredial(
        [FromQuery] DateTime? fechaInicial = null,
        [FromQuery] DateTime? fechaFinal = null)
    {
        try
        {
            var resultado = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync(fechaInicial, fechaFinal);
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

    [HttpGet("todos-campos")]
    public async Task<ActionResult> ObtenerTodosCampos()
    {
        try
        {
            var campos = await _solicitudService.ObtenerTodosCamposAsync();
            return Ok(campos);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al obtener los campos", error = ex.Message });
        }
    }

    [HttpGet("reporte-excel")]
    public async Task<ActionResult> GenerarReporteExcel(
        [FromQuery] DateTime? fechaInicial = null,
        [FromQuery] DateTime? fechaFinal = null)
    {
        try
        {
            var datos = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync(fechaInicial, fechaFinal);
            var excelBytes = _reporteExcelService.GenerarReportePagos(datos, fechaInicial, fechaFinal);

            var fileName = $"Reporte_Pagos_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensaje = "Error al generar el reporte", error = ex.Message });
        }
    }
}
