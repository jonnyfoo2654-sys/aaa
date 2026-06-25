using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using QTO.Core.Interfaces;
using QTO.Core.Models;

namespace QTO.TextRecognition
{
    /// <summary>
    /// Parses properties from raw text using regex pattern matching.
    /// Supports formats: "Key = Value", "Key: Value", "Key - Value".
    /// </summary>
    public class TextRecognizer : ITextRecognizer
    {
        // ── Regex patterns ───────────────────────────────────────────────────

        private static readonly (string Key, Regex Pattern)[] Patterns =
        [
            ("Depth",       new Regex(@"depth\s*[=:]\s*([\d.]+)\s*(mm|cm|m)?", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Width",       new Regex(@"width\s*[=:]\s*([\d.]+)\s*(mm|cm|m)?", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Height",      new Regex(@"height\s*[=:]\s*([\d.]+)\s*(mm|cm|m)?", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Diameter",    new Regex(@"(?:dia(?:meter)?|ø)\s*[=:]?\s*([\d.]+)\s*(mm|cm|m)?", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Voltage",     new Regex(@"(?:voltage|volt|kv|lv|mv)\s*[=:]?\s*([\d.]+\s*(?:kv|v)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Material",    new Regex(@"material\s*[=:]\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Size",        new Regex(@"size\s*[=:]\s*([A-Za-z0-9\s\-x×]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("CableSize",   new Regex(@"(\d+[Cc]\s*[×x]\s*[\d.]+\s*mm²?)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Location",    new Regex(@"location\s*[=:]\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Rating",      new Regex(@"rating\s*[=:]\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Description", new Regex(@"desc(?:ription)?\s*[=:]\s*([A-Za-z0-9\s\-,]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("TagNumber",   new Regex(@"tag\s*(?:no|number|#)?\s*[=:]\s*([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
            ("Insulation",  new Regex(@"(?:xlpe|pvc|epr|lsoh)", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ];

        // Unit normalisation: convert all lengths to metres
        private static readonly Dictionary<string, double> UnitMultipliers = new(StringComparer.OrdinalIgnoreCase)
        {
            { "mm", 0.001 },
            { "cm", 0.01 },
            { "m",  1.0 },
        };

        /// <inheritdoc/>
        public Dictionary<string, string> ParseProperties(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (var (key, pattern) in Patterns)
            {
                var match = pattern.Match(text);
                if (!match.Success) continue;

                // Special case for patterns with no capture group (e.g. Insulation)
                var value = match.Groups.Count > 1 && match.Groups[1].Success
                    ? match.Groups[1].Value.Trim()
                    : match.Value.Trim().ToUpper();

                // Append unit if captured
                if (match.Groups.Count > 2 && match.Groups[2].Success)
                    value += " " + match.Groups[2].Value;

                result[key] = value;
            }

            // Generic key=value fallback for unmatched lines
            var generic = Regex.Matches(text, @"([A-Za-z][A-Za-z\s]*)\s*[=:]\s*([^\n,;]+)");
            foreach (Match m in generic)
            {
                var key = m.Groups[1].Value.Trim();
                var val = m.Groups[2].Value.Trim();
                if (!result.ContainsKey(key) && !string.IsNullOrWhiteSpace(val))
                    result[key] = val;
            }

            return result;
        }

        /// <inheritdoc/>
        public void AssociateNearbyText(QuantityItem item, IEnumerable<TextEntity> nearbyTexts)
        {
            foreach (var textEntity in nearbyTexts)
            {
                var props = ParseProperties(textEntity.Content);
                foreach (var (k, v) in props)
                {
                    // Don't overwrite already-extracted attribute data
                    if (!item.ExtractedProperties.ContainsKey(k))
                        item.ExtractedProperties[k] = v;
                }

                // Populate known fields
                if (props.TryGetValue("CableSize", out var cableSize))
                {
                    item.ElectricalData ??= new ElectricalProperties();
                    item.ElectricalData.CableSize = cableSize;
                    ParseCableSize(cableSize, item.ElectricalData);
                }

                if (props.TryGetValue("Depth", out var depth) &&
                    double.TryParse(Regex.Replace(depth, "[^0-9.]", ""), out var d))
                {
                    item.ElectricalData ??= new ElectricalProperties();
                    item.ElectricalData.Depth = NormaliseToMetres(d, depth);
                }

                if (props.TryGetValue("Voltage", out var voltage))
                {
                    item.ElectricalData ??= new ElectricalProperties();
                    item.ElectricalData.VoltageLevel = voltage;
                }

                if (props.TryGetValue("Insulation", out var insul))
                {
                    item.ElectricalData ??= new ElectricalProperties();
                    item.ElectricalData.InsulationMaterial = insul;
                }

                if (props.TryGetValue("Location", out var loc) &&
                    string.IsNullOrWhiteSpace(item.Description))
                    item.Description = loc;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void ParseCableSize(string raw, ElectricalProperties elec)
        {
            // Pattern: "4C x 240 mm²" or "3x185mm2"
            var m = Regex.Match(raw, @"(\d+)\s*[Cc]\s*[×x]\s*([\d.]+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                elec.NumberOfCores = int.Parse(m.Groups[1].Value);
                // conductorArea is available but stored in CableSize string
            }
        }

        private static double NormaliseToMetres(double value, string rawWithUnit)
        {
            foreach (var (unit, mult) in UnitMultipliers)
                if (rawWithUnit.Contains(unit, StringComparison.OrdinalIgnoreCase))
                    return value * mult;
            return value; // assume metres
        }
    }
}
