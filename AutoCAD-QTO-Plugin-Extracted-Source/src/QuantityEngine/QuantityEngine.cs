using System;
using System.Collections.Generic;
using System.Linq;
using QTO.Core.Enums;
using QTO.Core.Interfaces;
using QTO.Core.Models;

namespace QTO.QuantityEngine
{
    /// <summary>
    /// Aggregates raw QuantityItems into a structured BOQSummary.
    /// Groups, sorts, and computes totals ready for export.
    /// </summary>
    public class QuantityEngine : IQuantityEngine
    {
        public BOQSummary Compute(IEnumerable<QuantityItem> items, ExtractionOptions options)
        {
            var itemList = items.ToList();

            var summary = new BOQSummary
            {
                DrawingName = System.IO.Path.GetFileNameWithoutExtension(
                    options.OutputPath.Length > 0 ? options.OutputPath : "Drawing"),
                ProjectName = options.ProjectName,
                PreparedBy = options.PreparedBy,
                Date = DateTime.Now
            };

            // Group items
            summary.Groups = options.GroupBy switch
            {
                GroupByMode.Layer           => GroupByLayer(itemList),
                GroupByMode.ObjectType      => GroupByType(itemList),
                GroupByMode.BlockName       => GroupByBlock(itemList),
                GroupByMode.Layer_Then_Type => GroupByLayerThenType(itemList),
                _ => GroupByLayer(itemList)
            };

            // Statistics
            summary.Stats = BuildStatistics(itemList, summary.Groups);

            return summary;
        }

        // ── Grouping strategies ──────────────────────────────────────────────

        private static List<BOQGroup> GroupByLayer(List<QuantityItem> items)
        {
            return items
                .GroupBy(i => i.Layer)
                .OrderBy(g => g.Key)
                .Select(g => new BOQGroup
                {
                    GroupKey  = g.Key,
                    GroupType = "Layer",
                    Description = $"Layer: {g.Key}",
                    Items = AggregateItems(g.ToList())
                })
                .ToList();
        }

        private static List<BOQGroup> GroupByType(List<QuantityItem> items)
        {
            return items
                .GroupBy(i => i.ObjectType)
                .OrderBy(g => g.Key)
                .Select(g => new BOQGroup
                {
                    GroupKey  = g.Key,
                    GroupType = "ObjectType",
                    Items = AggregateItems(g.ToList())
                })
                .ToList();
        }

        private static List<BOQGroup> GroupByBlock(List<QuantityItem> items)
        {
            return items
                .GroupBy(i => string.IsNullOrWhiteSpace(i.BlockName) ? i.ObjectType : i.BlockName)
                .OrderBy(g => g.Key)
                .Select(g => new BOQGroup
                {
                    GroupKey  = g.Key,
                    GroupType = "Block",
                    Items = AggregateItems(g.ToList())
                })
                .ToList();
        }

        private static List<BOQGroup> GroupByLayerThenType(List<QuantityItem> items)
        {
            return items
                .GroupBy(i => i.Layer)
                .OrderBy(g => g.Key)
                .Select(layerGroup => new BOQGroup
                {
                    GroupKey  = layerGroup.Key,
                    GroupType = "Layer",
                    Items = layerGroup
                        .GroupBy(i => i.ObjectType)
                        .SelectMany(tg => AggregateItems(tg.ToList()))
                        .ToList()
                })
                .ToList();
        }

        /// <summary>
        /// Merges identical block/type items on the same layer into a single row
        /// with summed lengths and areas, and a count.
        /// </summary>
        private static List<QuantityItem> AggregateItems(List<QuantityItem> items)
        {
            return items
                .GroupBy(i => new { i.ItemName, i.ObjectType, i.Unit })
                .Select(g =>
                {
                    var first = g.First();
                    return new QuantityItem
                    {
                        Layer       = first.Layer,
                        ObjectType  = first.ObjectType,
                        ItemName    = first.ItemName,
                        Description = first.Description,
                        Unit        = first.Unit,
                        Length      = Math.Round(g.Sum(i => i.Length), 3),
                        Area        = Math.Round(g.Sum(i => i.Area),   3),
                        Perimeter   = Math.Round(g.Sum(i => i.Perimeter), 3),
                        Volume      = Math.Round(g.Sum(i => i.Volume),    3),
                        Count       = g.Sum(i => i.Count),
                        BlockName   = first.BlockName,
                        // Merge attribute dictionaries (first wins on conflict)
                        Attributes  = g.SelectMany(i => i.Attributes)
                                       .GroupBy(kv => kv.Key)
                                       .ToDictionary(kg => kg.Key, kg => kg.First().Value),
                        ExtractedProperties = g.SelectMany(i => i.ExtractedProperties)
                                               .GroupBy(kv => kv.Key)
                                               .ToDictionary(kg => kg.Key, kg => kg.First().Value),
                        ElectricalData = first.ElectricalData
                    };
                })
                .ToList();
        }

        // ── Statistics ───────────────────────────────────────────────────────

        private static Statistics BuildStatistics(
            List<QuantityItem> items, List<BOQGroup> groups)
        {
            var typeCounts = items
                .GroupBy(i => i.ObjectType)
                .ToDictionary(g => g.Key, g => g.Count());

            var layerLengths = items
                .GroupBy(i => i.Layer)
                .ToDictionary(g => g.Key, g => Math.Round(g.Sum(i => i.Length), 3));

            return new Statistics
            {
                TotalLayers    = groups.Select(g => g.GroupKey).Distinct().Count(),
                TotalObjects   = items.Count,
                TotalCableLength = Math.Round(
                    items.Where(i => i.ElectricalData != null).Sum(i => i.Length), 3),
                TotalArea      = Math.Round(items.Sum(i => i.Area), 3),
                TotalBlockCount = items.Where(i => !string.IsNullOrWhiteSpace(i.BlockName)).Sum(i => i.Count),
                TotalLineCount  = items.Where(i => i.ObjectType is "Line" or "Polyline" or "Polyline3d").Count(),
                ObjectTypeCounts = typeCounts,
                LayerLengths     = layerLengths
            };
        }
    }
}
