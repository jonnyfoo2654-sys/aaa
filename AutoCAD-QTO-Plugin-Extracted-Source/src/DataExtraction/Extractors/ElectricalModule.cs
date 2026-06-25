using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using QTO.Core.Enums;
using QTO.Core.Interfaces;
using QTO.Core.Models;

namespace QTO.DataExtraction.Extractors
{
    /// <summary>
    /// Electrical-specific enrichment module.
    /// Classifies entities and populates ElectricalProperties based on
    /// layer naming conventions and extracted attributes.
    /// </summary>
    public class ElectricalModule : IElectricalModule
    {
        // Layer-to-type mapping based on common electrical drawing conventions
        private static readonly Dictionary<string, ElectricalItemType> LayerMappings =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "EL-CABLE",    ElectricalItemType.LVCable },
            { "EL-LV",       ElectricalItemType.LVCable },
            { "EL-MV",       ElectricalItemType.MVCable },
            { "EL-HV",       ElectricalItemType.MVCable },
            { "EL-TRAY",     ElectricalItemType.CableTray },
            { "EL-DUCT",     ElectricalItemType.Conduit },
            { "EL-CONDUIT",  ElectricalItemType.Conduit },
            { "EL-LIGHT",    ElectricalItemType.LightingFixture },
            { "EL-POLE",     ElectricalItemType.LightingPole },
            { "EL-JB",       ElectricalItemType.JunctionBox },
            { "EL-PANEL",    ElectricalItemType.Panel },
            { "EL-DB",       ElectricalItemType.Panel },
            { "EL-SW",       ElectricalItemType.Switch },
            { "EL-SOCKET",   ElectricalItemType.Socket },
            { "EL-EARTH",    ElectricalItemType.GroundingConductor },
            { "EL-GND",      ElectricalItemType.GroundingConductor },
            { "EL-MANHOLE",  ElectricalItemType.Manhole },
            { "EL-HH",       ElectricalItemType.Handhole },
        };

        // Block-name patterns
        private static readonly (Regex Pattern, ElectricalItemType Type)[] BlockPatterns =
        [
            (new Regex(@"light|luminaire|fixture", RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.LightingFixture),
            (new Regex(@"pole",  RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.LightingPole),
            (new Regex(@"panel|db|mdb|smdb", RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.Panel),
            (new Regex(@"junction|jb",       RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.JunctionBox),
            (new Regex(@"manhole|mh",        RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.Manhole),
            (new Regex(@"handhole|hh",       RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.Handhole),
            (new Regex(@"socket|outlet",     RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.Socket),
            (new Regex(@"switch|sw",         RegexOptions.IgnoreCase | RegexOptions.Compiled), ElectricalItemType.Switch),
        ];

        public ElectricalItemType ClassifyItem(QuantityItem item)
        {
            // 1. Try exact layer match
            if (LayerMappings.TryGetValue(item.Layer, out var layerType))
                return layerType;

            // 2. Try partial layer match
            foreach (var (layer, type) in LayerMappings)
                if (item.Layer.Contains(layer, StringComparison.OrdinalIgnoreCase))
                    return type;

            // 3. Try block name patterns
            if (!string.IsNullOrWhiteSpace(item.BlockName))
                foreach (var (pattern, type) in BlockPatterns)
                    if (pattern.IsMatch(item.BlockName))
                        return type;

            return ElectricalItemType.Unknown;
        }

        public void EnrichItem(QuantityItem item)
        {
            var eType = ClassifyItem(item);
            if (eType == ElectricalItemType.Unknown) return;

            item.ElectricalData ??= new ElectricalProperties();

            // Pull known attributes
            TryGetAttr(item, "SIZE",         v => item.ElectricalData.CableSize = v);
            TryGetAttr(item, "CABLE_SIZE",   v => item.ElectricalData.CableSize = v);
            TryGetAttr(item, "VOLTAGE",      v => item.ElectricalData.VoltageLevel = v);
            TryGetAttr(item, "RATING",       v => item.ElectricalData.Rating = v);
            TryGetAttr(item, "MANUFACTURER", v => item.ElectricalData.Manufacturer = v);
            TryGetAttr(item, "TAG",          v => item.ElectricalData.TagNumber = v);
            TryGetAttr(item, "TAG_NO",       v => item.ElectricalData.TagNumber = v);
            TryGetAttr(item, "HEIGHT",       v =>
            {
                if (double.TryParse(v, out var h))
                    item.ElectricalData.PoleHeight = h;
            });

            // Set defaults based on classification
            switch (eType)
            {
                case ElectricalItemType.LVCable:
                    item.ItemName = string.IsNullOrWhiteSpace(item.ElectricalData.CableSize)
                        ? "LV Cable" : $"LV Cable {item.ElectricalData.CableSize}";
                    item.Unit = "m";
                    if (string.IsNullOrWhiteSpace(item.ElectricalData.VoltageLevel))
                        item.ElectricalData.VoltageLevel = "0.6/1 kV";
                    break;

                case ElectricalItemType.MVCable:
                    item.ItemName = "MV Cable";
                    item.Unit = "m";
                    break;

                case ElectricalItemType.CableTray:
                    item.ItemName = "Cable Tray";
                    item.Unit = "m";
                    break;

                case ElectricalItemType.Conduit:
                    item.ItemName = "Conduit";
                    item.Unit = "m";
                    break;

                case ElectricalItemType.LightingFixture:
                    item.ItemName = "Lighting Fixture";
                    item.Unit = "No";
                    break;

                case ElectricalItemType.LightingPole:
                    item.ItemName = "Lighting Pole";
                    item.Unit = "No";
                    break;

                case ElectricalItemType.JunctionBox:
                    item.ItemName = "Junction Box";
                    item.Unit = "No";
                    break;

                case ElectricalItemType.Panel:
                    item.ItemName = "Panel / DB";
                    item.Unit = "No";
                    break;

                case ElectricalItemType.Manhole:
                    item.ItemName = "Manhole";
                    item.Unit = "No";
                    break;

                case ElectricalItemType.Handhole:
                    item.ItemName = "Handhole";
                    item.Unit = "No";
                    break;

                case ElectricalItemType.GroundingConductor:
                    item.ItemName = "Grounding Conductor";
                    item.Unit = "m";
                    break;
            }
        }

        private static void TryGetAttr(QuantityItem item, string key, Action<string> setter)
        {
            if (item.Attributes.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                setter(val);
        }
    }
}
