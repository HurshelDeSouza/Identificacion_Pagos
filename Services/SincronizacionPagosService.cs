using Microsoft.EntityFrameworkCore;
using IdentificacionPagos.DTOs;
using ERP.CONTEXTPV;
using ERP.CONTEXTSIGSA;
using System.Text.RegularExpressions;

namespace IdentificacionPagos.Services;

public class SincronizacionPagosService
{
    private readonly DbErpPuntoVentaContext _contextPV;
    private readonly SigsaContext _contextSigsa;
    private readonly SolicitudService _solicitudService;

    public SincronizacionPagosService(
        DbErpPuntoVentaContext contextPV, 
        SigsaContext contextSigsa,
        SolicitudService solicitudService)
    {
        _contextPV = contextPV;
        _contextSigsa = contextSigsa;
        _solicitudService = solicitudService;
    }

    public async Task<object> PrevisualizarPagosAsync()
    {
        // Obtener los datos de la tarea anterior
        var solicitudesConceptos = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync();

        // Obtener todos los montos de una vez para evitar consultas repetidas
        var folios = solicitudesConceptos.Select(s => s.FolioRecaudacion).Distinct().ToList();
        
        var solicitudes = await _contextPV.Solicitud
            .Where(s => folios.Contains(s.FolioRecaudacion))
            .ToListAsync();

        var solicitudIds = solicitudes.Select(s => s.Id).ToList();

        var conceptosSolicitud = await _contextPV.ConceptoSolicitud
            .Where(cs => solicitudIds.Contains(cs.Solicitud))
            .ToListAsync();

        var conceptoIds = conceptosSolicitud.Select(cs => cs.Concepto).Distinct().ToList();
        var conceptos = await _contextPV.Concepto
            .Where(c => conceptoIds.Contains(c.Id))
            .ToListAsync();

        var previsualizacion = new List<object>();
        int registrosValidos = 0;
        int registrosOmitidos = 0;

        foreach (var dto in solicitudesConceptos)
        {
            // Normalizar cuenta predial
            var cuentaPredialNormalizada = NormalizarCuentaPredial(dto.CuentaPredial);

            // Validar y procesar fechas
            if (!ProcesarFechas(dto.AnioInicial, dto.AnioFinal, out DateTime? fechaCreacion, out DateTime? fechaVencimiento, out int? anioParaCampo))
            {
                registrosOmitidos++;
                continue;
            }

            // Obtener el monto sin hacer consulta a BD
            var solicitud = solicitudes.FirstOrDefault(s => s.FolioRecaudacion == dto.FolioRecaudacion);
            var conceptoSol = conceptosSolicitud.FirstOrDefault(cs => 
                cs.Solicitud == solicitud?.Id && cs.Concepto == dto.ConceptoId);
            var monto = conceptoSol?.Monto ?? 0;

            previsualizacion.Add(new
            {
                // Datos originales
                cuentaPredialOriginal = dto.CuentaPredial,
                nombreConcepto = dto.NombreConcepto,
                folioRecaudacion = dto.FolioRecaudacion,
                fechaPago = dto.FechaPago,
                anioInicial = dto.AnioInicial,
                anioFinal = dto.AnioFinal,
                
                // Datos que se insertarán en SIS_Pagos
                datosSISPagos = new
                {
                    referencia = $"{{03}}{{{cuentaPredialNormalizada}}}",
                    interlocutor = cuentaPredialNormalizada,
                    descripcion = dto.NombreConcepto,
                    año = anioParaCampo ?? DateTime.Now.Year,
                    division = 0,
                    fechaCreacion = fechaCreacion,
                    fechaVencimiento = fechaVencimiento,
                    cantidad = monto,
                    estatus = "x",
                    folioPago = dto.FolioRecaudacion,
                    fechaPago = dto.FechaPago,
                    origenPago = "M",
                    concepto = 0,
                    folioCancelacion = (string?)null,
                    fechaCancelacion = (DateTime?)null,
                    clavePago = (string?)null
                }
            });

            registrosValidos++;
        }

        return new
        {
            mensaje = "Previsualización de sincronización",
            registrosValidos,
            registrosOmitidos,
            totalProcesados = solicitudesConceptos.Count,
            datos = previsualizacion.Take(10).ToList(),
            nota = previsualizacion.Count > 10 ? $"Mostrando 10 de {previsualizacion.Count} registros" : null
        };
    }

