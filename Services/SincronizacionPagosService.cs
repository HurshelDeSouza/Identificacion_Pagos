using Microsoft.EntityFrameworkCore;
using IdentificacionPagos.DTOs;
using ERP.CONTEXTPV;
using ERP.CONTEXTSIGSA;
using GRP.ContextCatastro;
using System.Text.RegularExpressions;
using System.Text;

namespace IdentificacionPagos.Services;

public class SincronizacionPagosService
{
    private readonly DbErpPuntoVentaContext _contextPV;
    private readonly SigsaContext _contextSigsa;
    private readonly DbErpCatastroContext _contextCatastro;
    private readonly SolicitudService _solicitudService;

    public SincronizacionPagosService(
        DbErpPuntoVentaContext contextPV, 
        SigsaContext contextSigsa,
        DbErpCatastroContext contextCatastro,
        SolicitudService solicitudService)
    {
        _contextPV = contextPV;
        _contextSigsa = contextSigsa;
        _contextCatastro = contextCatastro;
        _solicitudService = solicitudService;
    }

    public async Task<object> PrevisualizarPagosAsync()
    {
        // Obtener los datos de la tarea anterior (ya incluye montos, descuentos y totales)
        var solicitudesConceptos = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync();

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

            // Usar el monto directamente del DTO (ya viene calculado)
            var monto = dto.Total; // Usar el total (monto - descuento)

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
        // Obtener los datos de la tarea anterior (ya incluye montos, descuentos y totales)
        var solicitudesConceptos = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync();

        // Listas para el detalle
        var registrosInsertados = new List<object>();
        var registrosYaExistentes = new List<object>();
        var registrosSinCuentaPredial = new List<object>();
        var registrosSinFechas = new List<object>();

        // OPTIMIZACIÓN: Cargar todas las cuentas catastrales de una vez
        var cuentasNormalizadas = solicitudesConceptos
            .Select(dto => NormalizarCuentaPredial(dto.CuentaPredial))
            .Distinct()
            .ToList();

        HashSet<string> cuentasCatastralesSet;
        try
        {
            var cuentasCatastrales = await _contextCatastro.ClaveCatastralPadron
                .Where(ccp => cuentasNormalizadas.Contains(ccp.ClaveCatastral) && ccp.TipoClave == 3)
                .Select(ccp => ccp.ClaveCatastral)
                .ToListAsync();

            cuentasCatastralesSet = new HashSet<string>(cuentasCatastrales);
        }
        catch (Exception)
        {
            // Si falla la conexión a catastro, asumir que todas las cuentas son válidas
            cuentasCatastralesSet = new HashSet<string>(cuentasNormalizadas);
        }

        // OPTIMIZACIÓN: Cargar todos los folios existentes de una vez
        var folios = solicitudesConceptos.Select(dto => dto.FolioRecaudacion).Distinct().ToList();
        
        var pagosExistentes = await _contextSigsa.SisPagos
            .Where(p => folios.Contains(p.FolioPago))
            .Select(p => new { p.FolioPago, p.Interlocutor })
            .ToListAsync();

        var pagosExistentesSet = new HashSet<string>(
            pagosExistentes.Select(p => $"{p.FolioPago}|{p.Interlocutor}")
        );

        foreach (var dto in solicitudesConceptos)
        {
            try
            {
                // Normalizar cuenta predial
                var cuentaPredialNormalizada = NormalizarCuentaPredial(dto.CuentaPredial);

                // Validar y procesar fechas
                if (!ProcesarFechas(dto.AnioInicial, dto.AnioFinal, out DateTime? fechaCreacion, out DateTime? fechaVencimiento, out int? anioParaCampo))
                {
                    registrosSinFechas.Add(new
                    {
                        folio = dto.FolioRecaudacion,
                        cuenta = dto.CuentaPredial,
                        concepto = dto.NombreConcepto,
                        monto = dto.Total,
                        razon = "Sin fechas válidas (Año Inicial o Final vacíos)"
                    });
                    continue;
                }

                // Verificar si la cuenta existe en catastro (usando el set precargado)
                if (!cuentasCatastralesSet.Contains(cuentaPredialNormalizada))
                {
                    registrosSinCuentaPredial.Add(new
                    {
                        folio = dto.FolioRecaudacion,
                        cuenta = dto.CuentaPredial,
                        cuentaNormalizada = cuentaPredialNormalizada,
                        concepto = dto.NombreConcepto,
                        monto = dto.Total
                    });
                    continue;
                }

                // Verificar si el pago ya existe (usando el set precargado)
                var clavePago = $"{dto.FolioRecaudacion}|{cuentaPredialNormalizada}";
                if (pagosExistentesSet.Contains(clavePago))
                {
                    registrosYaExistentes.Add(new
                    {
                        folio = dto.FolioRecaudacion,
                        cuenta = dto.CuentaPredial
                    });
                    continue;
                }

                // Usar el monto directamente del DTO (ya viene calculado)
                var monto = dto.Total; // Usar el total (monto - descuento)

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
                
                registrosInsertados.Add(new
                {
                    folio = dto.FolioRecaudacion,
                    cuenta = dto.CuentaPredial,
                    concepto = dto.NombreConcepto,
                    monto = dto.Total
                });
            }
            catch (Exception ex)
            {
                registrosSinCuentaPredial.Add(new
                {
                    folio = dto.FolioRecaudacion,
                    cuenta = dto.CuentaPredial,
                    concepto = dto.NombreConcepto,
                    monto = dto.Total,
                    error = ex.Message
                });
            }
        }

        // Guardar todos los cambios
        await _contextSigsa.SaveChangesAsync();

        // Generar archivo de detalle
        var archivoDetalle = GenerarArchivoDetalle(
            registrosInsertados,
            registrosYaExistentes,
            registrosSinCuentaPredial,
            registrosSinFechas
        );

        return new
        {
            mensaje = "Sincronización completada",
            registrosInsertados = registrosInsertados.Count,
            registrosYaExistentes = registrosYaExistentes.Count,
            registrosSinCuentaPredial = registrosSinCuentaPredial.Count,
            registrosSinFechas = registrosSinFechas.Count,
            totalProcesados = solicitudesConceptos.Count,
            archivoDetalle,
            detalleInsertados = registrosInsertados.Take(10),
            detalleYaExistentes = registrosYaExistentes.Take(10),
            detalleSinCuentaPredial = registrosSinCuentaPredial.Take(10),
            detalleSinFechas = registrosSinFechas.Take(10)
        };
    }

