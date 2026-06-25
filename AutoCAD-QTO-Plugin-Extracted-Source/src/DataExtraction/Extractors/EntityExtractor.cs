using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using QTO.Core.Enums;
using QTO.Core.Interfaces;
using QTO.Core.Models;

namespace QTO.DataExtraction.Extractors
{
    /// <summary>
    /// Orchestrates all entity extractors against the active AutoCAD document.
    /// Processes entities in parallel batches to support 500 000+ object drawings.
    /// </summary>
    public class EntityExtractor : IEntityExtractor
    {
        private readonly ITextRecognizer _textRecognizer;
        private readonly IElectricalModule _electricalModule;

        public EntityExtractor(ITextRecognizer textRecognizer,
                               IElectricalModule electricalModule)
        {
            _textRecognizer = textRecognizer;
            _electricalModule = electricalModule;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<QuantityItem>> ExtractAsync(
            ExtractionOptions options,
            IProgress<ExtractionProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<QuantityItem>();

            // All AutoCAD API calls must run on the document thread.
            await Task.Run(() =>
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                using var docLock = doc.LockDocument();
                using var db = doc.Database;
                using var tr = db.TransactionManager.StartOpenCloseTransaction();

                // Collect object ids according to scope
                var objectIds = CollectObjectIds(tr, db, options);

                var textEntities = new List<TextEntity>();
                if (options.EnableTextRecognition)
                    textEntities = CollectTextEntities(tr, db);

                int total = objectIds.Count;
                int processed = 0;

                progress?.Report(new ExtractionProgress
                {
                    Total = total, Processed = 0,
                    CurrentOperation = "Scanning entities…",
                    Status = ProcessingStatus.Scanning
                });

                // Process in batches to keep memory under control
                var batches = objectIds
                    .Select((id, idx) => new { id, idx })
                    .GroupBy(x => x.idx / options.BatchSize)
                    .Select(g => g.Select(x => x.id).ToList());

                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                    CancellationToken = cancellationToken
                };

                foreach (var batch in batches)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // NOTE: AutoCAD transaction objects are not thread-safe.
                    // Open a new transaction per batch when parallel processing.
                    Parallel.ForEach(batch, parallelOptions, objId =>
                    {
                        try
                        {
                            // Open a separate transaction for each thread
                            using var batchTr = db.TransactionManager.StartOpenCloseTransaction();
                            var entity = batchTr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (entity == null) return;

                            var item = ExtractItem(entity, batchTr, options);
                            if (item == null) return;

                            // Spatial text association
                            if (options.EnableTextRecognition && item.InsertionPoint != null)
                            {
                                var nearby = textEntities
                                    .Where(t => t.Position.DistanceTo(item.InsertionPoint) <= options.TextSearchRadius)
                                    .ToList();
                                _textRecognizer.AssociateNearbyText(item, nearby);
                            }

                            // Electrical enrichment
                            if (options.EnableElectricalModule)
                                _electricalModule.EnrichItem(item);

                            results.Add(item);
                        }
                        catch (Exception ex)
                        {
                            // Log and continue – never crash AutoCAD
                            System.Diagnostics.Debug.WriteLine($"QTO extract error on {objId}: {ex.Message}");
                        }
                        finally
                        {
                            Interlocked.Increment(ref processed);
                            progress?.Report(new ExtractionProgress
                            {
                                Total = total, Processed = processed,
                                CurrentOperation = "Extracting quantities…",
                                Status = ProcessingStatus.Extracting
                            });
                        }
                    });
                }

                tr.Commit();
            }, cancellationToken);

            return results;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static List<ObjectId> CollectObjectIds(
            Transaction tr, Database db, ExtractionOptions options)
        {
            var space = (BlockTableRecord)tr.GetObject(
                db.CurrentSpaceId, OpenMode.ForRead);

            var ids = new List<ObjectId>();

            foreach (ObjectId id in space)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                // Layer filter
                if (options.SelectedLayers.Count > 0 &&
                    !options.SelectedLayers.Contains(entity.Layer))
                    continue;

                // Type filter
                var entityType = GetEntityType(entity);
                if (options.SelectedEntityTypes.Count > 0 &&
                    !options.SelectedEntityTypes.Contains(entityType))
                    continue;

                ids.Add(id);
            }

