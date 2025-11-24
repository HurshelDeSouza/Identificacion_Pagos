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

    public async Task<List<SolicitudConceptoDto>> ObtenerSolicitudesConCuentaPredialAsync(DateTime? fechaInicial = null, DateTime? fechaFinal = null)
    {
        // Obtener IDs de solicitudes que tienen respuestas del formulario con id 1 (Cuenta Predial)
        var solicitudIds = await _context.RespuestaCampoFormulario
            .Where(rcf => rcf.Formulario == 1)
            .Select(rcf => rcf.Solicitud)
            .Distinct()
            .ToListAsync();

        // Obtener solicitudes con sus relaciones usando Include
        // Filtrar por estatus == 2 y rango de fechas
        var query = _context.Solicitud
            .Where(s => solicitudIds.Contains(s.Id) && s.Estatus == 2);

        // Aplicar filtro de fecha si se proporciona
        if (fechaInicial.HasValue)
        {
            query = query.Where(s => s.FechaPago >= fechaInicial.Value);
        }

        if (fechaFinal.HasValue)
        {
            query = query.Where(s => s.FechaPago <= fechaFinal.Value);
        }

        var solicitudes = await query.ToListAsync();

        // Obtener IDs de clientes para cargar sus datos
        var clienteIds = solicitudes
            .Select(s => s.ClientePago ?? s.Cliente)
            .Where(c => c.HasValue)
            .Distinct()
            .ToList();

        // Obtener clientes
        var clientes = await _context.Cliente
            .Where(c => clienteIds.Contains(c.Id))
            .ToListAsync();

        // Obtener conceptos de solicitud con la relación de Concepto usando Include
        var solicitudIdsActualizados = solicitudes.Select(s => s.Id).ToList();
        var conceptosSolicitud = await _context.ConceptoSolicitud
            .Where(cs => solicitudIdsActualizados.Contains(cs.Solicitud))
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

            // Obtener el cliente (priorizar ClientePago, si no existe usar Cliente)
            var clienteId = solicitud.ClientePago ?? solicitud.Cliente;
            var cliente = clienteId.HasValue ? clientes.FirstOrDefault(c => c.Id == clienteId.Value) : null;
            var nombreContribuyente = ObtenerNombreCompleto(cliente);

            // Crear un DTO por cada concepto de la solicitud
            foreach (var conceptoSolicitud in conceptosSolicitudActual)
            {
                var concepto = conceptos.FirstOrDefault(c => c.Id.Equals(conceptoSolicitud.Concepto));
                
                var monto = conceptoSolicitud.Monto ?? 0;
                var descuento = conceptoSolicitud.MontoDescuento ?? 0;
                var total = monto - descuento;

                resultadoFinal.Add(new SolicitudConceptoDto
                {
                    ConceptoId = concepto?.Id ?? 0,
                    NombreConcepto = concepto?.Nombre ?? string.Empty,
                    FolioRecaudacion = solicitud.FolioRecaudacion ?? string.Empty,
                    FechaPago = solicitud.FechaPago,
                    CuentaPredial = cuentaPredial,
                    AnioInicial = ObtenerValorCampo(respuestasSolicitud, campos, "Año Inicial"),
                    AnioFinal = ObtenerValorCampo(respuestasSolicitud, campos, "Año Final"),
                    NombreContribuyente = nombreContribuyente,
                    Monto = monto,
                    Descuento = descuento,
                    Total = total
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

    private string ObtenerNombreCompleto(ERP.CONTEXTPV.Entities.Cliente? cliente)
    {
        if (cliente == null) return string.Empty;

        // Tipo 1 = Persona Física, Tipo 2 = Persona Moral
        if (cliente.Tipo == 1)
        {
            // Persona Física: Apellido Paterno + Apellido Materno + Nombre
            var partes = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(cliente.App))
                partes.Add(cliente.App.Trim());
            
            if (!string.IsNullOrWhiteSpace(cliente.Apm))
                partes.Add(cliente.Apm.Trim());
            
            if (!string.IsNullOrWhiteSpace(cliente.Nombre))
                partes.Add(cliente.Nombre.Trim());
            
            return string.Join(" ", partes);
        }
        else
        {
            // Persona Moral: Razón Social
            return cliente.RazonSocial?.Trim() ?? string.Empty;
        }
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