    public async Task<object> SincronizarPagosAsync()
    {
        // Obtener los datos de la tarea anterior
        var solicitudesConceptos = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync();

        // Obtener todos los montos de una vez
        var folios = solicitudesConceptos.Select(s => s.FolioRecaudacion).Distinct().ToList();
        
        var solicitudes = await _contextPV.Solicitud
            .Where(s => folios.Contains(s.FolioRecaudacion))
            .ToListAsync();

        var solicitudIds = solicitudes.Select(s => s.Id).ToList();

        var conceptosSolicitud = await _contextPV.ConceptoSolicitud
            .Where(cs => solicitudIds.Contains(cs.Solicitud))
            .ToListAsync();

        var conceptoIds = conceptosSolicitud.Select(cs => cs.Concepto).Distinct().ToList();
        var conceptos = await _contextPV.Concepto
            .Where(c => conceptoIds.Contains(c.Id))
            .ToListAsync();

        int registrosInsertados = 0;
        int registrosOmitidos = 0;
        var errores = new List<string>();

        foreach (var dto in solicitudesConceptos)
        {
            try
            {
                // Normalizar cuenta predial
                var cuentaPredialNormalizada = NormalizarCuentaPredial(dto.CuentaPredial);

                // Validar y procesar fechas
                if (!ProcesarFechas(dto.AnioInicial, dto.AnioFinal, out DateTime? fechaCreacion, out DateTime? fechaVencimiento, out int? anioParaCampo))
                {
                    registrosOmitidos++;
                    continue;
                }

                // Obtener el monto sin hacer consulta a BD
                var solicitud = solicitudes.FirstOrDefault(s => s.FolioRecaudacion == dto.FolioRecaudacion);
                var conceptoSol = conceptosSolicitud.FirstOrDefault(cs => 
                    cs.Solicitud == solicitud?.Id && cs.Concepto == dto.ConceptoId);
                var monto = conceptoSol?.Monto ?? 0;

                // Crear el registro de pago
                var pago = new ERP.CONTEXTSIGSA.Entities.SIS_Pagos
                {
                    Descripcion = dto.NombreConcepto,
                    Año = anioParaCampo ?? DateTime.Now.Year,
                    Division = 0,
                    FechaCreacion = fechaCreacion,
                    FechaVencimiento = fechaVencimiento,
                    Cantidad = monto,
                    Estatus = "x",
                    FolioPago = dto.FolioRecaudacion,
                    FechaPago = dto.FechaPago,
                    OrigenPago = "M",
                    FolioCancelacion = null,
                    FechaCancelacion = null,
                    ClavePago = null,
                    Referencia = $"{{03}}{{{cuentaPredialNormalizada}}}",
                    Interlocutor = cuentaPredialNormalizada,
                    Concepto = 0
                };

                _contextSigsa.SisPagos.Add(pago);
                registrosInsertados++;
            }
            catch (Exception ex)
            {
                errores.Add($"Error procesando registro {dto.CuentaPredial}: {ex.Message}");
            }
        }

        // Guardar todos los cambios
        await _contextSigsa.SaveChangesAsync();

        return new
        {
            mensaje = "Sincronización completada",
            registrosInsertados,
            registrosOmitidos,
            totalProcesados = solicitudesConceptos.Count,
            errores
        };
    }