    private string GenerarArchivoDetalle(
        List<object> insertados,
        List<object> yaExistentes,
        List<object> sinCuentaPredial,
        List<object> sinFechas)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine("REPORTE DE SINCRONIZACIÓN DE PAGOS");
        sb.AppendLine($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine();

        // Resumen
        sb.AppendLine("RESUMEN:");
        sb.AppendLine($"  - Registros insertados: {insertados.Count}");
        sb.AppendLine($"  - Registros ya existentes: {yaExistentes.Count}");
        sb.AppendLine($"  - Registros sin cuenta predial en catastro: {sinCuentaPredial.Count}");
        sb.AppendLine($"  - Registros sin fechas válidas: {sinFechas.Count}");
        sb.AppendLine($"  - Total procesados: {insertados.Count + yaExistentes.Count + sinCuentaPredial.Count + sinFechas.Count}");
        sb.AppendLine();

        // Detalle de insertados
        if (insertados.Count > 0)
        {
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("1. REGISTROS INSERTADOS");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();
            foreach (dynamic item in insertados)
            {
                sb.AppendLine($"  Folio: {item.folio}");
                sb.AppendLine($"  Cuenta: {item.cuenta}");
                sb.AppendLine($"  Concepto: {item.concepto}");
                sb.AppendLine($"  Monto: ${item.monto:N2}");
                sb.AppendLine("  " + "-".PadRight(76, '-'));
            }
            sb.AppendLine();
        }

        // Detalle de ya existentes
        if (yaExistentes.Count > 0)
        {
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("2. REGISTROS YA EXISTENTES (OMITIDOS)");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();
            foreach (dynamic item in yaExistentes)
            {
                sb.AppendLine($"  Folio: {item.folio}");
                sb.AppendLine($"  Cuenta: {item.cuenta}");
                sb.AppendLine("  " + "-".PadRight(76, '-'));
            }
            sb.AppendLine();
        }

        // Detalle de sin cuenta predial
        if (sinCuentaPredial.Count > 0)
        {
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("3. REGISTROS SIN CUENTA PREDIAL EN CATASTRO");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();
            foreach (dynamic item in sinCuentaPredial)
            {
                sb.AppendLine($"  Folio: {item.folio}");
                sb.AppendLine($"  Cuenta: {item.cuenta}");
                if (item.GetType().GetProperty("cuentaNormalizada") != null)
                    sb.AppendLine($"  Cuenta Normalizada: {item.cuentaNormalizada}");
                sb.AppendLine($"  Concepto: {item.concepto}");
                sb.AppendLine($"  Monto: ${item.monto:N2}");
                if (item.GetType().GetProperty("error") != null)
                    sb.AppendLine($"  Error: {item.error}");
                sb.AppendLine("  " + "-".PadRight(76, '-'));
            }
            sb.AppendLine();
        }

        // Detalle de sin fechas
        if (sinFechas.Count > 0)
        {
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine("4. REGISTROS SIN FECHAS VÁLIDAS");
            sb.AppendLine("=".PadRight(80, '='));
            sb.AppendLine();
            foreach (dynamic item in sinFechas)
            {
                sb.AppendLine($"  Folio: {item.folio}");
                sb.AppendLine($"  Cuenta: {item.cuenta}");
                sb.AppendLine($"  Concepto: {item.concepto}");
                sb.AppendLine($"  Monto: ${item.monto:N2}");
                sb.AppendLine($"  Razón: {item.razon}");
                sb.AppendLine("  " + "-".PadRight(76, '-'));
            }
            sb.AppendLine();
        }

        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine("FIN DEL REPORTE");
        sb.AppendLine("=".PadRight(80, '='));

        return sb.ToString();
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
