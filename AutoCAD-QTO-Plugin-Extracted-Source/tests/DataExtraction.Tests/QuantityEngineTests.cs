using System.Collections.Generic;
using FluentAssertions;
using QTO.Core.Enums;
using QTO.Core.Models;
using QTO.QuantityEngine;
using Xunit;

namespace QTO.DataExtraction.Tests
{
    public class QuantityEngineTests
    {
        private readonly QuantityEngine.QuantityEngine _engine = new();

        private static ExtractionOptions DefaultOptions(GroupByMode mode = GroupByMode.Layer) =>
            new() { GroupBy = mode };

        [Fact]
        public void Compute_EmptyItems_ReturnsSummaryWithNoGroups()
        {
            var summary = _engine.Compute([], DefaultOptions());
            summary.Groups.Should().BeEmpty();
            summary.Stats.TotalObjects.Should().Be(0);
        }

        [Fact]
        public void Compute_GroupsByLayer_CorrectGroupCount()
        {
            var items = new List<QuantityItem>
            {
                new() { Layer = "EL-CABLE", ObjectType = "Polyline", ItemName = "LV Cable", Unit = "m", Length = 100 },
                new() { Layer = "EL-CABLE", ObjectType = "Polyline", ItemName = "LV Cable", Unit = "m", Length = 200 },
                new() { Layer = "EL-LIGHT", ObjectType = "Block",    ItemName = "Lighting Pole", Unit = "No", Count = 5 },
            };

            var summary = _engine.Compute(items, DefaultOptions(GroupByMode.Layer));

            summary.Groups.Should().HaveCount(2);
        }

        [Fact]
        public void Compute_AggregatesLengthCorrectly()
        {
            var items = new List<QuantityItem>
            {
                new() { Layer = "EL-CABLE", ObjectType = "Polyline", ItemName = "LV Cable", Unit = "m", Length = 100 },
                new() { Layer = "EL-CABLE", ObjectType = "Polyline", ItemName = "LV Cable", Unit = "m", Length = 250.5 },
            };

            var summary = _engine.Compute(items, DefaultOptions());

            summary.Groups[0].TotalLength.Should().BeApproximately(350.5, 0.001);
        }

        [Fact]
        public void Compute_Statistics_AreCorrect()
        {
            var items = new List<QuantityItem>
            {
                new() { Layer = "EL-CABLE", ObjectType = "Polyline", ItemName = "Cable", Unit = "m", Length = 500,
                        ElectricalData = new ElectricalProperties() },
                new() { Layer = "EL-LIGHT", ObjectType = "Block",    ItemName = "Pole",  Unit = "No", Count = 10 },
            };

            var summary = _engine.Compute(items, DefaultOptions());

            summary.Stats.TotalObjects.Should().Be(2);
            summary.Stats.TotalCableLength.Should().Be(500);
            summary.Stats.TotalBlockCount.Should().Be(0); // no BlockName set
        }

        [Fact]
        public void Compute_GroupByObjectType_Works()
        {
            var items = new List<QuantityItem>
            {
                new() { Layer = "L1", ObjectType = "Line", ItemName = "Line", Unit = "m", Length = 10 },
                new() { Layer = "L2", ObjectType = "Line", ItemName = "Line", Unit = "m", Length = 20 },
                new() { Layer = "L3", ObjectType = "Circle", ItemName = "Circle", Unit = "No", Count = 3 },
            };

            var summary = _engine.Compute(items, DefaultOptions(GroupByMode.ObjectType));

            summary.Groups.Should().HaveCount(2);
            summary.Groups.Should().Contain(g => g.GroupKey == "Line");
        }
    }
}