    private string NormalizarCuentaPredial(string cuentaPredial)
    {
        if (string.IsNullOrWhiteSpace(cuentaPredial))
            return "U0";

        // Patrón 1: Letra-Número (ej: U-3452, R-123)
        var patron1 = new Regex(@"^([RSU])-(\d+)$", RegexOptions.IgnoreCase);
        var match1 = patron1.Match(cuentaPredial.Trim());
        if (match1.Success)
        {
            // Quitar el guión
            return $"{match1.Groups[1].Value.ToUpper()}{match1.Groups[2].Value}";
        }

        // Patrón 2: Solo número (ej: 345)
        var patron2 = new Regex(@"^\d+$");
        if (patron2.IsMatch(cuentaPredial.Trim()))
        {
            // Agregar U por defecto
            return $"U{cuentaPredial.Trim()}";
        }

        // Si ya tiene el formato correcto (ej: U345, R123)
        var patron3 = new Regex(@"^[RSU]\d+$", RegexOptions.IgnoreCase);
        if (patron3.IsMatch(cuentaPredial.Trim()))
        {
            return cuentaPredial.Trim().ToUpper();
        }

        // Si no coincide con ningún patrón, agregar U por defecto
        return $"U{cuentaPredial.Trim()}";
    }

    private bool ProcesarFechas(string anioInicial, string anioFinal, out DateTime? fechaCreacion, out DateTime? fechaVencimiento, out int? anioParaCampo)
    {
        fechaCreacion = null;
        fechaVencimiento = null;
        anioParaCampo = null;

        // Si ambos están vacíos, omitir
        if (string.IsNullOrWhiteSpace(anioInicial) && string.IsNullOrWhiteSpace(anioFinal))
        {
            return false;
        }

        // Si solo uno tiene valor, usar el mismo para ambos
        string anioParaFechas;
        string anioFinalStr;
        
        if (!string.IsNullOrWhiteSpace(anioInicial) && !string.IsNullOrWhiteSpace(anioFinal))
        {
            anioParaFechas = anioInicial; // Usar año inicial para fechas
            anioFinalStr = anioFinal; // Usar año final para el campo "año"
        }
        else
        {
            // Si solo hay uno, usar el mismo para todo
            anioParaFechas = !string.IsNullOrWhiteSpace(anioInicial) ? anioInicial : anioFinal;
            anioFinalStr = anioParaFechas;
        }

        // Intentar parsear el año para fechas
        if (int.TryParse(anioParaFechas, out int anoFechas))
        {
            fechaCreacion = new DateTime(anoFechas, 1, 1);
            fechaVencimiento = new DateTime(anoFechas, 12, 31);
        }
        else
        {
            return false;
        }

        // Intentar parsear el año final para el campo "año"
        if (int.TryParse(anioFinalStr, out int anoFinal))
        {
            anioParaCampo = anoFinal;
            return true;
        }

        return false;
    }

    private async Task<decimal> ObtenerMontoConceptoSolicitud(SolicitudConceptoDto dto)
    {
        try
        {
            // Buscar la solicitud por folio
            var solicitud = await _contextPV.Solicitud
                .FirstOrDefaultAsync(s => s.FolioRecaudacion == dto.FolioRecaudacion);

            if (solicitud == null)
                return 0;

            // Obtener conceptos de la solicitud
            var conceptosSolicitud = await _contextPV.ConceptoSolicitud
                .Where(cs => cs.Solicitud == solicitud.Id)
                .ToListAsync();

            if (!conceptosSolicitud.Any())
                return 0;

            // Obtener todos los conceptos
            var conceptoIds = conceptosSolicitud.Select(cs => cs.Concepto).Distinct().ToList();
            var conceptos = await _contextPV.Concepto
                .Where(c => conceptoIds.Contains(c.Id))
                .ToListAsync();

            // Buscar el concepto que coincida con el nombre
            var concepto = conceptos.FirstOrDefault(c => c.Nombre == dto.NombreConcepto);
            if (concepto == null)
                return 0;

            // Buscar el monto del concepto_solicitud
            var conceptoSol = conceptosSolicitud.FirstOrDefault(cs => cs.Concepto == concepto.Id);
            return conceptoSol?.Monto ?? 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}
