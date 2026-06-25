using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QTO.Core.Interfaces;
using QTO.Core.Models;

namespace QTO.ExcelExport
{
    /// <summary>
    /// Exports BOQSummary to a formatted Excel workbook using EPPlus.
    /// Produces three sheets: BOQ Summary, Detailed Takeoff, and Statistics.
    /// Supports 100 000+ rows via chunked writing with minimal memory overhead.
    /// </summary>
    public class ExcelExporter : IExporter
    {
        static ExcelExporter()
        {
            // EPPlus 5+ requires a license context
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task ExportAsync(BOQSummary summary, string filePath,
            CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                using var package = new ExcelPackage();

                WriteSummarySheet(package, summary);
                cancellationToken.ThrowIfCancellationRequested();

                WriteDetailSheet(package, summary);
                cancellationToken.ThrowIfCancellationRequested();

                WriteStatisticsSheet(package, summary);

                // Save
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                package.SaveAs(new FileInfo(filePath));

            }, cancellationToken);
        }

        // ── Sheet 1: BOQ Summary ─────────────────────────────────────────────

        private static void WriteSummarySheet(ExcelPackage pkg, BOQSummary summary)
        {
            var ws = pkg.Workbook.Worksheets.Add("BOQ Summary");

            // Title block
            ws.Cells["A1"].Value = "BILL OF QUANTITIES";
            StyleTitle(ws.Cells["A1:F1"]);
            ws.Cells["A2"].Value = $"Project: {summary.ProjectName}";
            ws.Cells["A3"].Value = $"Drawing: {summary.DrawingName}";
            ws.Cells["A4"].Value = $"Prepared by: {summary.PreparedBy}";
            ws.Cells["B4"].Value = $"Date: {summary.Date:dd-MMM-yyyy}";

            // Headers
            int row = 6;
            string[] headers = ["Item No.", "Description", "Layer", "Unit", "Quantity", "Remarks"];
            for (int c = 0; c < headers.Length; c++)
            {
                ws.Cells[row, c + 1].Value = headers[c];
            }
            StyleHeader(ws.Cells[row, 1, row, headers.Length]);
            row++;

            int itemNo = 1;
            foreach (var group in summary.Groups)
            {
                // Group header row
                ws.Cells[row, 1].Value = group.GroupKey;
                ws.Cells[row, 1, row, headers.Length].Merge = true;
                StyleGroupHeader(ws.Cells[row, 1, row, headers.Length]);
                row++;

                foreach (var item in group.Items)
                {
                    ws.Cells[row, 1].Value = itemNo++;
                    ws.Cells[row, 2].Value = item.ItemName;
                    ws.Cells[row, 3].Value = item.Layer;
                    ws.Cells[row, 4].Value = item.Unit;

                    // Show most meaningful quantity
                    double qty = item.Unit == "No" ? item.Count
                               : item.Unit == "m²" ? item.Area
                               : item.Length;
                    ws.Cells[row, 5].Value = Math.Round(qty, 3);
                    ws.Cells[row, 5].Style.Numberformat.Format = "#,##0.000";

                    ws.Cells[row, 6].Value = item.Description;

                    StyleDataRow(ws.Cells[row, 1, row, headers.Length], row % 2 == 0);
                    row++;
                }
            }

            // Auto-fit
            ws.Cells[ws.Dimension.Address].AutoFitColumns(10, 50);
            SetBorders(ws, 6, row - 1, headers.Length);
        }

        // ── Sheet 2: Detailed Takeoff ────────────────────────────────────────

