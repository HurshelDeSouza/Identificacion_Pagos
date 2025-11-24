using OfficeOpenXml;
using OfficeOpenXml.Style;
using IdentificacionPagos.DTOs;
using System.Drawing;

namespace IdentificacionPagos.Services;

public class ReporteExcelService
{
    public byte[] GenerarReportePagos(List<SolicitudConceptoDto> datos, DateTime? fechaInicial, DateTime? fechaFinal)
    {
        // Configurar licencia de EPPlus (modo no comercial)
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Reporte de Pagos");

        // Configurar título
        worksheet.Cells["A1"].Value = "REPORTE DE IDENTIFICACIÓN DE PAGOS";
        worksheet.Cells["A1:L1"].Merge = true;
        worksheet.Cells["A1"].Style.Font.Size = 16;
        worksheet.Cells["A1"].Style.Font.Bold = true;
        worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        worksheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
        worksheet.Cells["A1"].Style.Font.Color.SetColor(Color.White);

        // Información de filtros
        int currentRow = 3;
        worksheet.Cells[$"A{currentRow}"].Value = "Filtros Aplicados:";
        worksheet.Cells[$"A{currentRow}"].Style.Font.Bold = true;
        currentRow++;

        worksheet.Cells[$"A{currentRow}"].Value = "Fecha Inicial:";
        worksheet.Cells[$"B{currentRow}"].Value = fechaInicial?.ToString("dd/MM/yyyy") ?? "Sin filtro";
        currentRow++;

        worksheet.Cells[$"A{currentRow}"].Value = "Fecha Final:";
        worksheet.Cells[$"B{currentRow}"].Value = fechaFinal?.ToString("dd/MM/yyyy") ?? "Sin filtro";
        currentRow++;

        worksheet.Cells[$"A{currentRow}"].Value = "Total de Registros:";
        worksheet.Cells[$"B{currentRow}"].Value = datos.Count;
        worksheet.Cells[$"B{currentRow}"].Style.Font.Bold = true;
        currentRow += 2;

        // Encabezados de la tabla
        int headerRow = currentRow;
        worksheet.Cells[$"A{headerRow}"].Value = "Concepto ID";
        worksheet.Cells[$"B{headerRow}"].Value = "Nombre Concepto";
        worksheet.Cells[$"C{headerRow}"].Value = "Folio Recaudación";
        worksheet.Cells[$"D{headerRow}"].Value = "Fecha Pago";
        worksheet.Cells[$"E{headerRow}"].Value = "Cuenta Predial";
        worksheet.Cells[$"F{headerRow}"].Value = "Año Inicial";
        worksheet.Cells[$"G{headerRow}"].Value = "Año Final";
        worksheet.Cells[$"H{headerRow}"].Value = "Nombre Contribuyente";
        worksheet.Cells[$"I{headerRow}"].Value = "Monto";
        worksheet.Cells[$"J{headerRow}"].Value = "Descuento";
        worksheet.Cells[$"K{headerRow}"].Value = "Total";

        // Estilo de encabezados
        using (var range = worksheet.Cells[$"A{headerRow}:K{headerRow}"])
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 225, 242));
            range.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        // Llenar datos
        currentRow++;
        int dataStartRow = currentRow;
        foreach (var item in datos)
        {
            worksheet.Cells[$"A{currentRow}"].Value = item.ConceptoId;
            worksheet.Cells[$"B{currentRow}"].Value = item.NombreConcepto;
            worksheet.Cells[$"C{currentRow}"].Value = item.FolioRecaudacion;
            worksheet.Cells[$"D{currentRow}"].Value = item.FechaPago?.ToString("dd/MM/yyyy HH:mm:ss");
            worksheet.Cells[$"E{currentRow}"].Value = item.CuentaPredial;
            worksheet.Cells[$"F{currentRow}"].Value = item.AnioInicial;
            worksheet.Cells[$"G{currentRow}"].Value = item.AnioFinal;
            worksheet.Cells[$"H{currentRow}"].Value = item.NombreContribuyente;
            worksheet.Cells[$"I{currentRow}"].Value = item.Monto;
            worksheet.Cells[$"J{currentRow}"].Value = item.Descuento;
            worksheet.Cells[$"K{currentRow}"].Value = item.Total;

            currentRow++;
        }

        // Formato de moneda para las columnas de dinero
        if (datos.Count > 0)
        {
            worksheet.Cells[$"I{dataStartRow}:K{currentRow - 1}"].Style.Numberformat.Format = "$#,##0.00";
        }

        // Totales
        if (datos.Count > 0)
        {
            currentRow++;
            worksheet.Cells[$"H{currentRow}"].Value = "TOTALES:";
            worksheet.Cells[$"H{currentRow}"].Style.Font.Bold = true;
            worksheet.Cells[$"I{currentRow}"].Formula = $"SUM(I{dataStartRow}:I{currentRow - 1})";
            worksheet.Cells[$"J{currentRow}"].Formula = $"SUM(J{dataStartRow}:J{currentRow - 1})";
            worksheet.Cells[$"K{currentRow}"].Formula = $"SUM(K{dataStartRow}:K{currentRow - 1})";
            
            using (var range = worksheet.Cells[$"H{currentRow}:K{currentRow}"])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 242, 204));
                range.Style.Numberformat.Format = "$#,##0.00";
            }
        }

        // Bordes para toda la tabla de datos
        if (datos.Count > 0)
        {
            using (var range = worksheet.Cells[$"A{headerRow}:K{currentRow}"])
            {
                range.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            }
        }

        // Ajustar ancho de columnas
        worksheet.Column(1).Width = 12;  // Concepto ID
        worksheet.Column(2).Width = 50;  // Nombre Concepto
        worksheet.Column(3).Width = 20;  // Folio
        worksheet.Column(4).Width = 20;  // Fecha Pago
        worksheet.Column(5).Width = 15;  // Cuenta Predial
        worksheet.Column(6).Width = 12;  // Año Inicial
        worksheet.Column(7).Width = 12;  // Año Final
        worksheet.Column(8).Width = 40;  // Nombre Contribuyente
        worksheet.Column(9).Width = 15;  // Monto
        worksheet.Column(10).Width = 15; // Descuento
        worksheet.Column(11).Width = 15; // Total

        // Congelar paneles (encabezados)
        worksheet.View.FreezePanes(headerRow + 1, 1);

        return package.GetAsByteArray();
    }
}
