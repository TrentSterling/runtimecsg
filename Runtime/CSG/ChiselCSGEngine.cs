using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Chisel-style per-brush CSG engine. Processes brushes individually: each brush's
    /// polygons are split against overlapping brushes' planes, then each fragment is
    /// evaluated against the full CSG boolean expression to determine visibility.
    ///
    /// This replaces the monolithic BSP approach with per-brush processing that enables
    /// future incremental updates (only recompute brushes affected by a change).
    /// </summary>
    public static class ChiselCSGEngine
    {
        public struct BrushData
        {
            public List<CSGPolygon> Polygons;
            public List<CSGPlane> WorldPlanes;
            public CSGOperation Operation;
            public int Order;
        }

        /// <summary>
        /// Process all brushes and return the final visible polygons.
        /// Brushes must be sorted by Order before calling.
        /// </summary>
        public static List<CSGPolygon> Process(List<BrushData> brushes)
        {
            if (brushes.Count == 0)
                return new List<CSGPolygon>();

            // Single additive brush: return polygons directly
            if (brushes.Count == 1)
            {
                if (brushes[0].Operation == CSGOperation.Additive)
                    return new List<CSGPolygon>(brushes[0].Polygons);
                return new List<CSGPolygon>();
            }

            int n = brushes.Count;

            // Pre-compute overlap matrix
            var overlaps = new bool[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    bool overlap = BrushPairIntersection.BrushesOverlap(
                        brushes[i].WorldPlanes, brushes[j].WorldPlanes);
                    overlaps[i, j] = overlap;
                    overlaps[j, i] = overlap;
                }
            }

            var result = new List<CSGPolygon>();

            // Process each brush independently
            for (int ownerIdx = 0; ownerIdx < n; ownerIdx++)
            {
                var owner = brushes[ownerIdx];
                if (owner.Polygons.Count == 0) continue;

                // Find overlapping brush indices
                var overlappingIndices = new List<int>();
                for (int j = 0; j < n; j++)
                {
                    if (j != ownerIdx && overlaps[ownerIdx, j])
                        overlappingIndices.Add(j);
                }

                // Collect splitting planes from overlapping brushes
                var splittingPlanes = CollectSplittingPlanes(overlappingIndices, brushes);

                // Process each face polygon
                for (int p = 0; p < owner.Polygons.Count; p++)
                {
                    var fragments = SplitPolygon(owner.Polygons[p], splittingPlanes);

                    for (int f = 0; f < fragments.Count; f++)
                    {
                        var fragment = fragments[f];
                        if (fragment.IsDegenerate()) continue;

                        var visibility = EvaluateFragment(
                            fragment, ownerIdx, brushes, n, overlaps);

                        switch (visibility)
                        {
                            case PolygonCategory.Aligned:
                                result.Add(fragment);
                                break;
                            case PolygonCategory.ReverseAligned:
                                fragment.Flip();
                                result.Add(fragment);
                                break;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Evaluate a fragment's visibility by computing the CSG boolean at points
        /// just in front of and behind the fragment surface.
        ///
        /// Front = outside the owner brush (in normal direction)
        /// Back = inside the owner brush (against normal direction)
        ///
        /// For coplanar cases (fragment Aligned/ReverseAligned with another brush),
        /// the front/back interpretation accounts for which side of that brush the
        /// evaluation point falls on.
        /// </summary>
        static PolygonCategory EvaluateFragment(
            CSGPolygon fragment, int ownerIdx,
            List<BrushData> brushes, int brushCount,
            bool[,] overlaps)
        {
            // Categorize fragment against each non-owner brush.
            // Non-overlapping brushes default to Outside (optimization).
            var categories = new PolygonCategory[brushCount];
            for (int b = 0; b < brushCount; b++)
            {
                if (b == ownerIdx)
                {
                    categories[b] = PolygonCategory.Aligned; // placeholder, handled below
                    continue;
                }
                if (!overlaps[ownerIdx, b])
                {
                    categories[b] = PolygonCategory.Outside;
                    continue;
                }
                categories[b] = BrushPairIntersection.CategorizePolygon(
                    fragment, brushes[b].WorldPlanes);
            }

            // Coplanar tiebreaker: if fragment is Aligned or ReverseAligned with a
            // LATER brush in the chain, that brush "claims" the surface to prevent
            // z-fighting from duplicate coplanar polygons. Discard this fragment.
            for (int b = ownerIdx + 1; b < brushCount; b++)
            {
                if (categories[b] == PolygonCategory.Aligned ||
                    categories[b] == PolygonCategory.ReverseAligned)
                    return PolygonCategory.Outside;
            }

            // Evaluate chain for front (outside owner) and back (inside owner).
            // For each brush, convert category to inside/outside boolean differently
            // for front vs back evaluation.
            var frontInside = new bool[brushCount];
            var backInside = new bool[brushCount];

            for (int b = 0; b < brushCount; b++)
            {
                if (b == ownerIdx)
                {
                    frontInside[b] = false; // front is outside owner
                    backInside[b] = true;   // back is inside owner
                    continue;
                }

                switch (categories[b])
                {
                    case PolygonCategory.Inside:
                        frontInside[b] = true;
                        backInside[b] = true;
                        break;
                    case PolygonCategory.Outside:
                        frontInside[b] = false;
                        backInside[b] = false;
                        break;
                    case PolygonCategory.Aligned:
                        // Fragment normal matches brush plane normal.
                        // Front of fragment = front of brush plane = outside brush
                        // Back of fragment = back of brush plane = inside brush
                        frontInside[b] = false;
                        backInside[b] = true;
                        break;
                    case PolygonCategory.ReverseAligned:
                        // Fragment normal opposes brush plane normal.
                        // Front of fragment = back of brush plane = inside brush
                        // Back of fragment = front of brush plane = outside brush
                        frontInside[b] = true;
                        backInside[b] = false;
                        break;
                }
            }

            bool frontSolid = EvaluateChain(brushes, frontInside, brushCount);
            bool backSolid = EvaluateChain(brushes, backInside, brushCount);

            if (!frontSolid && backSolid)
                return PolygonCategory.Aligned;
            if (frontSolid && !backSolid)
                return PolygonCategory.ReverseAligned;

            return PolygonCategory.Outside; // not on boundary (both solid or both empty)
        }

        /// <summary>
        /// Evaluate the CSG boolean chain: process brushes in order, accumulating
        /// the solid/empty state. Returns true if the final result is "solid".
        /// </summary>
        static bool EvaluateChain(List<BrushData> brushes, bool[] isInside, int brushCount)
        {
            bool solid = false; // start with empty space

            for (int b = 0; b < brushCount; b++)
            {
                switch (brushes[b].Operation)
                {
                    case CSGOperation.Additive:
                        solid = solid || isInside[b];
                        break;
                    case CSGOperation.Subtractive:
                        solid = solid && !isInside[b];
                        break;
                    case CSGOperation.Intersect:
                        solid = solid && isInside[b];
                        break;
                }
            }

            return solid;
        }

        static List<CSGPlane> CollectSplittingPlanes(
            List<int> overlappingIndices, List<BrushData> brushes)
        {
            var planes = new List<CSGPlane>();
            for (int i = 0; i < overlappingIndices.Count; i++)
            {
                var brushPlanes = brushes[overlappingIndices[i]].WorldPlanes;
                for (int p = 0; p < brushPlanes.Count; p++)
                    planes.Add(brushPlanes[p]);
            }
            return planes;
        }

        /// <summary>
        /// Split a polygon against all planes, producing convex fragments.
        /// </summary>
        static List<CSGPolygon> SplitPolygon(CSGPolygon polygon, List<CSGPlane> planes)
        {
            var current = new List<CSGPolygon> { polygon.Clone() };

            for (int i = 0; i < planes.Count; i++)
            {
                var next = new List<CSGPolygon>(current.Count + 1);
                for (int j = 0; j < current.Count; j++)
                {
                    PolygonClipper.Split(current[j], planes[i],
                        out var front, out var back, out var cf, out var cb);

                    if (front != null) next.Add(front);
                    if (back != null) next.Add(back);
                    if (cf != null) next.Add(cf);
                    if (cb != null) next.Add(cb);
                }
                current = next;
                if (current.Count == 0) break;
            }

            return current;
        }
    }
}
