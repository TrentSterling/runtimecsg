using System.Collections.Generic;

namespace RuntimeCSG
{
    /// <summary>
    /// BSP tree node. Each node has a splitting plane, coplanar polygons,
    /// and front/back children.
    /// </summary>
    public class BSPNode
    {
        const int MaxDepth = 256;

        public CSGPlane Plane;
        public List<CSGPolygon> Polygons = new List<CSGPolygon>();
        public BSPNode Front;
        public BSPNode Back;

        public BSPNode()
        {
        }

        public BSPNode(List<CSGPolygon> polygons)
        {
            Build(polygons, 0);
        }

        public BSPNode Clone()
        {
            var node = new BSPNode();
            node.Plane = Plane;
            node.Polygons = new List<CSGPolygon>(Polygons.Count);
            for (int i = 0; i < Polygons.Count; i++)
                node.Polygons.Add(Polygons[i].Clone());
            node.Front = Front?.Clone();
            node.Back = Back?.Clone();
            return node;
        }

        /// <summary>
        /// Flip all polygons and swap front/back subtrees.
        /// </summary>
        public void Invert()
        {
            for (int i = 0; i < Polygons.Count; i++)
                Polygons[i].Flip();
            Plane = Plane.Flipped();
            Front?.Invert();
            Back?.Invert();
            var temp = Front;
            Front = Back;
            Back = temp;
        }

        /// <summary>
        /// Remove all polygons in this BSP tree that are inside the given BSP tree.
        /// </summary>
        public List<CSGPolygon> ClipPolygons(List<CSGPolygon> polygons)
        {
            if (Polygons.Count == 0 && Front == null && Back == null)
                return new List<CSGPolygon>(polygons);

            var front = new List<CSGPolygon>();
            var back = new List<CSGPolygon>();

            for (int i = 0; i < polygons.Count; i++)
            {
                PolygonClipper.Split(polygons[i], Plane,
                    out var f, out var b, out var cf, out var cb);

                if (f != null) front.Add(f);
                if (cf != null) front.Add(cf);
                if (b != null) back.Add(b);
                if (cb != null) back.Add(cb);
            }

            front = Front != null ? Front.ClipPolygons(front) : front;
            back = Back != null ? Back.ClipPolygons(back) : new List<CSGPolygon>();

            front.AddRange(back);
            return front;
        }

        /// <summary>
        /// Remove all polygons in this tree that are inside the other tree.
        /// </summary>
        public void ClipTo(BSPNode other)
        {
            Polygons = other.ClipPolygons(Polygons);
            Front?.ClipTo(other);
            Back?.ClipTo(other);
        }

        /// <summary>
        /// Collect all polygons from this tree.
        /// </summary>
        public List<CSGPolygon> AllPolygons()
        {
            var result = new List<CSGPolygon>(Polygons);
            if (Front != null) result.AddRange(Front.AllPolygons());
            if (Back != null) result.AddRange(Back.AllPolygons());
            return result;
        }

        /// <summary>
        /// Build BSP tree from a list of polygons.
        /// </summary>
        public void Build(List<CSGPolygon> polygons)
        {
            Build(polygons, 0);
        }

        void Build(List<CSGPolygon> polygons, int depth)
        {
            if (polygons == null || polygons.Count == 0)
                return;

            // When this is a fresh node, the first polygon defines the plane
            // and is always treated as coplanar (avoids precision issues where
            // a polygon's vertices drift from its own plane after clipping).
            bool isNewNode = Polygons.Count == 0;
            int startIdx = 0;
            if (isNewNode)
            {
                Plane = polygons[0].Plane;
                Polygons.Add(polygons[0]);
                startIdx = 1;
            }

            var front = new List<CSGPolygon>();
            var back = new List<CSGPolygon>();

            for (int i = startIdx; i < polygons.Count; i++)
            {
                PolygonClipper.Split(polygons[i], Plane,
                    out var f, out var b, out var cf, out var cb);

                if (cf != null) Polygons.Add(cf);
                if (cb != null) Polygons.Add(cb);
                if (f != null) front.Add(f);
                if (b != null) back.Add(b);
            }

            // Safety: cap recursion depth to prevent stack overflow
            if (depth >= MaxDepth)
            {
                Polygons.AddRange(front);
                Polygons.AddRange(back);
                return;
            }

            if (front.Count > 0)
            {
                if (Front == null) Front = new BSPNode();
                Front.Build(front, depth + 1);
            }
            if (back.Count > 0)
            {
                if (Back == null) Back = new BSPNode();
                Back.Build(back, depth + 1);
            }
        }
    }
}
