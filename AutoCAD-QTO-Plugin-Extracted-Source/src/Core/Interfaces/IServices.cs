using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QTO.Core.Models;

namespace QTO.Core.Interfaces
{
    /// <summary>
    /// Contract for extracting entities from an AutoCAD document.
    /// </summary>
    public interface IEntityExtractor
    {
        /// <summary>
        /// Extracts all quantity items from the active document.
        /// </summary>
        Task<IEnumerable<QuantityItem>> ExtractAsync(
            ExtractionOptions options,
            IProgress<ExtractionProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Contract for computing BOQ summaries from raw items.
    /// </summary>
    public interface IQuantityEngine
    {
        BOQSummary Compute(IEnumerable<QuantityItem> items, ExtractionOptions options);
    }

    /// <summary>
    /// Contract for recognising properties from nearby text.
    /// </summary>
    public interface ITextRecognizer
    {
        /// <summary>
        /// Parses key/value properties from a raw text string.
        /// </summary>
        Dictionary<string, string> ParseProperties(string text);

        /// <summary>
        /// Associates nearby text objects with the target item.
        /// </summary>
        void AssociateNearbyText(QuantityItem item, IEnumerable<TextEntity> nearbyTexts);
    }

    /// <summary>
    /// Contract for exporting a BOQ to a file.
    /// </summary>
    public interface IExporter
    {
        /// <summary>
        /// Exports the summary to the specified file path.
        /// </summary>
        Task ExportAsync(BOQSummary summary, string filePath,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Contract for the electrical-specific extraction module.
    /// </summary>
    public interface IElectricalModule
    {
        void EnrichItem(QuantityItem item);
        Enums.ElectricalItemType ClassifyItem(QuantityItem item);
    }

    /// <summary>
    /// Represents a text object found in the drawing (MText or Text).
    /// </summary>
    public class TextEntity
    {
        public string Content { get; set; } = string.Empty;
        public Point3D Position { get; set; } = new(0, 0, 0);
        public string Layer { get; set; } = string.Empty;
        public double Height { get; set; }
    }

    /// <summary>
    /// Progress report for extraction operations.
    /// </summary>
    public class ExtractionProgress
    {
        public int Total { get; set; }
        public int Processed { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public Enums.ProcessingStatus Status { get; set; }

        public double Percentage =>
            Total > 0 ? (double)Processed / Total * 100.0 : 0;
    }
}