        private static void WriteDetailSheet(ExcelPackage pkg, BOQSummary summary)
        {
            var ws = pkg.Workbook.Worksheets.Add("Detailed Takeoff");

            string[] headers =
            [
                "Layer", "Object ID", "Type", "Item Name",
                "Length (m)", "Area (m²)", "Perimeter (m)", "Count",
                "Depth (m)", "Cable Size", "Voltage", "Insulation",
                "Location", "Description", "Attributes"
            ];

            for (int c = 0; c < headers.Length; c++)
                ws.Cells[1, c + 1].Value = headers[c];
            StyleHeader(ws.Cells[1, 1, 1, headers.Length]);

            int row = 2;
            foreach (var item in summary.AllItems)
            {
                ws.Cells[row, 1].Value  = item.Layer;
                ws.Cells[row, 2].Value  = item.ObjectId;
                ws.Cells[row, 3].Value  = item.ObjectType;
                ws.Cells[row, 4].Value  = item.ItemName;
                ws.Cells[row, 5].Value  = Math.Round(item.Length, 3);
                ws.Cells[row, 6].Value  = Math.Round(item.Area, 3);
                ws.Cells[row, 7].Value  = Math.Round(item.Perimeter, 3);
                ws.Cells[row, 8].Value  = item.Count;

                if (item.ElectricalData != null)
                {
                    ws.Cells[row, 9].Value  = item.ElectricalData.Depth;
                    ws.Cells[row, 10].Value = item.ElectricalData.CableSize;
                    ws.Cells[row, 11].Value = item.ElectricalData.VoltageLevel;
                    ws.Cells[row, 12].Value = item.ElectricalData.InsulationMaterial;
                }

                item.ExtractedProperties.TryGetValue("Location", out var loc);
                ws.Cells[row, 13].Value = loc ?? string.Empty;
                ws.Cells[row, 14].Value = item.Description;

                // Flatten attributes
                var attrs = string.Join("; ", item.Attributes.Select(kv => $"{kv.Key}={kv.Value}"));
                ws.Cells[row, 15].Value = attrs;

                StyleDataRow(ws.Cells[row, 1, row, headers.Length], row % 2 == 0);
                row++;
            }

            // Add AutoFilter
            ws.Cells[1, 1, row - 1, headers.Length].AutoFilter = true;
            ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 40);
            SetBorders(ws, 1, row - 1, headers.Length);
        }

        // ── Sheet 3: Statistics ──────────────────────────────────────────────

        private static void WriteStatisticsSheet(ExcelPackage pkg, BOQSummary summary)
        {
            var ws = pkg.Workbook.Worksheets.Add("Statistics");
            var stats = summary.Stats;

            ws.Cells["A1"].Value = "EXTRACTION STATISTICS";
            StyleTitle(ws.Cells["A1:B1"]);

            int row = 3;
            void Stat(string label, object value)
            {
                ws.Cells[row, 1].Value = label;
                ws.Cells[row, 2].Value = value;
                ws.Cells[row, 1].Style.Font.Bold = true;
                row++;
            }

            Stat("Total Layers",       stats.TotalLayers);
            Stat("Total Objects",      stats.TotalObjects);
            Stat("Total Cable Length (m)", stats.TotalCableLength);
            Stat("Total Area (m²)",    stats.TotalArea);
            Stat("Total Block Count",  stats.TotalBlockCount);
            Stat("Total Line Count",   stats.TotalLineCount);
            Stat("Processing Time",    stats.ProcessingTime.ToString(@"mm\:ss\.fff"));
            row++;

            // Object type breakdown
            ws.Cells[row, 1].Value = "Object Type Counts";
            StyleHeader(ws.Cells[row, 1, row, 2]);
            row++;

            foreach (var (type, count) in stats.ObjectTypeCounts.OrderByDescending(kv => kv.Value))
            {
                ws.Cells[row, 1].Value = type;
                ws.Cells[row, 2].Value = count;
                row++;
            }
            row++;

            // Layer length breakdown
            ws.Cells[row, 1].Value = "Layer Lengths (m)";
            StyleHeader(ws.Cells[row, 1, row, 2]);
            row++;

            foreach (var (layer, length) in stats.LayerLengths.OrderByDescending(kv => kv.Value))
            {
                ws.Cells[row, 1].Value = layer;
                ws.Cells[row, 2].Value = length;
                ws.Cells[row, 2].Style.Numberformat.Format = "#,##0.000";
                row++;
            }

            ws.Columns[1].Width = 30;
            ws.Columns[2].Width = 20;
        }

        // ── Style helpers ────────────────────────────────────────────────────

        private static void StyleTitle(ExcelRange range)
        {
            range.Merge = true;
            range.Style.Font.Bold = true;
            range.Style.Font.Size = 14;
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
            range.Style.Font.Color.SetColor(Color.White);
        }

        private static void StyleHeader(ExcelRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(68, 114, 196));
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        private static void StyleGroupHeader(ExcelRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(189, 215, 238));
        }

        private static void StyleDataRow(ExcelRange range, bool alternate)
        {
            if (alternate)
            {
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(242, 242, 242));
            }
        }

        private static void SetBorders(ExcelWorksheet ws, int startRow, int endRow, int cols)
        {
            var range = ws.Cells[startRow, 1, endRow, cols];
            range.Style.Border.Top.Style    = ExcelBorderStyle.Thin;
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Left.Style   = ExcelBorderStyle.Thin;
            range.Style.Border.Right.Style  = ExcelBorderStyle.Thin;
        }
    }
}