            return ids;
        }

        private static List<TextEntity> CollectTextEntities(
            Transaction tr, Database db)
        {
            var space = (BlockTableRecord)tr.GetObject(
                db.CurrentSpaceId, OpenMode.ForRead);

            var texts = new List<TextEntity>();

            foreach (ObjectId id in space)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead);

                if (entity is DBText dbText)
                    texts.Add(new TextEntity
                    {
                        Content = dbText.TextString,
                        Position = dbText.Position.ToPoint3D(),
                        Layer = dbText.Layer,
                        Height = dbText.Height
                    });
                else if (entity is MText mtext)
                    texts.Add(new TextEntity
                    {
                        Content = mtext.Contents,
                        Position = mtext.Location.ToPoint3D(),
                        Layer = mtext.Layer,
                        Height = mtext.TextHeight
                    });
            }

            return texts;
        }

        private static QuantityItem? ExtractItem(
            Entity entity, Transaction tr, ExtractionOptions options)
        {
            var item = new QuantityItem
            {
                ObjectId = entity.ObjectId.ToString(),
                Layer = entity.Layer,
                Color = entity.Color.ToString(),
                Linetype = entity.Linetype,
                ObjectType = GetEntityType(entity).ToString()
            };

            switch (entity)
            {
                case Line line:
                    item.Length = line.Length;
                    item.InsertionPoint = line.StartPoint.ToPoint3D();
                    item.ItemName = "Line";
                    item.Unit = "m";
                    break;

                case Polyline pl:
                    item.Length = pl.Length;
                    item.Area = pl.Closed ? pl.Area : 0;
                    item.InsertionPoint = pl.GetPoint3dAt(0).ToPoint3D();
                    item.ItemName = "Polyline";
                    item.Unit = pl.Closed ? "m²" : "m";
                    break;

                case Polyline3d pl3d:
                    item.Length = Calculate3dPolylineLength(pl3d, tr);
                    item.InsertionPoint = pl3d.GetPoint3dAt(0).ToPoint3D();
                    item.ItemName = "3D Polyline";
                    item.Unit = "m";
                    break;

                case Arc arc:
                    item.Length = arc.Length;
                    item.InsertionPoint = arc.Center.ToPoint3D();
                    item.ItemName = "Arc";
                    item.Unit = "m";
                    break;

                case Circle circle:
                    item.Length = circle.Circumference;
                    item.Area = circle.Area;
                    item.Perimeter = circle.Circumference;
                    item.InsertionPoint = circle.Center.ToPoint3D();
                    item.ItemName = "Circle";
                    item.Unit = "No";
                    break;

                case BlockReference blkRef:
                    ExtractBlockData(blkRef, tr, item);
                    break;

                case Hatch hatch:
                    item.Area = hatch.Area;
                    item.InsertionPoint = new Point3D(0, 0, 0);
                    item.ItemName = "Hatch";
                    item.Unit = "m²";
                    break;

                case Dimension dim:
                    item.Length = dim.DimensionText.Length > 0
                        ? ParseDimensionValue(dim.DimensionText)
                        : 0;
                    item.ItemName = "Dimension";
                    item.Unit = "m";
                    break;

                default:
                    return null; // skip unsupported types
            }

            // Bounding box
            if (options.IncludeBoundingBox)
            {
                try
                {
                    var ext = entity.GeometricExtents;
                    item.BoundingBox = new BoundingBox(
                        ext.MinPoint.ToPoint3D(),
                        ext.MaxPoint.ToPoint3D());
                }
                catch { /* some entities don't support extents */ }
            }

            return item;
        }

        private static void ExtractBlockData(
            BlockReference blkRef, Transaction tr, QuantityItem item)
        {
            item.BlockName = blkRef.Name;
            item.InsertionPoint = blkRef.Position.ToPoint3D();
            item.Scale = blkRef.ScaleFactors.X;
            item.ItemName = blkRef.Name;
            item.Unit = "No";
            item.Count = 1;

            // Extract attributes
            if (blkRef.AttributeCollection != null)
            {
                foreach (ObjectId attId in blkRef.AttributeCollection)
                {
                    var attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (attRef != null && !string.IsNullOrWhiteSpace(attRef.Tag))
                        item.Attributes[attRef.Tag] = attRef.TextString;
                }
            }

            // Dynamic block properties
            if (blkRef.IsDynamicBlock)
            {
                var props = blkRef.DynamicBlockReferencePropertyCollection;
                if (props != null)
                    foreach (DynamicBlockReferenceProperty prop in props)
                        item.ExtractedProperties[$"DYN_{prop.PropertyName}"] = prop.Value?.ToString() ?? string.Empty;
            }
        }

        private static double Calculate3dPolylineLength(Polyline3d pl3d, Transaction tr)
        {
            double length = 0;
            Point3d? prev = null;
            foreach (ObjectId vtxId in pl3d)
            {
                var vtx = tr.GetObject(vtxId, OpenMode.ForRead) as PolylineVertex3d;
                if (vtx == null) continue;
                if (prev.HasValue)
                    length += prev.Value.DistanceTo(vtx.Position);
                prev = vtx.Position;
            }
            return length;
        }

        private static double ParseDimensionValue(string text)
        {
            if (double.TryParse(
                System.Text.RegularExpressions.Regex.Replace(text, "[^0-9.]", ""),
                out double val))
                return val;
            return 0;
        }

        private static EntityType GetEntityType(Entity entity) => entity switch
        {
            Line => EntityType.Line,
            Polyline => EntityType.Polyline,
            Polyline3d => EntityType.Polyline3d,
            Arc => EntityType.Arc,
            Circle => EntityType.Circle,
            BlockReference blk => blk.IsDynamicBlock
                ? EntityType.DynamicBlock : EntityType.Block,
            MText => EntityType.MText,
            DBText => EntityType.Text,
            Hatch => EntityType.Hatch,
            Dimension => EntityType.Dimension,
            Spline => EntityType.Spline,
            Ellipse => EntityType.Ellipse,
            _ => EntityType.Unknown
        };
    }

    // Extension helpers
    internal static class AcadExtensions
    {
        public static Point3D ToPoint3D(this Point3d p) => new(p.X, p.Y, p.Z);
    }
}
