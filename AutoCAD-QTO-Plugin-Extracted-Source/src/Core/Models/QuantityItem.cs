using System;
using System.Collections.Generic;

namespace QTO.Core.Models
{
    /// <summary>
    /// Represents a single quantity takeoff item extracted from AutoCAD.
    /// </summary>
    public class QuantityItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ObjectId { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public string ObjectType { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;

        // Quantities
        public double Length { get; set; }
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public double Volume { get; set; }
        public int Count { get; set; } = 1;

        // Geometry
        public Point3D? InsertionPoint { get; set; }
        public BoundingBox? BoundingBox { get; set; }

        // Properties
        public string Color { get; set; } = string.Empty;
        public string Linetype { get; set; } = string.Empty;
        public double Scale { get; set; } = 1.0;
        public double Elevation { get; set; }

        // Block-specific
        public string BlockName { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new();

        // Text recognition results
        public Dictionary<string, string> ExtractedProperties { get; set; } = new();

        // Electrical-specific
        public ElectricalProperties? ElectricalData { get; set; }

        // Metadata
        public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
        public string SourceDrawing { get; set; } = string.Empty;
    }

    /// <summary>
    /// Electrical-specific quantity properties.
    /// </summary>
    public class ElectricalProperties
    {
        public string CableSize { get; set; } = string.Empty;
        public int NumberOfCores { get; set; }
        public string VoltageLevel { get; set; } = string.Empty;
        public string InsulationMaterial { get; set; } = string.Empty;
        public string InstallationMethod { get; set; } = string.Empty;
        public string ConduitSize { get; set; } = string.Empty;
        public double PoleHeight { get; set; }
        public string EquipmentRating { get; set; } = string.Empty;
        public double Depth { get; set; }
        public string Manufacturer { get; set; } = string.Empty;
        public string TagNumber { get; set; } = string.Empty;
        public string Rating { get; set; } = string.Empty;
    }

    /// <summary>
    /// 3D point structure.
    /// </summary>
    public class Point3D
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }

        public double DistanceTo(Point3D other)
        {
            double dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    /// <summary>
    /// Axis-aligned bounding box.
    /// </summary>
    public class BoundingBox
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }

        public BoundingBox(Point3D min, Point3D max)
        {
            Min = min; Max = max;
        }

        public Point3D Center => new(
            (Min.X + Max.X) / 2,
            (Min.Y + Max.Y) / 2,
            (Min.Z + Max.Z) / 2);
    }
}
