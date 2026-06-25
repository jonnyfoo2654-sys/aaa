using System.Collections.Generic;
using System.Linq;

namespace QTO.Core.Models
{
    /// <summary>
    /// Bill of Quantities summary grouping items by layer and type.
    /// </summary>
    public class BOQSummary
    {
        public string DrawingName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string PreparedBy { get; set; } = string.Empty;
        public System.DateTime Date { get; set; } = System.DateTime.Now;

        public List<BOQGroup> Groups { get; set; } = new();

        public Statistics Stats { get; set; } = new();

        /// <summary>
        /// Flattens all items from all groups.
        /// </summary>
        public IEnumerable<QuantityItem> AllItems =>
            Groups.SelectMany(g => g.Items);
    }

    /// <summary>
    /// A group of quantity items (e.g., all items on one layer).
    /// </summary>
    public class BOQGroup
    {
        public string GroupKey { get; set; } = string.Empty;    // e.g. layer name
        public string GroupType { get; set; } = string.Empty;   // "Layer", "Block", "Type"
        public string Description { get; set; } = string.Empty;
        public List<QuantityItem> Items { get; set; } = new();

        public double TotalLength => Items.Sum(i => i.Length);
        public double TotalArea => Items.Sum(i => i.Area);
        public int TotalCount => Items.Sum(i => i.Count);
    }

    /// <summary>
    /// Drawing-wide statistics.
    /// </summary>
    public class Statistics
    {
        public int TotalLayers { get; set; }
        public int TotalObjects { get; set; }
        public double TotalCableLength { get; set; }
        public double TotalArea { get; set; }
        public int TotalBlockCount { get; set; }
        public int TotalLineCount { get; set; }
        public System.TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, int> ObjectTypeCounts { get; set; } = new();
        public Dictionary<string, double> LayerLengths { get; set; } = new();
    }
}
