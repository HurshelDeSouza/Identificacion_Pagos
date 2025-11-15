using Microsoft.EntityFrameworkCore;
using ERP.CONTEXTPV;
using GRP.ContextCatastro;
using System.Text.RegularExpressions;

namespace IdentificacionPagos.Services;

public class ActualizacionPadronService
{
    private readonly DbErpPuntoVentaContext _contextPV;
    private readonly DbErpCatastroContext _contextCatastro;
    private readonly SolicitudService _solicitudService;

    public ActualizacionPadronService(
        DbErpPuntoVentaContext contextPV, 
        DbErpCatastroContext contextCatastro,
        SolicitudService solicitudService)
    {
        _contextPV = contextPV;
        _contextCatastro = contextCatastro;
        _solicitudService = solicitudService;
    }

    public async Task<object> ActualizarPadronAsync()
    {
        int padronesActualizados = 0;
        int adeudosActualizados = 0;
        int solicitudesOmitidas = 0;
        int padronesNoEncontrados = 0;
        int sinConceptoPredial = 0;

        // Usar el servicio de solicitudes para obtener los datos corregidos
        var solicitudesConceptos = await _solicitudService.ObtenerSolicitudesConCuentaPredialAsync();

        // Filtrar solo los conceptos de impuesto predial
        var conceptosPrediales = solicitudesConceptos
            .Where(sc => sc.NombreConcepto.ToLower().Contains("impuesto predial"))
            .ToList();

        // Procesar cada concepto
        foreach (var dto in conceptosPrediales)
        {
            if (string.IsNullOrWhiteSpace(dto.CuentaPredial))
            {
                solicitudesOmitidas++;
                continue;
            }

            // Normalizar cuenta predial
            var cuentaPredialNormalizada = NormalizarCuentaPredial(dto.CuentaPredial);

            // Buscar en clave_catastral_padron
            var claveCatastral = await _contextCatastro.ClaveCatastralPadron
                .Include(ccp => ccp.PadronNavigation)
                .FirstOrDefaultAsync(ccp => ccp.ClaveCatastral == cuentaPredialNormalizada && 
                                           ccp.TipoClave == 3);

            if (claveCatastral?.PadronNavigation == null)
            {
                padronesNoEncontrados++;
                continue;
            }

            var padron = claveCatastral.PadronNavigation;

            // Obtener año inicial y final del concepto
            int? anioInicial = null;
            int? anioFinal = null;

            if (!string.IsNullOrWhiteSpace(dto.AnioInicial) && int.TryParse(dto.AnioInicial, out int ai))
            {
                anioInicial = ai;
            }

            if (!string.IsNullOrWhiteSpace(dto.AnioFinal) && int.TryParse(dto.AnioFinal, out int af))
            {
                anioFinal = af;
            }

            // Si no hay año final, omitir
            if (!anioFinal.HasValue)
            {
                solicitudesOmitidas++;
                continue;
            }

            // Si no hay año inicial, usar el año final
            if (!anioInicial.HasValue)
            {
                anioInicial = anioFinal;
            }

            // Actualizar último año de pago solo si es mayor
            if (!padron.Pago.HasValue || padron.Pago.Value < anioFinal.Value)
            {
                padron.Pago = anioFinal.Value;
                padronesActualizados++;
            }

            // Actualizar adeudos - obtener todos los adeudos del padrón
            var adeudos = await _contextCatastro.Adeudos
                .Where(a => a.Padron == padron.Id)
                .ToListAsync();

            foreach (var adeudo in adeudos)
            {
                if (adeudo.FechaInicio.HasValue)
                {
                    int anioAdeudo = adeudo.FechaInicio.Value.Year;
                    
                    // Verificar si el año del adeudo está dentro del rango cubierto (entre año inicial y año final)
                    if (anioAdeudo >= anioInicial.Value && anioAdeudo <= anioFinal.Value)
                    {
                        adeudo.Estatus = 2; // Marcar como pagado
                        adeudosActualizados++;
                    }
                }
            }
        }

        await _contextCatastro.SaveChangesAsync();

        return new
        {
            mensaje = "Actualización de padrón completada",
            padronesActualizados,
            adeudosActualizados,
            solicitudesOmitidas,
            padronesNoEncontrados,
            sinConceptoPredial,
            totalConceptosProcesados = conceptosPrediales.Count
        };
    }

    private string NormalizarCuentaPredial(string cuentaPredial)
    {
        if (string.IsNullOrWhiteSpace(cuentaPredial))
            return "U0";

        // Patrón 1: Letra-Numero (ej: U-3452, R-123, S-456)
        var patron1 = new Regex(@"^([RSU])-(\d+)$", RegexOptions.IgnoreCase);
        var match1 = patron1.Match(cuentaPredial.Trim());
        
        if (match1.Success)
        {
            return $"{match1.Groups[1].Value.ToUpper()}{match1.Groups[2].Value}";
        }

        // Patrón 2: Solo número (ej: 345)
        var patron2 = new Regex(@"^\d+$");
        if (patron2.IsMatch(cuentaPredial.Trim()))
        {
            return $"U{cuentaPredial.Trim()}";
        }

        // Si ya tiene el formato correcto
        var patron3 = new Regex(@"^[RSU]\d+$", RegexOptions.IgnoreCase);
        if (patron3.IsMatch(cuentaPredial.Trim()))
        {
            return cuentaPredial.Trim().ToUpper();
        }

        return $"U{cuentaPredial.Trim()}";
    }
}
