using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QTO.Core.Interfaces;
using QTO.Core.Models;

namespace QTO.ExcelExport
{
    /// <summary>Exports BOQ to CSV format.</summary>
    public class CsvExporter : IExporter
    {
        public async Task ExportAsync(BOQSummary summary, string filePath,
            CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Layer,Item Name,Unit,Quantity,Length(m),Area(m2),Count,Description");

            foreach (var item in summary.AllItems)
            {
                double qty = item.Unit == "No" ? item.Count
                           : item.Unit == "m²" ? item.Area
                           : item.Length;

                sb.AppendLine(string.Join(",",
                    Escape(item.Layer),
                    Escape(item.ItemName),
                    Escape(item.Unit),
                    qty,
                    item.Length,
                    item.Area,
                    item.Count,
                    Escape(item.Description)));

                cancellationToken.ThrowIfCancellationRequested();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, cancellationToken);
        }

        private static string Escape(string s)
        {
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
    }

    /// <summary>Exports BOQ to JSON format.</summary>
    public class JsonExporter : IExporter
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task ExportAsync(BOQSummary summary, string filePath,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                summary.ProjectName,
                summary.DrawingName,
                summary.PreparedBy,
                Date = summary.Date.ToString("yyyy-MM-dd"),
                Statistics = summary.Stats,
                Groups = summary.Groups.Select(g => new
                {
                    g.GroupKey,
                    g.GroupType,
                    g.TotalLength,
                    g.TotalArea,
                    g.TotalCount,
                    Items = g.Items
                })
            };

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            var json = JsonSerializer.Serialize(payload, Options);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }
    }

    /// <summary>Exports BOQ to XML format.</summary>
    public class XmlExporter : IExporter
    {
        public async Task ExportAsync(BOQSummary summary, string filePath,
            CancellationToken cancellationToken = default)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<BOQ>");
            sb.AppendLine($"  <Project>{Esc(summary.ProjectName)}</Project>");
            sb.AppendLine($"  <Drawing>{Esc(summary.DrawingName)}</Drawing>");
            sb.AppendLine($"  <PreparedBy>{Esc(summary.PreparedBy)}</PreparedBy>");
            sb.AppendLine($"  <Date>{summary.Date:yyyy-MM-dd}</Date>");
            sb.AppendLine("  <Groups>");

            foreach (var group in summary.Groups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine($"    <Group key=\"{Esc(group.GroupKey)}\" type=\"{group.GroupType}\">");
                foreach (var item in group.Items)
                {
                    sb.AppendLine("      <Item>");
                    sb.AppendLine($"        <Layer>{Esc(item.Layer)}</Layer>");
                    sb.AppendLine($"        <Name>{Esc(item.ItemName)}</Name>");
                    sb.AppendLine($"        <Unit>{Esc(item.Unit)}</Unit>");
                    sb.AppendLine($"        <Length>{item.Length}</Length>");
                    sb.AppendLine($"        <Area>{item.Area}</Area>");
                    sb.AppendLine($"        <Count>{item.Count}</Count>");
                    if (item.ElectricalData != null)
                    {
                        sb.AppendLine($"        <CableSize>{Esc(item.ElectricalData.CableSize)}</CableSize>");
                        sb.AppendLine($"        <Voltage>{Esc(item.ElectricalData.VoltageLevel)}</Voltage>");
                        sb.AppendLine($"        <Depth>{item.ElectricalData.Depth}</Depth>");
                    }
                    sb.AppendLine("      </Item>");
                }
                sb.AppendLine("    </Group>");
            }

            sb.AppendLine("  </Groups>");
            sb.AppendLine("</BOQ>");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, cancellationToken);
        }

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
