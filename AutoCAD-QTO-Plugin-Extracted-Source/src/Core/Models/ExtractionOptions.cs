using System.Collections.Generic;
using QTO.Core.Enums;

namespace QTO.Core.Models
{
    /// <summary>
    /// Filter options for entity extraction.
    /// </summary>
    public class ExtractionOptions
    {
        // Scope
        public ExtractionScope Scope { get; set; } = ExtractionScope.EntireDrawing;
        public List<string> SelectedLayers { get; set; } = new();
        public List<string> SelectedBlockNames { get; set; } = new();
        public List<EntityType> SelectedEntityTypes { get; set; } = new();

        // Text recognition
        public bool EnableTextRecognition { get; set; } = true;
        public double TextSearchRadius { get; set; } = 1000.0; // drawing units
        public bool UseRegexParsing { get; set; } = true;

        // Electrical module
        public bool EnableElectricalModule { get; set; } = true;

        // Performance
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
        public int BatchSize { get; set; } = 5000;

        // Output
        public bool IncludeCoordinates { get; set; } = false;
        public bool IncludeBoundingBox { get; set; } = false;
        public GroupByMode GroupBy { get; set; } = GroupByMode.Layer;
        public string OutputPath { get; set; } = string.Empty;
        public ExportFormat ExportFormat { get; set; } = ExportFormat.Excel;
        public string ExcelTemplatePath { get; set; } = string.Empty;

        // Project info
        public string ProjectName { get; set; } = string.Empty;
        public string PreparedBy { get; set; } = string.Empty;
    }
}
