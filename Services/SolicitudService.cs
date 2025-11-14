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
        // Obtener todas las solicitudes que tienen respuestas del formulario con id 1 (Cuenta Predial)
        var solicitudesConFormulario = await _context.RespuestaCampoFormulario
            .Where(rcf => rcf.Formulario == 1)
            .Select(rcf => rcf.Solicitud)
            .Distinct()
            .ToListAsync();

        // Obtener los datos de las solicitudes
        var solicitudes = await _context.Solicitud
            .Where(s => solicitudesConFormulario.Contains(s.Id))
            .ToListAsync();

        var solicitudIds = solicitudes.Select(s => s.Id).ToList();

        // Obtener los conceptos relacionados
        var conceptosSolicitud = await _context.ConceptoSolicitud.ToListAsync();
        conceptosSolicitud = conceptosSolicitud.Where(cs => solicitudIds.Contains(cs.Solicitud)).ToList();

        var conceptos = await _context.Concepto.ToListAsync();

        // Obtener las respuestas de los campos específicos del formulario
        var respuestasCampos = await _context.RespuestaCampoFormulario
            .Where(rcf => rcf.Formulario == 1)
            .ToListAsync();
        
        respuestasCampos = respuestasCampos.Where(rcf => rcf.Solicitud != null && solicitudIds.Any(id => id == rcf.Solicitud.Value)).ToList();

        var campos = await _context.CampoFormulario
            .ToListAsync();

        // Combinar los datos
        var resultadoFinal = solicitudes.Select(s =>
        {
            var conceptoSolicitud = conceptosSolicitud.FirstOrDefault(cs => cs.Solicitud.Equals(s.Id));
            var concepto = conceptoSolicitud != null 
                ? conceptos.FirstOrDefault(c => c.Clave.Equals(conceptoSolicitud.Concepto)) 
                : null;

            var respuestasSolicitud = respuestasCampos.Where(rcf => rcf.Solicitud == s.Id).ToList();

            return new SolicitudConceptoDto
            {
                NombreConcepto = concepto?.Nombre ?? string.Empty,
                FolioRecaudacion = s.FolioRecaudacion ?? string.Empty,
                FechaPago = s.FechaPago,
                CuentaPredial = ObtenerValorCampo(respuestasSolicitud, campos, "Clave Catastral"),
                AnioInicial = ObtenerValorCampo(respuestasSolicitud, campos, "Año inicial"),
                AnioFinal = ObtenerValorCampo(respuestasSolicitud, campos, "Año Final")
            };
        })
        .OrderBy(x => x.CuentaPredial)
        .ToList();

        return resultadoFinal;
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
}
