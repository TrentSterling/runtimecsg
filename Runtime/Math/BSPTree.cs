using System.Collections.Generic;

namespace RuntimeCSG
{
    /// <summary>
    /// BSP tree wrapper providing CSG boolean operations.
    /// Classic BSP-based CSG as used in Quake/Hammer editors.
    /// </summary>
    public class BSPTree
    {
        public BSPNode Root;

        BSPTree() { }

        /// <summary>
        /// Build a BSP tree from a list of polygons.
        /// </summary>
        public static BSPTree FromPolygons(List<CSGPolygon> polygons)
        {
            var tree = new BSPTree();
            tree.Root = new BSPNode(polygons);
            return tree;
        }

        /// <summary>
        /// Collect all polygons from this tree.
        /// </summary>
        public List<CSGPolygon> ToPolygons()
        {
            return Root?.AllPolygons() ?? new List<CSGPolygon>();
        }

        public BSPTree Clone()
        {
            var tree = new BSPTree();
            tree.Root = Root?.Clone();
            return tree;
        }

        /// <summary>
        /// CSG Union: A ∪ B
        /// Return a tree containing geometry from both A and B,
        /// removing interior faces.
        /// </summary>
        public static BSPTree Union(BSPTree a, BSPTree b)
        {
            var aNode = a.Root.Clone();
            var bNode = b.Root.Clone();

            aNode.ClipTo(bNode);
            bNode.ClipTo(aNode);
            bNode.Invert();
            bNode.ClipTo(aNode);
            bNode.Invert();
            aNode.Build(bNode.AllPolygons());

            var tree = new BSPTree();
            tree.Root = aNode;
            return tree;
        }

        /// <summary>
        /// CSG Subtract: A - B
        /// Return a tree containing geometry of A with B carved out.
        /// </summary>
        public static BSPTree Subtract(BSPTree a, BSPTree b)
        {
            var aNode = a.Root.Clone();
            var bNode = b.Root.Clone();

            aNode.Invert();
            aNode.ClipTo(bNode);
            bNode.ClipTo(aNode);
            bNode.Invert();
            bNode.ClipTo(aNode);
            bNode.Invert();
            aNode.Build(bNode.AllPolygons());
            aNode.Invert();

            var tree = new BSPTree();
            tree.Root = aNode;
            return tree;
        }

        /// <summary>
        /// CSG Intersect: A ∩ B
        /// Return a tree containing only geometry that is in both A and B.
        /// </summary>
        public static BSPTree Intersect(BSPTree a, BSPTree b)
        {
            var aNode = a.Root.Clone();
            var bNode = b.Root.Clone();

            aNode.Invert();
            bNode.ClipTo(aNode);
            bNode.Invert();
            aNode.ClipTo(bNode);
            bNode.ClipTo(aNode);
            aNode.Build(bNode.AllPolygons());
            aNode.Invert();

            var tree = new BSPTree();
            tree.Root = aNode;
            return tree;
        }

        /// <summary>
        /// Apply a CSG operation between two trees.
        /// </summary>
        public static BSPTree Apply(BSPTree a, BSPTree b, CSGOperation operation)
        {
            switch (operation)
            {
                case CSGOperation.Additive:
                    return Union(a, b);
                case CSGOperation.Subtractive:
                    return Subtract(a, b);
                case CSGOperation.Intersect:
                    return Intersect(a, b);
                default:
                    return Union(a, b);
            }
        }
    }
}
