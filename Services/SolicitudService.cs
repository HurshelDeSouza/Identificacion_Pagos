using Microsoft.EntityFrameworkCore;
using IdentificacionPagos.DTOs;
using ERP.CONTEXTPV;

namespace IdentificacionPagos.Services;

public class SolicitudService
{
    private readonly DbErpPuntoVentaContext _context;

    public SolicitudService(DbErpPuntoVentaContext context)
    {
        _context = context;
    }

    public async Task<List<SolicitudConceptoDto>> ObtenerSolicitudesConCuentaPredialAsync()
    {
        // Obtener IDs de solicitudes que tienen respuestas del formulario con id 1 (Cuenta Predial)
        var solicitudIds = await _context.RespuestaCampoFormulario
            .Where(rcf => rcf.Formulario == 1)
            .Select(rcf => rcf.Solicitud)
            .Distinct()
            .ToListAsync();

        // Obtener solicitudes con sus relaciones usando Include
        var solicitudes = await _context.Solicitud
            .Where(s => solicitudIds.Contains(s.Id))
            .ToListAsync();

        // Obtener conceptos de solicitud con la relación de Concepto usando Include
        var conceptosSolicitud = await _context.ConceptoSolicitud
            .Where(cs => solicitudIds.Contains(cs.Solicitud))
            .ToListAsync();

        // Obtener todos los conceptos necesarios
        var conceptoIds = conceptosSolicitud.Select(cs => cs.Concepto).Distinct().ToList();
        var conceptos = await _context.Concepto
            .Where(c => conceptoIds.Contains(c.Id))
            .ToListAsync();

        // Obtener respuestas de TODOS los formularios (no solo formulario 1)
        var respuestasCampos = await _context.RespuestaCampoFormulario
            .Where(rcf => rcf.Solicitud != null && solicitudIds.Contains(rcf.Solicitud.Value))
            .ToListAsync();

        // Obtener todos los campos de formulario necesarios
        var campos = await _context.CampoFormulario
            .ToListAsync();

        // Combinar los datos - crear un DTO por cada conceptoSolicitud
        var resultadoFinal = new List<SolicitudConceptoDto>();

        foreach (var solicitud in solicitudes)
        {
            // Obtener TODOS los conceptos de esta solicitud
            var conceptosSolicitudActual = conceptosSolicitud.Where(cs => cs.Solicitud.Equals(solicitud.Id)).ToList();
            
            // Obtener las respuestas de esta solicitud
            var respuestasSolicitud = respuestasCampos.Where(rcf => rcf.Solicitud == solicitud.Id).ToList();
            
            // Obtener la cuenta predial
            var cuentaPredial = ObtenerValorCampo(respuestasSolicitud, campos, "Clave Catastral");

            // Crear un DTO por cada concepto de la solicitud
            foreach (var conceptoSolicitud in conceptosSolicitudActual)
            {
                var concepto = conceptos.FirstOrDefault(c => c.Id.Equals(conceptoSolicitud.Concepto));

                resultadoFinal.Add(new SolicitudConceptoDto
                {
                    NombreConcepto = concepto?.Nombre ?? string.Empty,
                    FolioRecaudacion = solicitud.FolioRecaudacion ?? string.Empty,
                    FechaPago = solicitud.FechaPago,
                    CuentaPredial = cuentaPredial,
                    AnioInicial = ObtenerValorCampo(respuestasSolicitud, campos, "Año Inicial"),
                    AnioFinal = ObtenerValorCampo(respuestasSolicitud, campos, "Año Final")
                });
            }
        }

        // Filtrar solo los que tienen cuenta predial y ordenar
        return resultadoFinal
            .Where(dto => !string.IsNullOrEmpty(dto.CuentaPredial))
            .OrderBy(x => x.CuentaPredial)
            .ToList();
    }

    private string ObtenerValorCampo(List<ERP.CONTEXTPV.Entities.RespuestaCampoFormulario> respuestas, 
                                     List<ERP.CONTEXTPV.Entities.CampoFormulario> campos, 
                                     string nombreCampo)
    {
        var campo = campos.FirstOrDefault(c => c.Campo == nombreCampo);
        if (campo == null) return string.Empty;

        foreach (var respuesta in respuestas)
        {
            // Comparar con el tipo correcto
            if (respuesta.CampoFormulario.Equals(campo.Id))
            {
                return respuesta.Valor ?? string.Empty;
            }
        }
        return string.Empty;
    }

    public async Task<List<object>> ObtenerCamposFormularioAsync(int formularioId)
    {
        var campos = await _context.CampoFormulario
            .Where(cf => cf.Formulario == formularioId)
            .Select(cf => new
            {
                Id = cf.Id,
                Campo = cf.Campo,
                Descripcion = cf.Descripcion
            })
            .ToListAsync();

        return campos.Cast<object>().ToList();
    }

    public async Task<List<object>> ObtenerRespuestasConValorAsync(int formularioId)
    {
        var respuestas = await _context.RespuestaCampoFormulario
            .Where(rcf => rcf.Formulario == formularioId && rcf.Valor != null && rcf.Valor != "")
            .Take(10)
            .Select(rcf => new
            {
                Solicitud = rcf.Solicitud,
                CampoFormulario = rcf.CampoFormulario,
                Valor = rcf.Valor
            })
            .ToListAsync();

        return respuestas.Cast<object>().ToList();
    }

    public async Task<List<object>> ObtenerTodosCamposAsync()
    {
        var campos = await _context.CampoFormulario
            .Select(cf => new
            {
                Id = cf.Id,
                Formulario = cf.Formulario,
                Campo = cf.Campo,
                Descripcion = cf.Descripcion
            })
            .ToListAsync();

        return campos.Cast<object>().ToList();
    }
}
