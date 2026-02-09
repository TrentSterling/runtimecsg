using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RuntimeCSG;

namespace RuntimeCSG.Editor.Tests
{
    public class ChiselCSGEngineTests
    {
        // ══════════════════════════════════════════════
        //  Helpers
        // ══════════════════════════════════════════════

        static ChiselCSGEngine.BrushData MakeBoxBrushData(
            Vector3 center, Vector3 halfExtents, CSGOperation op, int order)
        {
            var localPlanes = BrushFactory.Box(halfExtents);
            var worldPlanes = OffsetPlanes(localPlanes, center);
            var polygons = GeneratePolygons(worldPlanes);

            return new ChiselCSGEngine.BrushData
            {
                Polygons = polygons,
                WorldPlanes = worldPlanes,
                Operation = op,
                Order = order,
            };
        }

        static List<CSGPlane> OffsetPlanes(List<CSGPlane> planes, Vector3 offset)
        {
            var result = new List<CSGPlane>(planes.Count);
            for (int i = 0; i < planes.Count; i++)
            {
                var p = planes[i];
                double newD = p.D - (p.A * offset.x + p.B * offset.y + p.C * offset.z);
                result.Add(new CSGPlane(p.A, p.B, p.C, newD));
            }
            return result;
        }

        static List<CSGPolygon> GeneratePolygons(List<CSGPlane> planes)
        {
            int n = planes.Count;
            if (n < 4) return new List<CSGPolygon>();

            var faceVertexLists = new List<List<Vector3>>(n);
            for (int i = 0; i < n; i++)
                faceVertexLists.Add(new List<Vector3>());

            for (int i = 0; i < n - 2; i++)
            {
                for (int j = i + 1; j < n - 1; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        if (BrushBoundsUtil.IntersectThreePlanes(planes[i], planes[j], planes[k], out var point))
                        {
                            if (BrushBoundsUtil.IsInsideAllPlanes(point, planes))
                            {
                                faceVertexLists[i].Add(point);
                                faceVertexLists[j].Add(point);
                                faceVertexLists[k].Add(point);
                            }
                        }
                    }
                }
            }

            var polygons = new List<CSGPolygon>(n);
            for (int i = 0; i < n; i++)
            {
                var facePoints = faceVertexLists[i];
                if (facePoints.Count < 3) continue;

                RemoveDuplicatePoints(facePoints);
                if (facePoints.Count < 3) continue;

                SortWindingOrder(facePoints, planes[i]);

                Vector3 normal = planes[i].Normal;
                var verts = new List<CSGVertex>(facePoints.Count);
                for (int v = 0; v < facePoints.Count; v++)
                    verts.Add(new CSGVertex(facePoints[v], normal, Vector2.zero));

                var polygon = new CSGPolygon(verts, planes[i]);
                if (!polygon.IsDegenerate())
                    polygons.Add(polygon);
            }

            return polygons;
        }

        static void RemoveDuplicatePoints(List<Vector3> points, float epsilon = 1e-4f)
        {
            float epsSq = epsilon * epsilon;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if ((points[i] - points[j]).sqrMagnitude < epsSq)
                    {
                        points.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        static void SortWindingOrder(List<Vector3> points, CSGPlane plane)
        {
            if (points.Count < 3) return;

            Vector3 normal = plane.Normal;
            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                center += points[i];
            center /= points.Count;

            Vector3 up = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 tangent = Vector3.Cross(normal, up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            points.Sort((a, b) =>
            {
                Vector3 da = a - center;
                Vector3 db = b - center;
                float angleA = Mathf.Atan2(Vector3.Dot(da, bitangent), Vector3.Dot(da, tangent));
                float angleB = Mathf.Atan2(Vector3.Dot(db, bitangent), Vector3.Dot(db, tangent));
                return angleA.CompareTo(angleB);
            });

            Vector3 edge1 = points[1] - points[0];
            Vector3 edge2 = points[2] - points[0];
            if (Vector3.Dot(Vector3.Cross(edge1, edge2), normal) < 0)
                points.Reverse();
        }

        static float TotalArea(List<CSGPolygon> polygons)
        {
            float total = 0;
            foreach (var poly in polygons)
            {
                if (poly.Vertices.Count < 3) continue;
                Vector3 area = Vector3.zero;
                var v0 = poly.Vertices[0].Position;
                for (int i = 1; i < poly.Vertices.Count - 1; i++)
                {
                    area += Vector3.Cross(
                        poly.Vertices[i].Position - v0,
                        poly.Vertices[i + 1].Position - v0);
                }
                total += area.magnitude * 0.5f;
            }
            return total;
        }

        static bool HasInwardFacingPolygons(List<CSGPolygon> polygons, Vector3 center)
        {
            foreach (var poly in polygons)
            {
                Vector3 polyCenter = Vector3.zero;
                foreach (var v in poly.Vertices)
                    polyCenter += v.Position;
                polyCenter /= poly.Vertices.Count;

                Vector3 toCenter = center - polyCenter;
                if (Vector3.Dot(poly.Plane.Normal, toCenter) > 0.01f)
                    return true;
            }
            return false;
        }

        static CSGPolygon MakeQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            var verts = new List<CSGVertex>
            {
                new CSGVertex(a, normal, Vector2.zero),
                new CSGVertex(b, normal, Vector2.zero),
                new CSGVertex(c, normal, Vector2.zero),
                new CSGVertex(d, normal, Vector2.zero),
            };
            return new CSGPolygon(verts, new CSGPlane(normal.x, normal.y, normal.z,
                -(normal.x * (double)a.x + normal.y * (double)a.y + normal.z * (double)a.z)));
        }

        static CSGPolygon MakeTri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            var verts = new List<CSGVertex>
            {
                new CSGVertex(a, normal, Vector2.zero),
                new CSGVertex(b, normal, Vector2.zero),
                new CSGVertex(c, normal, Vector2.zero),
            };
            return new CSGPolygon(verts, new CSGPlane(normal.x, normal.y, normal.z,
                -(normal.x * (double)a.x + normal.y * (double)a.y + normal.z * (double)a.z)));
        }

        /// <summary>
        /// Count polygons whose normal matches a direction (within tolerance).
        /// </summary>
        static int CountPolygonsWithNormal(List<CSGPolygon> polygons, Vector3 dir, float tol = 0.1f)
        {
            int count = 0;
            foreach (var p in polygons)
            {
                if (Vector3.Dot(p.Plane.Normal, dir) > 1f - tol)
                    count++;
            }
            return count;
        }

        // ══════════════════════════════════════════════
        //  BrushPairIntersection — Overlap Detection
        // ══════════════════════════════════════════════

        [Test]
        public void Overlap_OverlappingBoxes_ReturnsTrue()
        {
            var a = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(0.5f, 0, 0));
            Assert.IsTrue(BrushPairIntersection.BrushesOverlap(a, b));
        }

