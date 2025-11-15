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

        var previsualizacion = new List<object>();
        int registrosValidos = 0;
        int registrosOmitidos = 0;

        foreach (var dto in solicitudesConceptos)
        {
            // Normalizar cuenta predial
            var cuentaPredialNormalizada = NormalizarCuentaPredial(dto.CuentaPredial);

            // Validar y procesar fechas
            if (!ProcesarFechas(dto.AnioInicial, dto.AnioFinal, out DateTime? fechaCreacion, out DateTime? fechaVencimiento))
            {
                registrosOmitidos++;
                continue;
            }

            // Obtener el monto del concepto_solicitud
            var monto = await ObtenerMontoConceptoSolicitud(dto);

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
                    año = fechaVencimiento?.Year ?? DateTime.Now.Year,
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
            datos = previsualizacion.Take(10).ToList(), // Mostrar solo los primeros 10 para no saturar
            nota = previsualizacion.Count > 10 ? $"Mostrando 10 de {previsualizacion.Count} registros" : null
        };
    }

    public async Task<object> SincronizarPagosAsync()
    {
        // Obtener los datos de la tarea anterior
        var solicitudesConceptos = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync();

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
                if (!ProcesarFechas(dto.AnioInicial, dto.AnioFinal, out DateTime? fechaCreacion, out DateTime? fechaVencimiento))
                {
                    registrosOmitidos++;
                    continue; // Omitir si no hay fechas válidas
                }

                // Obtener el monto del concepto_solicitud
                var monto = await ObtenerMontoConceptoSolicitud(dto);

                // Crear el registro de pago
                var pago = new ERP.CONTEXTSIGSA.Entities.SIS_Pagos
                {
                    Descripcion = dto.NombreConcepto,
                    Año = fechaVencimiento?.Year ?? DateTime.Now.Year,
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

    private bool ProcesarFechas(string anioInicial, string anioFinal, out DateTime? fechaCreacion, out DateTime? fechaVencimiento)
    {
        fechaCreacion = null;
        fechaVencimiento = null;

        // Si ambos están vacíos, omitir
        if (string.IsNullOrWhiteSpace(anioInicial) && string.IsNullOrWhiteSpace(anioFinal))
        {
            return false;
        }

        // Si solo uno tiene valor, usar el mismo para ambos
        string anioAUsar;
        if (!string.IsNullOrWhiteSpace(anioInicial) && !string.IsNullOrWhiteSpace(anioFinal))
        {
            anioAUsar = anioInicial; // Usar año inicial si ambos tienen valor
        }
        else
        {
            anioAUsar = !string.IsNullOrWhiteSpace(anioInicial) ? anioInicial : anioFinal;
        }

        // Intentar parsear el año
        if (int.TryParse(anioAUsar, out int ano))
        {
            fechaCreacion = new DateTime(ano, 1, 1);
            fechaVencimiento = new DateTime(ano, 12, 31);
            return true;
        }

        return false;
    }

    private async Task<decimal> ObtenerMontoConceptoSolicitud(SolicitudConceptoDto dto)
    {
        // Buscar el concepto_solicitud correspondiente
        var conceptoSolicitud = await _contextPV.ConceptoSolicitud
            .Join(_contextPV.Solicitud,
                cs => cs.Solicitud,
                s => s.Id,
                (cs, s) => new { ConceptoSolicitud = cs, Solicitud = s })
            .Where(x => x.Solicitud.FolioRecaudacion == dto.FolioRecaudacion)
            .Join(_contextPV.Concepto,
                x => x.ConceptoSolicitud.Concepto,
                c => c.Id,
                (x, c) => new { x.ConceptoSolicitud, x.Solicitud, Concepto = c })
            .Where(x => x.Concepto.Nombre == dto.NombreConcepto)
            .Select(x => x.ConceptoSolicitud.Monto)
            .FirstOrDefaultAsync();

        return conceptoSolicitud ?? 0;
    }
}
