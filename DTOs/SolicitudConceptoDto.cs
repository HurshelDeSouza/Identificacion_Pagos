namespace IdentificacionPagos.DTOs;

public class SolicitudConceptoDto
{
    public string NombreConcepto { get; set; } = string.Empty;
    public string FolioRecaudacion { get; set; } = string.Empty;
    public DateTime? FechaPago { get; set; }
    public string CuentaPredial { get; set; } = string.Empty;
    public string AnioInicial { get; set; } = string.Empty;
    public string AnioFinal { get; set; } = string.Empty;
}