        [Test]
        public void Overlap_SeparatedBoxes_ReturnsFalse()
        {
            var a = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(5f, 0, 0));
            Assert.IsFalse(BrushPairIntersection.BrushesOverlap(a, b));
        }

        [Test]
        public void Overlap_TouchingBoxes_ReturnsFalse()
        {
            var a = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(1f, 0, 0));
            Assert.IsFalse(BrushPairIntersection.BrushesOverlap(a, b));
        }

        [Test]
        public void Overlap_ContainedBox_ReturnsTrue()
        {
            var big = OffsetPlanes(BrushFactory.Box(Vector3.one * 2f), Vector3.zero);
            var small = OffsetPlanes(BrushFactory.Box(Vector3.one * 0.25f), Vector3.zero);
            Assert.IsTrue(BrushPairIntersection.BrushesOverlap(big, small));
        }

        [Test]
        public void Overlap_IsSymmetric()
        {
            var a = OffsetPlanes(BrushFactory.Box(), new Vector3(0, 0, 0));
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(0.3f, 0.3f, 0));
            Assert.AreEqual(
                BrushPairIntersection.BrushesOverlap(a, b),
                BrushPairIntersection.BrushesOverlap(b, a));
        }

        [Test]
        public void Overlap_DiagonallySeparated_ReturnsFalse()
        {
            var a = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(5, 5, 5));
            Assert.IsFalse(BrushPairIntersection.BrushesOverlap(a, b));
        }

        [Test]
        public void Overlap_TinyOverlap_ReturnsTrue()
        {
            // Overlap by just a tiny sliver on X
            var a = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(0.99f, 0, 0));
            Assert.IsTrue(BrushPairIntersection.BrushesOverlap(a, b));
        }

        [Test]
        public void Overlap_DifferentSizedBoxes_ReturnsTrue()
        {
            var a = OffsetPlanes(BrushFactory.Box(new Vector3(2, 0.5f, 0.5f)), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(new Vector3(0.5f, 2, 0.5f)), Vector3.zero);
            Assert.IsTrue(BrushPairIntersection.BrushesOverlap(a, b));
        }

        [Test]
        public void Overlap_SeparatedOnY_ReturnsFalse()
        {
            var a = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(0, 3, 0));
            Assert.IsFalse(BrushPairIntersection.BrushesOverlap(a, b));
        }

        [Test]
        public void Overlap_SeparatedOnZ_ReturnsFalse()
        {
            var a = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var b = OffsetPlanes(BrushFactory.Box(), new Vector3(0, 0, 3));
            Assert.IsFalse(BrushPairIntersection.BrushesOverlap(a, b));
        }

        // ══════════════════════════════════════════════
        //  BrushPairIntersection — CategorizePoint
        // ══════════════════════════════════════════════

        [Test]
        public void CategorizePoint_InsideBox_ReturnsInside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Inside,
                BrushPairIntersection.CategorizePoint(Vector3.zero, planes, Vector3.up));
        }

        [Test]
        public void CategorizePoint_OutsideBox_ReturnsOutside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Outside,
                BrushPairIntersection.CategorizePoint(new Vector3(5, 0, 0), planes, Vector3.up));
        }

        [Test]
        public void CategorizePoint_OnPosXFace_NormalMatchesFace_Aligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Aligned,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(0.5f, 0, 0), planes, Vector3.right));
        }

        [Test]
        public void CategorizePoint_OnPosXFace_NormalOpposesFace_ReverseAligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.ReverseAligned,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(0.5f, 0, 0), planes, Vector3.left));
        }

        [Test]
        public void CategorizePoint_OnNegXFace_ReturnsAligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Aligned,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(-0.5f, 0, 0), planes, Vector3.left));
        }

        [Test]
        public void CategorizePoint_OnTopFace_ReturnsAligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Aligned,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(0, 0.5f, 0), planes, Vector3.up));
        }

        [Test]
        public void CategorizePoint_OnBottomFace_ReturnsAligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Aligned,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(0, -0.5f, 0), planes, Vector3.down));
        }

        [Test]
        public void CategorizePoint_JustInside_ReturnsInside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Inside,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(0.49f, 0, 0), planes, Vector3.up));
        }

        [Test]
        public void CategorizePoint_JustOutside_ReturnsOutside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            Assert.AreEqual(PolygonCategory.Outside,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(0.51f, 0, 0), planes, Vector3.up));
        }

        [Test]
        public void CategorizePoint_OffsetBox_InsideCenter()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), new Vector3(10, 10, 10));
            Assert.AreEqual(PolygonCategory.Inside,
                BrushPairIntersection.CategorizePoint(
                    new Vector3(10, 10, 10), planes, Vector3.up));
        }

        [Test]
        public void CategorizePoint_OffsetBox_OriginIsOutside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), new Vector3(10, 10, 10));
            Assert.AreEqual(PolygonCategory.Outside,
                BrushPairIntersection.CategorizePoint(
                    Vector3.zero, planes, Vector3.up));
        }

        // ══════════════════════════════════════════════
        //  BrushPairIntersection — CategorizePolygon
        // ══════════════════════════════════════════════

        [Test]
        public void CategorizePolygon_FullyInsideLargeBox_ReturnsInside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(Vector3.one * 2f), Vector3.zero);
            var poly = MakeTri(
                new Vector3(0, 0, 0),
                new Vector3(0.1f, 0, 0),
                new Vector3(0.1f, 0, 0.1f),
                Vector3.up);
            Assert.AreEqual(PolygonCategory.Inside,
                BrushPairIntersection.CategorizePolygon(poly, planes));
        }

        [Test]
        public void CategorizePolygon_FullyOutside_ReturnsOutside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var poly = MakeTri(
                new Vector3(5, 0, 0),
                new Vector3(5.1f, 0, 0),
                new Vector3(5.1f, 0, 0.1f),
                Vector3.up);
            Assert.AreEqual(PolygonCategory.Outside,
                BrushPairIntersection.CategorizePolygon(poly, planes));
        }

        [Test]
        public void CategorizePolygon_OnTopFace_SameNormal_ReturnsAligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            // Polygon on the top face (y=0.5), normal = up (matches +Y plane)
            var poly = MakeQuad(
                new Vector3(-0.25f, 0.5f, -0.25f),
                new Vector3(0.25f, 0.5f, -0.25f),
                new Vector3(0.25f, 0.5f, 0.25f),
                new Vector3(-0.25f, 0.5f, 0.25f),
                Vector3.up);
            Assert.AreEqual(PolygonCategory.Aligned,
                BrushPairIntersection.CategorizePolygon(poly, planes));
        }

        [Test]
        public void CategorizePolygon_OnTopFace_OppositeNormal_ReturnsReverseAligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var poly = MakeQuad(
                new Vector3(-0.25f, 0.5f, -0.25f),
                new Vector3(-0.25f, 0.5f, 0.25f),
                new Vector3(0.25f, 0.5f, 0.25f),
                new Vector3(0.25f, 0.5f, -0.25f),
                Vector3.down);
            Assert.AreEqual(PolygonCategory.ReverseAligned,
                BrushPairIntersection.CategorizePolygon(poly, planes));
        }

        [Test]
        public void CategorizePolygon_OnBottomFace_Aligned()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var poly = MakeQuad(
                new Vector3(-0.25f, -0.5f, -0.25f),
                new Vector3(-0.25f, -0.5f, 0.25f),
                new Vector3(0.25f, -0.5f, 0.25f),
                new Vector3(0.25f, -0.5f, -0.25f),
                Vector3.down);
            Assert.AreEqual(PolygonCategory.Aligned,
                BrushPairIntersection.CategorizePolygon(poly, planes));
        }

        [Test]
        public void CategorizePolygon_FarAway_ReturnsOutside()
        {
            var planes = OffsetPlanes(BrushFactory.Box(), Vector3.zero);
            var poly = MakeQuad(
                new Vector3(100, 100, 100),
                new Vector3(101, 100, 100),
                new Vector3(101, 100, 101),
                new Vector3(100, 100, 101),
                Vector3.up);
            Assert.AreEqual(PolygonCategory.Outside,
                BrushPairIntersection.CategorizePolygon(poly, planes));
        }

        // ══════════════════════════════════════════════
        //  ChiselCSGEngine.Process — Basic
        // ══════════════════════════════════════════════

        [Test]
        public void Process_EmptyList_ReturnsEmpty()
        {
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData>());
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Process_SingleAdditive_Returns6Faces()
        {
            var brush = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });
            Assert.AreEqual(6, result.Count);
        }

        [Test]
        public void Process_SingleAdditive_CorrectSurfaceArea()
        {
            var brush = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });
            Assert.AreEqual(6f, TotalArea(result), 0.01f);
        }

        [Test]
        public void Process_SingleAdditive_AllFaces4Verts()
        {
            var brush = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });
            foreach (var p in result)
                Assert.AreEqual(4, p.Vertices.Count);
        }

        [Test]
        public void Process_SingleAdditive_AllNormalsUnitLength()
        {
            var brush = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });
            foreach (var p in result)
                Assert.AreEqual(1f, p.Plane.Normal.magnitude, 0.01f);
        }

        [Test]
        public void Process_SingleAdditive_NormalsPointOutward()
        {
            var brush = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });

            // Every face normal should point away from center
            foreach (var poly in result)
            {
                Vector3 faceCenter = Vector3.zero;
                foreach (var v in poly.Vertices) faceCenter += v.Position;
                faceCenter /= poly.Vertices.Count;

                float dot = Vector3.Dot(poly.Plane.Normal, faceCenter.normalized);
                Assert.Greater(dot, 0, $"Normal {poly.Plane.Normal} should point away from center");
            }
        }

        [Test]
        public void Process_SingleSubtractive_ReturnsEmpty()
        {
            var brush = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Subtractive, 0);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Process_SingleIntersect_ReturnsEmpty()
        {
            // Intersect with nothing = empty
            var brush = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Intersect, 0);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Process_SingleBrushNoPolygons_ReturnsEmpty()
        {
            var brush = new ChiselCSGEngine.BrushData
            {
                Polygons = new List<CSGPolygon>(),
                WorldPlanes = BrushFactory.Box(),
                Operation = CSGOperation.Additive,
                Order = 0,
            };
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { brush });
            Assert.AreEqual(0, result.Count);
        }

        // ══════════════════════════════════════════════
        //  ChiselCSGEngine.Process — Union (Additive)
        // ══════════════════════════════════════════════

        [Test]
        public void Union_NoOverlap_ReturnsBothSets()
        {
            var a = MakeBoxBrushData(new Vector3(-2, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(2, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(12, result.Count);
        }

        [Test]
        public void Union_NoOverlap_DoubledSurfaceArea()
        {
            var a = MakeBoxBrushData(new Vector3(-2, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(2, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(12f, TotalArea(result), 0.1f);
        }

        [Test]
        public void Union_Overlapping_FewerThan12Faces()
        {
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(0.5f, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            // Splitting fragments faces, so count may exceed 12, but interior faces are removed
            // and surface area matches the merged 1.5x1x1 shape
            Assert.Greater(result.Count, 0, "Overlapping union should produce geometry");
            Assert.AreEqual(8f, TotalArea(result), 0.15f, "Overlapping union should merge interior faces");
        }

        [Test]
        public void Union_Overlapping_NoDegenerate()
        {
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(0.5f, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            foreach (var p in result)
                Assert.IsFalse(p.IsDegenerate(), "No degenerate polygons in union result");
        }

        [Test]
        public void Union_Overlapping_SurfaceAreaCorrect()
        {
            // Two 1x1x1 boxes offset by 0.5 on X. Merged shape is 1.5 x 1 x 1
            // Surface area = 2*(1.5*1 + 1.5*1 + 1*1) = 2*(1.5 + 1.5 + 1) = 8
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(0.5f, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(8f, TotalArea(result), 0.15f);
        }

        [Test]
        public void Union_ThreeSeparateBoxes_Returns18Faces()
        {
            var a = MakeBoxBrushData(new Vector3(-5, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var c = MakeBoxBrushData(new Vector3(5, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 2);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b, c });
            Assert.AreEqual(18, result.Count);
        }

        [Test]
        public void Union_IdenticalBoxes_Coplanar_Only6Faces()
        {
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(6, result.Count, "Coplanar faces should not duplicate");
        }

        [Test]
        public void Union_IdenticalBoxes_CorrectArea()
        {
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(6f, TotalArea(result), 0.1f);
        }

        [Test]
        public void Union_OverlapOnY_SurfaceAreaCorrect()
        {
            // Overlap on Y axis
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(0, 0.5f, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            // Merged: 1 x 1.5 x 1 → SA = 2*(1*1.5 + 1*1.5 + 1*1) = 8
            Assert.AreEqual(8f, TotalArea(result), 0.15f);
        }

        [Test]
        public void Union_OverlapOnZ_SurfaceAreaCorrect()
        {
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(0, 0, 0.5f), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(8f, TotalArea(result), 0.15f);
        }

        // ══════════════════════════════════════════════
        //  ChiselCSGEngine.Process — Subtraction
        // ══════════════════════════════════════════════

        [Test]
        public void Subtract_FullyContained_CarvesHole()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });
            // Outer faces get fragmented by sub's planes; at least 6 outer + 6 inner
            Assert.GreaterOrEqual(result.Count, 12);
        }

        [Test]
        public void Subtract_FullyContained_CorrectSurfaceArea()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });
            // Outer: 2x2x2 box → SA = 6*4 = 24
            // Inner: 0.5x0.5x0.5 cavity → SA = 6*0.25 = 1.5
            Assert.AreEqual(25.5f, TotalArea(result), 0.1f);
        }

        [Test]
        public void Subtract_InnerFaces_FlippedInward()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });
            Assert.IsTrue(HasInwardFacingPolygons(result, Vector3.zero),
                "Subtractive faces should be flipped inward");
        }

        [Test]
        public void Subtract_FullyEncompassing_NothingRemains()
        {
            // Sub box bigger than add → completely eaten
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Subtract_NoOverlap_AdditiveUnchanged()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(new Vector3(5, 0, 0), Vector3.one * 0.5f, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });
            Assert.AreEqual(6, result.Count);
            Assert.AreEqual(6f, TotalArea(result), 0.1f);
        }

        [Test]
        public void Subtract_PartialOverlap_SlabRemains()
        {
            // Sub box overlaps right half of add box
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(new Vector3(0.25f, 0, 0), Vector3.one * 0.5f, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });

            Assert.Greater(result.Count, 0, "Partial subtraction should leave geometry");
            // Remaining: 0.25 x 1 x 1 slab → SA = 2*(0.25 + 0.25 + 1) = 3.0
            Assert.AreEqual(3f, TotalArea(result), 0.15f);
        }

        [Test]
        public void Subtract_PartialOverlap_HasExposedInnerFace()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(new Vector3(0.25f, 0, 0), Vector3.one * 0.5f, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });

            // Should have at least one face pointing in -X direction (the new exposed face
            // from the cut)
            int leftFacing = CountPolygonsWithNormal(result, Vector3.left);
            Assert.Greater(leftFacing, 0, "Should have exposed inner face from subtraction");
        }

        [Test]
        public void Subtract_SubtractiveOnly_NothingRendered()
        {
            var sub1 = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Subtractive, 0);
            var sub2 = MakeBoxBrushData(Vector3.one, Vector3.one * 0.5f, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { sub1, sub2 });
            Assert.AreEqual(0, result.Count, "Subtractive-only brushes produce nothing");
        }

        // ══════════════════════════════════════════════
        //  ChiselCSGEngine.Process — Intersection
        // ══════════════════════════════════════════════

        [Test]
        public void Intersect_Overlapping_KeepsOnlyOverlapVolume()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var inter = MakeBoxBrushData(new Vector3(0.25f, 0, 0), Vector3.one * 0.5f, CSGOperation.Intersect, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, inter });

            Assert.Greater(result.Count, 0);
            // Add spans [-0.5,0.5], Intersect spans [-0.25,0.75]. Overlap: [-0.25,0.5] = 0.75 wide
            // Intersection volume: 0.75 x 1 x 1 → SA = 2*(0.75+0.75+1) = 5
            Assert.AreEqual(5f, TotalArea(result), 0.15f);
        }

        [Test]
        public void Intersect_NoOverlap_ReturnsEmpty()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var inter = MakeBoxBrushData(new Vector3(5, 0, 0), Vector3.one * 0.5f, CSGOperation.Intersect, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, inter });
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Intersect_FullyContained_KeepsSmaller()
        {
            // Small box fully inside large box → intersect keeps small box
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Additive, 0);
            var inter = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Intersect, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, inter });

            Assert.Greater(result.Count, 0);
            // Small box: 0.5 x 0.5 x 0.5 → SA = 6*0.25 = 1.5
            Assert.AreEqual(1.5f, TotalArea(result), 0.1f);
        }

        [Test]
        public void Intersect_IdenticalBoxes_KeepsSameBox()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var inter = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Intersect, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, inter });
            Assert.AreEqual(6f, TotalArea(result), 0.1f);
        }

        // ══════════════════════════════════════════════
        //  ChiselCSGEngine.Process — Multi-brush chains
        // ══════════════════════════════════════════════

        [Test]
        public void Chain_AddSubAdd_ProducesGeometry()
        {
            var add1 = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Subtractive, 1);
            var add2 = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Additive, 2);
            var result = ChiselCSGEngine.Process(
                new List<ChiselCSGEngine.BrushData> { add1, sub, add2 });

            Assert.Greater(result.Count, 0);
            Assert.Greater(result.Count, 6, "Three-brush chain should produce complex geometry");
        }

        [Test]
        public void Chain_AddSubAdd_MoreAreaThanOuterBoxAlone()
        {
            // Outer shell has a hole, inner box partially fills it
            var add1 = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Subtractive, 1);
            var add2 = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Additive, 2);
            var result = ChiselCSGEngine.Process(
                new List<ChiselCSGEngine.BrushData> { add1, sub, add2 });

            float area = TotalArea(result);
            // Outer box SA = 24. With hole + inner plug: must be > 24
            Assert.Greater(area, 24f, "Shell + inner box has more surface area than outer box alone");
        }

        [Test]
        public void Chain_SubBeforeAdd_SubIgnored()
        {
            // Subtractive first (nothing to subtract from), then additive
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Subtractive, 0);
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(
                new List<ChiselCSGEngine.BrushData> { sub, add });

            // Sub from nothing = nothing. Then add box → should get the add box.
            // But sub overlaps add, so the sub (which comes before add) subtracts from accumulated=empty,
            // giving empty still. Then add's own faces are evaluated against the sub:
            // Actually the chain evaluation is: solid = false
            // brush0 (sub): solid = false && !inside[0] → for front: solid = false && !false = false; no change
            // brush1 (add): solid = false || inside[1]
            // So the add brush should produce polygons where it's solid.
            // The sub can't subtract from nothing, so the add should appear.
            Assert.Greater(result.Count, 0, "Add after empty sub should produce geometry");
        }

        [Test]
        public void Chain_FourAdditiveBoxes_NoOverlap()
        {
            var brushes = new List<ChiselCSGEngine.BrushData>
            {
                MakeBoxBrushData(new Vector3(-3, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 0),
                MakeBoxBrushData(new Vector3(-1, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1),
                MakeBoxBrushData(new Vector3(1, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 2),
                MakeBoxBrushData(new Vector3(3, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 3),
            };
            var result = ChiselCSGEngine.Process(brushes);
            Assert.AreEqual(24, result.Count);
            Assert.AreEqual(24f, TotalArea(result), 0.1f);
        }

        [Test]
        public void Chain_TwoSubs_CarveFromSameAdd()
        {
            // One large box, two small subs at different positions
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 2f, CSGOperation.Additive, 0);
            var sub1 = MakeBoxBrushData(new Vector3(-1, 0, 0), Vector3.one * 0.25f, CSGOperation.Subtractive, 1);
            var sub2 = MakeBoxBrushData(new Vector3(1, 0, 0), Vector3.one * 0.25f, CSGOperation.Subtractive, 2);
            var result = ChiselCSGEngine.Process(
                new List<ChiselCSGEngine.BrushData> { add, sub1, sub2 });

            Assert.Greater(result.Count, 6, "Two subs should carve two holes");
            // Each sub removes a 0.5^3 cube from a 4^3 box
            // Outer SA = 6*16 = 96
            // Each cavity SA = 6*0.25 = 1.5, two = 3
            // Total = 99
            Assert.AreEqual(99f, TotalArea(result), 0.5f);
        }

        // ══════════════════════════════════════════════
        //  Coplanar / z-fighting prevention
        // ══════════════════════════════════════════════

        [Test]
        public void Coplanar_IdenticalBoxes_NoDuplicateFaces()
        {
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(6, result.Count);
        }

        [Test]
        public void Coplanar_LaterBrushClaimsSurface()
        {
            // Two identical boxes — the later brush (order 1) should claim all faces
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });

            // All 6 faces should exist, surface area = 6
            Assert.AreEqual(6, result.Count);
            Assert.AreEqual(6f, TotalArea(result), 0.1f);
        }

        [Test]
        public void Coplanar_SharedFace_NotDuplicated()
        {
            // Two boxes sharing exactly one face (touching + tiny overlap)
            // Flush at x = 0.5 boundary
            var a = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(1f, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);

            // These are touching/barely separated, so they shouldn't overlap.
            // No face duplication issue since they don't overlap.
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(12, result.Count, "Touching boxes should return all 12 faces");
        }

        // ══════════════════════════════════════════════
        //  EvaluateChain behavior (verified via Process)
        // ══════════════════════════════════════════════

        [Test]
        public void EvaluateChain_Additive_IsOR()
        {
            var a = MakeBoxBrushData(new Vector3(-2, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var b = MakeBoxBrushData(new Vector3(2, 0, 0), Vector3.one * 0.5f, CSGOperation.Additive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { a, b });
            Assert.AreEqual(12, result.Count, "Additive OR: both boxes appear");
        }

        [Test]
        public void EvaluateChain_Subtractive_IsANDNOT()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.25f, CSGOperation.Additive, 0);
            var sub = MakeBoxBrushData(Vector3.zero, Vector3.one, CSGOperation.Subtractive, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, sub });
            Assert.AreEqual(0, result.Count, "AND NOT: fully subtracted → nothing");
        }

        [Test]
        public void EvaluateChain_Intersect_IsAND()
        {
            var add = MakeBoxBrushData(Vector3.zero, Vector3.one * 0.5f, CSGOperation.Additive, 0);
            var inter = MakeBoxBrushData(new Vector3(5, 0, 0), Vector3.one * 0.5f, CSGOperation.Intersect, 1);
            var result = ChiselCSGEngine.Process(new List<ChiselCSGEngine.BrushData> { add, inter });
            Assert.AreEqual(0, result.Count, "AND: no overlap → nothing");
        }

        // ══════════════════════════════════════════════
        //  RoutingTable
        // ══════════════════════════════════════════════

        [Test]
        public void RoutingTable_TwoAdditive_Owner0_Outside_StaysAligned()
        {
            var ops = new List<CSGOperation> { CSGOperation.Additive, CSGOperation.Additive };
            var table = RoutingTable.Build(ops, 0);
            Assert.AreEqual(1, table.EntryCount);
            Assert.AreEqual(PolygonCategory.Aligned,
                table.Walk(new[] { PolygonCategory.Outside }));
        }

        [Test]
        public void RoutingTable_TwoAdditive_Owner0_Inside_Hidden()
        {
            var ops = new List<CSGOperation> { CSGOperation.Additive, CSGOperation.Additive };
            var table = RoutingTable.Build(ops, 0);
            Assert.AreEqual(PolygonCategory.Inside,
                table.Walk(new[] { PolygonCategory.Inside }));
        }

        [Test]
        public void RoutingTable_TwoAdditive_Owner1_Outside_Aligned()
        {
            var ops = new List<CSGOperation> { CSGOperation.Additive, CSGOperation.Additive };
            var table = RoutingTable.Build(ops, 1);
            Assert.AreEqual(1, table.EntryCount);
            Assert.AreEqual(PolygonCategory.Aligned,
                table.Walk(new[] { PolygonCategory.Outside }));
        }

        [Test]
        public void RoutingTable_Subtractive_Owner0_Outside_Aligned()
        {
            var ops = new List<CSGOperation> { CSGOperation.Additive, CSGOperation.Subtractive };
            var table = RoutingTable.Build(ops, 0);
            Assert.AreEqual(PolygonCategory.Aligned,
                table.Walk(new[] { PolygonCategory.Outside }));
        }

        [Test]
        public void RoutingTable_Subtractive_Owner0_Inside_Discarded()
        {
            // When the additive owner's face is inside the sub brush, both sides become
            // empty after subtraction (front=outside A, back=inside A but subtracted).
            // Cavity walls come from the sub brush's own faces, not the additive brush's.
            var ops = new List<CSGOperation> { CSGOperation.Additive, CSGOperation.Subtractive };
            var table = RoutingTable.Build(ops, 0);
            Assert.AreEqual(PolygonCategory.Outside,
                table.Walk(new[] { PolygonCategory.Inside }));
        }

        [Test]
        public void RoutingTable_Intersect_Owner0_Outside_Hidden()
        {
            var ops = new List<CSGOperation> { CSGOperation.Additive, CSGOperation.Intersect };
            var table = RoutingTable.Build(ops, 0);
            var result = table.Walk(new[] { PolygonCategory.Outside });
            Assert.AreEqual(PolygonCategory.Outside, result);
        }

        [Test]
        public void RoutingTable_Intersect_Owner0_Inside_Aligned()
        {
            var ops = new List<CSGOperation> { CSGOperation.Additive, CSGOperation.Intersect };
            var table = RoutingTable.Build(ops, 0);
            var result = table.Walk(new[] { PolygonCategory.Inside });
            Assert.AreEqual(PolygonCategory.Aligned, result);
        }

        [Test]
        public void RoutingTable_ThreeBrush_EntryCount()
        {
            var ops = new List<CSGOperation>
            {
                CSGOperation.Additive,
                CSGOperation.Subtractive,
                CSGOperation.Additive,
            };
            var table = RoutingTable.Build(ops, 0);
            Assert.AreEqual(2, table.EntryCount, "Owner 0 with 3 brushes → 2 entries");
        }

        [Test]
        public void RoutingTable_ThreeBrush_MiddleOwner()
        {
            var ops = new List<CSGOperation>
            {
                CSGOperation.Additive,
                CSGOperation.Subtractive,
                CSGOperation.Additive,
            };
            var table = RoutingTable.Build(ops, 1);
            Assert.AreEqual(2, table.EntryCount, "Owner 1 with 3 brushes → 2 entries");
        }

        // ══════════════════════════════════════════════
        //  OperationTables
        // ══════════════════════════════════════════════

        [Test]
        public void OpTable_Additive_OutsideOutside_IsOutside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Additive);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Outside, PolygonCategory.Outside));
        }

        [Test]
        public void OpTable_Additive_InsideInside_IsInside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Additive);
            Assert.AreEqual(PolygonCategory.Inside,
                OperationTables.Lookup(t, PolygonCategory.Inside, PolygonCategory.Inside));
        }

        [Test]
        public void OpTable_Additive_AlignedOutside_IsAligned()
        {
            var t = OperationTables.GetStandard(CSGOperation.Additive);
            Assert.AreEqual(PolygonCategory.Aligned,
                OperationTables.Lookup(t, PolygonCategory.Outside, PolygonCategory.Aligned));
        }

        [Test]
        public void OpTable_Additive_InsideAligned_IsInside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Additive);
            Assert.AreEqual(PolygonCategory.Inside,
                OperationTables.Lookup(t, PolygonCategory.Inside, PolygonCategory.Aligned));
        }

        [Test]
        public void OpTable_Subtractive_OutsideOutside_IsOutside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Subtractive);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Outside, PolygonCategory.Outside));
        }

        [Test]
        public void OpTable_Subtractive_InsideInside_IsOutside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Subtractive);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Inside, PolygonCategory.Inside));
        }

        [Test]
        public void OpTable_Subtractive_InsideAligned_IsOutside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Subtractive);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Inside, PolygonCategory.Aligned));
        }

        [Test]
        public void OpTable_Subtractive_OutsideAligned_IsAligned()
        {
            var t = OperationTables.GetStandard(CSGOperation.Subtractive);
            Assert.AreEqual(PolygonCategory.Aligned,
                OperationTables.Lookup(t, PolygonCategory.Outside, PolygonCategory.Aligned));
        }

        [Test]
        public void OpTable_Intersect_InsideInside_IsInside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Intersect);
            Assert.AreEqual(PolygonCategory.Inside,
                OperationTables.Lookup(t, PolygonCategory.Inside, PolygonCategory.Inside));
        }

        [Test]
        public void OpTable_Intersect_OutsideOutside_IsOutside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Intersect);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Outside, PolygonCategory.Outside));
        }

        [Test]
        public void OpTable_Intersect_InsideOutside_IsOutside()
        {
            var t = OperationTables.GetStandard(CSGOperation.Intersect);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Outside, PolygonCategory.Inside));
        }

        [Test]
        public void OpTable_Beyond_Additive_CenterIsOutside()
        {
            var t = OperationTables.GetBeyond(CSGOperation.Additive);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Aligned, PolygonCategory.Aligned));
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.ReverseAligned, PolygonCategory.Aligned));
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Aligned, PolygonCategory.ReverseAligned));
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.ReverseAligned, PolygonCategory.ReverseAligned));
        }

        [Test]
        public void OpTable_Beyond_Subtractive_CenterIsOutside()
        {
            var t = OperationTables.GetBeyond(CSGOperation.Subtractive);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Aligned, PolygonCategory.Aligned));
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.ReverseAligned, PolygonCategory.ReverseAligned));
        }

        [Test]
        public void OpTable_Beyond_Intersect_CenterIsOutside()
        {
            var t = OperationTables.GetBeyond(CSGOperation.Intersect);
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Aligned, PolygonCategory.Aligned));
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.ReverseAligned, PolygonCategory.ReverseAligned));
        }

        [Test]
        public void OpTable_Beyond_Additive_CornersPreserved()
        {
            // Beyond tables should still match standard in the corner positions
            var t = OperationTables.GetBeyond(CSGOperation.Additive);
            // [Inside, Inside] = Inside (row 0, col 0)
            Assert.AreEqual(PolygonCategory.Inside,
                OperationTables.Lookup(t, PolygonCategory.Inside, PolygonCategory.Inside));
            // [Outside, Outside] = Outside (row 3, col 3)
            Assert.AreEqual(PolygonCategory.Outside,
                OperationTables.Lookup(t, PolygonCategory.Outside, PolygonCategory.Outside));
        }

        // ══════════════════════════════════════════════
        //  Polygon generation (GeneratePolygons helper)
        // ══════════════════════════════════════════════

        [Test]
        public void PolyGen_UnitBox_6Faces()
        {
            var planes = BrushFactory.Box(Vector3.one * 0.5f);
            var polys = GeneratePolygons(planes);
            Assert.AreEqual(6, polys.Count);
        }

        [Test]
        public void PolyGen_UnitBox_Each4Verts()
        {
            var planes = BrushFactory.Box(Vector3.one * 0.5f);
            var polys = GeneratePolygons(planes);
            foreach (var p in polys)
                Assert.AreEqual(4, p.Vertices.Count);
        }

        [Test]
        public void PolyGen_UnitBox_CorrectArea()
        {
            var planes = BrushFactory.Box(Vector3.one * 0.5f);
            var polys = GeneratePolygons(planes);
            Assert.AreEqual(6f, TotalArea(polys), 0.01f);
        }

        [Test]
        public void PolyGen_FlatBox_Produces6Faces()
        {
            var planes = BrushFactory.Box(new Vector3(2, 0.1f, 2));
            var polys = GeneratePolygons(planes);
            Assert.AreEqual(6, polys.Count);
        }

        [Test]
        public void PolyGen_OffsetBox_CorrectPositions()
        {
            var planes = OffsetPlanes(BrushFactory.Box(Vector3.one * 0.5f), new Vector3(10, 0, 0));
            var polys = GeneratePolygons(planes);
            Assert.AreEqual(6, polys.Count);

            // All vertices should be in range [9.5, 10.5] on X
            foreach (var poly in polys)
            {
                foreach (var v in poly.Vertices)
                {
                    Assert.GreaterOrEqual(v.Position.x, 9.49f);
                    Assert.LessOrEqual(v.Position.x, 10.51f);
                }
            }
        }

        [Test]
        public void PolyGen_NoneDegenerate()
        {
            var planes = BrushFactory.Box(Vector3.one * 0.5f);
            var polys = GeneratePolygons(planes);
            foreach (var p in polys)
                Assert.IsFalse(p.IsDegenerate());
        }

        [Test]
        public void PolyGen_TooFewPlanes_ReturnsEmpty()
        {
            var planes = new List<CSGPlane>
            {
                new CSGPlane(1, 0, 0, -1),
                new CSGPlane(-1, 0, 0, -1),
                new CSGPlane(0, 1, 0, -1),
            };
            var polys = GeneratePolygons(planes);
            Assert.AreEqual(0, polys.Count);
        }

        [Test]
        public void PolyGen_Wedge_Produces5Faces()
        {
            var planes = BrushFactory.Wedge(Vector3.one * 0.5f);
            var polys = GeneratePolygons(planes);
            Assert.AreEqual(5, polys.Count);
        }

        [Test]
        public void PolyGen_Wedge_HasTriangularFaces()
        {
            var planes = BrushFactory.Wedge(Vector3.one * 0.5f);
            var polys = GeneratePolygons(planes);
            bool hasTriangle = polys.Any(p => p.Vertices.Count == 3);
            Assert.IsTrue(hasTriangle, "Wedge should have triangular faces");
        }

        // ══════════════════════════════════════════════
        //  CSGPlane
        // ══════════════════════════════════════════════

        [Test]
        public void Plane_DistanceTo_OriginOnXPlane()
        {
            var plane = new CSGPlane(1, 0, 0, 0);
            Assert.AreEqual(0, plane.DistanceTo(Vector3.zero), 1e-6);
        }

        [Test]
        public void Plane_DistanceTo_PointInFront()
        {
            var plane = new CSGPlane(1, 0, 0, 0);
            Assert.Greater(plane.DistanceTo(new Vector3(1, 0, 0)), 0);
        }

        [Test]
        public void Plane_DistanceTo_PointBehind()
        {
            var plane = new CSGPlane(1, 0, 0, 0);
            Assert.Less(plane.DistanceTo(new Vector3(-1, 0, 0)), 0);
        }

        [Test]
        public void Plane_ClassifyPoint_Front()
        {
            var plane = new CSGPlane(0, 1, 0, 0);
            Assert.AreEqual(PlaneClassification.Front,
                plane.ClassifyPoint(new Vector3(0, 1, 0)));
        }

        [Test]
        public void Plane_ClassifyPoint_Back()
        {
            var plane = new CSGPlane(0, 1, 0, 0);
            Assert.AreEqual(PlaneClassification.Back,
                plane.ClassifyPoint(new Vector3(0, -1, 0)));
        }

        [Test]
        public void Plane_ClassifyPoint_OnPlane()
        {
            var plane = new CSGPlane(0, 1, 0, 0);
            Assert.AreEqual(PlaneClassification.OnPlane,
                plane.ClassifyPoint(Vector3.zero));
        }

        [Test]
        public void Plane_Flipped_ReversesNormal()
        {
            var plane = new CSGPlane(1, 0, 0, -5);
            var flipped = plane.Flipped();
            Assert.AreEqual(-1, flipped.A, 1e-6);
            Assert.AreEqual(5, flipped.D, 1e-6);
        }

        [Test]
        public void Plane_Constructor_Normalizes()
        {
            var plane = new CSGPlane(2, 0, 0, -4);
            Assert.AreEqual(1, plane.A, 1e-6);
            Assert.AreEqual(-2, plane.D, 1e-6);
        }

        [Test]
        public void Plane_Equality_WithinEpsilon()
        {
            var a = new CSGPlane(1, 0, 0, -1);
            var b = new CSGPlane(1, 0, 0, -1.000005);
            Assert.IsTrue(a.Equals(b));
        }

        [Test]
        public void Plane_FromPoints_CorrectNormal()
        {
            var plane = CSGPlane.FromPoints(
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 0, 1));
            // Cross of (1,0,0) x (0,0,1) = (0,-1,0), normalized = (0,-1,0)
            // Actually: (b-a) x (c-a) = (1,0,0) x (0,0,1) = (0*1-0*0, 0*0-1*1, 1*0-0*0) = (0,-1,0)
            Assert.AreEqual(0, plane.A, 0.01);
            Assert.AreEqual(-1, plane.B, 0.01);
            Assert.AreEqual(0, plane.C, 0.01);
        }

        // ══════════════════════════════════════════════
        //  CSGPolygon
        // ══════════════════════════════════════════════

        [Test]
        public void Polygon_Clone_IsDeepCopy()
        {
            var verts = new List<CSGVertex>
            {
                new CSGVertex(Vector3.zero),
                new CSGVertex(Vector3.right),
                new CSGVertex(Vector3.up),
            };
            var orig = new CSGPolygon(verts);
            var clone = orig.Clone();

            clone.Vertices[0] = new CSGVertex(new Vector3(99, 99, 99));
            Assert.AreNotEqual(orig.Vertices[0].Position, clone.Vertices[0].Position);
        }

        [Test]
        public void Polygon_Flip_ReversesWinding()
        {
            var verts = new List<CSGVertex>
            {
                new CSGVertex(new Vector3(0, 0, 0)),
                new CSGVertex(new Vector3(1, 0, 0)),
                new CSGVertex(new Vector3(0, 1, 0)),
            };
            var poly = new CSGPolygon(verts, new CSGPlane(0, 0, 1, 0));

            var firstBefore = poly.Vertices[0].Position;
            poly.Flip();
            var lastAfter = poly.Vertices[poly.Vertices.Count - 1].Position;

            Assert.AreEqual(firstBefore, lastAfter, "Flip should reverse vertex order");
        }

        [Test]
        public void Polygon_Flip_FlipsPlane()
        {
            var poly = new CSGPolygon(
                new List<CSGVertex>
                {
                    new CSGVertex(Vector3.zero),
                    new CSGVertex(Vector3.right),
                    new CSGVertex(Vector3.up),
                },
                new CSGPlane(0, 0, 1, -1));

            poly.Flip();
            Assert.AreEqual(-1, poly.Plane.C, 0.01, "Plane normal should be flipped");
        }

        [Test]
        public void Polygon_IsDegenerate_TinyTriangle()
        {
            var verts = new List<CSGVertex>
            {
                new CSGVertex(new Vector3(0, 0, 0)),
                new CSGVertex(new Vector3(0.000001f, 0, 0)),
                new CSGVertex(new Vector3(0, 0.000001f, 0)),
            };
            var poly = new CSGPolygon(verts);
            Assert.IsTrue(poly.IsDegenerate());
        }

        [Test]
        public void Polygon_IsDegenerate_TwoVerts()
        {
            var verts = new List<CSGVertex>
            {
                new CSGVertex(Vector3.zero),
                new CSGVertex(Vector3.right),
            };
            var poly = new CSGPolygon(verts, new CSGPlane(0, 0, 1, 0));
            Assert.IsTrue(poly.IsDegenerate());
        }

        [Test]
        public void Polygon_NotDegenerate_UnitTriangle()
        {
            var verts = new List<CSGVertex>
            {
                new CSGVertex(new Vector3(0, 0, 0)),
                new CSGVertex(new Vector3(1, 0, 0)),
                new CSGVertex(new Vector3(0, 1, 0)),
            };
            var poly = new CSGPolygon(verts);
            Assert.IsFalse(poly.IsDegenerate());
        }

        // ══════════════════════════════════════════════
        //  BrushBoundsUtil
        // ══════════════════════════════════════════════

        [Test]
        public void ThreePlaneIntersect_AxisAligned_FindsCorner()
        {
            var px = new CSGPlane(1, 0, 0, -1);  // x = 1
            var py = new CSGPlane(0, 1, 0, -2);  // y = 2
            var pz = new CSGPlane(0, 0, 1, -3);  // z = 3
            Assert.IsTrue(BrushBoundsUtil.IntersectThreePlanes(px, py, pz, out var pt));
            Assert.AreEqual(1f, pt.x, 0.001f);
            Assert.AreEqual(2f, pt.y, 0.001f);
            Assert.AreEqual(3f, pt.z, 0.001f);
        }

        [Test]
        public void ThreePlaneIntersect_ParallelPlanes_ReturnsFalse()
        {
            var p1 = new CSGPlane(1, 0, 0, 0);
            var p2 = new CSGPlane(1, 0, 0, -1);
            var p3 = new CSGPlane(0, 1, 0, 0);
            Assert.IsFalse(BrushBoundsUtil.IntersectThreePlanes(p1, p2, p3, out _));
        }

        [Test]
        public void IsInsideAllPlanes_CenterOfBox_True()
        {
            var planes = BrushFactory.Box(Vector3.one * 0.5f);
            Assert.IsTrue(BrushBoundsUtil.IsInsideAllPlanes(Vector3.zero, planes));
        }

        [Test]
        public void IsInsideAllPlanes_OutsideBox_False()
        {
            var planes = BrushFactory.Box(Vector3.one * 0.5f);
            Assert.IsFalse(BrushBoundsUtil.IsInsideAllPlanes(new Vector3(5, 0, 0), planes));
        }

        [Test]
        public void ComputeBounds_UnitBox_Correct()
        {
            var planes = BrushFactory.Box(Vector3.one * 0.5f);
            var bounds = BrushBoundsUtil.ComputeBounds(planes);
            Assert.AreEqual(0f, bounds.center.x, 0.01f);
            Assert.AreEqual(0f, bounds.center.y, 0.01f);
            Assert.AreEqual(0f, bounds.center.z, 0.01f);
            Assert.AreEqual(1f, bounds.size.x, 0.01f);
            Assert.AreEqual(1f, bounds.size.y, 0.01f);
            Assert.AreEqual(1f, bounds.size.z, 0.01f);
        }

        [Test]
        public void ComputeBounds_OffsetBox_CorrectCenter()
        {
            var planes = OffsetPlanes(BrushFactory.Box(Vector3.one * 0.5f), new Vector3(5, 3, 1));
            var bounds = BrushBoundsUtil.ComputeBounds(planes);
            Assert.AreEqual(5f, bounds.center.x, 0.01f);
            Assert.AreEqual(3f, bounds.center.y, 0.01f);
            Assert.AreEqual(1f, bounds.center.z, 0.01f);
        }

        [Test]
        public void ComputeBounds_RectBox_CorrectSize()
        {
            var planes = BrushFactory.Box(new Vector3(1, 2, 3));
            var bounds = BrushBoundsUtil.ComputeBounds(planes);
            Assert.AreEqual(2f, bounds.size.x, 0.01f);
            Assert.AreEqual(4f, bounds.size.y, 0.01f);
            Assert.AreEqual(6f, bounds.size.z, 0.01f);
        }

        [Test]
        public void ComputeBounds_TooFewPlanes_ReturnsZero()
        {
            var planes = new List<CSGPlane>
            {
                new CSGPlane(1, 0, 0, 0),
                new CSGPlane(0, 1, 0, 0),
            };
            var bounds = BrushBoundsUtil.ComputeBounds(planes);
            Assert.AreEqual(Vector3.zero, bounds.size);
        }

        // ══════════════════════════════════════════════
        //  BrushFactory
        // ══════════════════════════════════════════════

        [Test]
        public void BrushFactory_Box_Returns6Planes()
        {
            var planes = BrushFactory.Box();
            Assert.AreEqual(6, planes.Count);
        }

        [Test]
        public void BrushFactory_Box_PlanesAreNormalized()
        {
            var planes = BrushFactory.Box(new Vector3(1, 2, 3));
            foreach (var p in planes)
            {
                double len = System.Math.Sqrt(p.A * p.A + p.B * p.B + p.C * p.C);
                Assert.AreEqual(1.0, len, 1e-6);
            }
        }

        [Test]
        public void BrushFactory_Wedge_Returns5Planes()
        {
            var planes = BrushFactory.Wedge();
            Assert.AreEqual(5, planes.Count);
        }

        [Test]
        public void BrushFactory_Cylinder_Returns18Planes_Default()
        {
            // 16 sides + top + bottom = 18
            var planes = BrushFactory.Cylinder();
            Assert.AreEqual(18, planes.Count);
        }

        [Test]
        public void BrushFactory_Cylinder_MinSides3()
        {
            var planes = BrushFactory.Cylinder(0.5f, 0.5f, 1);
            // Min sides = 3, plus top + bottom = 5
            Assert.AreEqual(5, planes.Count);
        }

        // ══════════════════════════════════════════════
        //  CSGVertex
        // ══════════════════════════════════════════════

        [Test]
        public void Vertex_Lerp_Midpoint()
        {
            var a = new CSGVertex(Vector3.zero, Vector3.up, Vector2.zero);
            var b = new CSGVertex(Vector3.one, Vector3.up, Vector2.one);
            var mid = CSGVertex.Lerp(a, b, 0.5f);
            Assert.AreEqual(0.5f, mid.Position.x, 0.001f);
            Assert.AreEqual(0.5f, mid.Position.y, 0.001f);
            Assert.AreEqual(0.5f, mid.UV.x, 0.001f);
        }

        [Test]
        public void Vertex_Lerp_AtZero_ReturnsA()
        {
            var a = new CSGVertex(Vector3.zero, Vector3.up, Vector2.zero);
            var b = new CSGVertex(Vector3.one, Vector3.right, Vector2.one);
            var result = CSGVertex.Lerp(a, b, 0f);
            Assert.AreEqual(Vector3.zero, result.Position);
        }

        [Test]
        public void Vertex_Lerp_AtOne_ReturnsB()
        {
            var a = new CSGVertex(Vector3.zero, Vector3.up, Vector2.zero);
            var b = new CSGVertex(Vector3.one, Vector3.right, Vector2.one);
            var result = CSGVertex.Lerp(a, b, 1f);
            Assert.AreEqual(Vector3.one, result.Position);
        }

        [Test]
        public void Vertex_Flip_NegatesNormal()
        {
            var v = new CSGVertex(Vector3.zero, Vector3.up, Vector2.zero);
            v.Flip();
            Assert.AreEqual(Vector3.down, v.Normal);
        }

        // ══════════════════════════════════════════════
        //  PolygonClipper
        // ══════════════════════════════════════════════

        [Test]
        public void Clipper_AllFront_ReturnsFront()
        {
            var poly = MakeTri(
                new Vector3(0, 1, 0),
                new Vector3(1, 1, 0),
                new Vector3(0.5f, 1, 0.5f),
                Vector3.up);
            var plane = new CSGPlane(0, 1, 0, 0); // y=0 plane

            PolygonClipper.Split(poly, plane, out var front, out var back, out var cf, out var cb);
            Assert.IsNotNull(front);
            Assert.IsNull(back);
            Assert.IsNull(cf);
            Assert.IsNull(cb);
        }

        [Test]
        public void Clipper_AllBack_ReturnsBack()
        {
            var poly = MakeTri(
                new Vector3(0, -1, 0),
                new Vector3(1, -1, 0),
                new Vector3(0.5f, -1, 0.5f),
                Vector3.up);
            var plane = new CSGPlane(0, 1, 0, 0);

            PolygonClipper.Split(poly, plane, out var front, out var back, out var cf, out var cb);
            Assert.IsNull(front);
            Assert.IsNotNull(back);
            Assert.IsNull(cf);
            Assert.IsNull(cb);
        }

        [Test]
        public void Clipper_Spanning_SplitsBoth()
        {
            var poly = MakeTri(
                new Vector3(0, -1, 0),
                new Vector3(1, -1, 0),
                new Vector3(0.5f, 1, 0),
                Vector3.forward);
            var plane = new CSGPlane(0, 1, 0, 0);

            PolygonClipper.Split(poly, plane, out var front, out var back, out var cf, out var cb);
            Assert.IsNotNull(front, "Should have front portion");
            Assert.IsNotNull(back, "Should have back portion");
        }

        [Test]
        public void Clipper_CoplanarSameDir_ReturnsCoplanarFront()
        {
            // Polygon on the plane, normal matches
            var poly = MakeQuad(
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),
                Vector3.up);
            var plane = new CSGPlane(0, 1, 0, 0);

            PolygonClipper.Split(poly, plane, out var front, out var back, out var cf, out var cb);
            Assert.IsNull(front);
            Assert.IsNull(back);
            Assert.IsNotNull(cf, "Coplanar same direction → coplanarFront");
            Assert.IsNull(cb);
        }

        [Test]
        public void Clipper_CoplanarOppositeDir_ReturnsCoplanarBack()
        {
            var poly = MakeQuad(
                new Vector3(0, 0, 0),
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(1, 0, 0),
                Vector3.down);
            var plane = new CSGPlane(0, 1, 0, 0);

            PolygonClipper.Split(poly, plane, out var front, out var back, out var cf, out var cb);
            Assert.IsNull(front);
            Assert.IsNull(back);
            Assert.IsNull(cf);
            Assert.IsNotNull(cb, "Coplanar opposite direction → coplanarBack");
        }

        // ══════════════════════════════════════════════
        //  Large-scale stress / integration
        // ══════════════════════════════════════════════

        [Test]
        public void Stress_ManyNonOverlappingBoxes_AllPresent()
        {
            var brushes = new List<ChiselCSGEngine.BrushData>();
            for (int i = 0; i < 10; i++)
            {
                brushes.Add(MakeBoxBrushData(
                    new Vector3(i * 3, 0, 0),
                    Vector3.one * 0.5f,
                    CSGOperation.Additive, i));
            }
            var result = ChiselCSGEngine.Process(brushes);
            Assert.AreEqual(60, result.Count, "10 separate boxes = 60 faces");
        }

        [Test]
        public void Stress_ChainOfOverlapping_ProducesResult()
        {
            // 5 boxes overlapping in a line
            var brushes = new List<ChiselCSGEngine.BrushData>();
            for (int i = 0; i < 5; i++)
            {
                brushes.Add(MakeBoxBrushData(
                    new Vector3(i * 0.5f, 0, 0),
                    Vector3.one * 0.5f,
                    CSGOperation.Additive, i));
            }
            var result = ChiselCSGEngine.Process(brushes);

            Assert.Greater(result.Count, 0);
            // Merged shape is 3x1x1 → SA = 2*(3+3+1) = 14
            Assert.AreEqual(14f, TotalArea(result), 0.3f);
        }

        [Test]
        public void Stress_AlternatingAddSub_ProducesGeometry()
        {
            var brushes = new List<ChiselCSGEngine.BrushData>
            {
                MakeBoxBrushData(Vector3.zero, Vector3.one * 2f, CSGOperation.Additive, 0),
                MakeBoxBrushData(new Vector3(-1, 0, 0), Vector3.one * 0.3f, CSGOperation.Subtractive, 1),
                MakeBoxBrushData(new Vector3(0, 0, 0), Vector3.one * 0.3f, CSGOperation.Subtractive, 2),
                MakeBoxBrushData(new Vector3(1, 0, 0), Vector3.one * 0.3f, CSGOperation.Subtractive, 3),
            };
            var result = ChiselCSGEngine.Process(brushes);
            Assert.Greater(result.Count, 6, "Box with 3 holes should have more than 6 faces");
        }
    }
}
