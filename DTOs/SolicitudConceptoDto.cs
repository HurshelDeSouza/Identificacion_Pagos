namespace IdentificacionPagos.DTOs;

public class SolicitudConceptoDto
{
    public long ConceptoId { get; set; }
    public string NombreConcepto { get; set; } = string.Empty;
    public string FolioRecaudacion { get; set; } = string.Empty;
    public DateTime? FechaPago { get; set; }
    public string CuentaPredial { get; set; } = string.Empty;
    public string AnioInicial { get; set; } = string.Empty;
    public string AnioFinal { get; set; } = string.Empty;
    public string NombreContribuyente { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
}
