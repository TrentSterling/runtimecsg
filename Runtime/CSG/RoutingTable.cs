using System.Collections.Generic;

namespace RuntimeCSG
{
    /// <summary>
    /// Per-brush routing table that maps polygon categories through a chain of CSG operations.
    ///
    /// Built from the ordered brush chain (excluding the owner brush). Each entry corresponds
    /// to one non-owner brush. The table is walked by providing the polygon's category relative
    /// to each non-owner brush, producing a final category that determines visibility.
    ///
    /// The initial walk state is always Aligned (index 1), because the owner brush's polygons
    /// are by definition on its own surface facing outward.
    /// </summary>
    public class RoutingTable
    {
        // _entries[entryIndex] is a flat array: [state * 4 + category] → newState
        readonly int[][] _entries;
        readonly PolygonCategory[] _finalCategories;

        RoutingTable(int[][] entries, PolygonCategory[] finalCategories)
        {
            _entries = entries;
            _finalCategories = finalCategories;
        }

        /// <summary>
        /// Build a routing table for the given brush within an ordered chain of operations.
        /// </summary>
        /// <param name="operations">Operations for all brushes in the chain.</param>
        /// <param name="ownerIndex">Index of the brush this table belongs to.</param>
        public static RoutingTable Build(List<CSGOperation> operations, int ownerIndex)
        {
            int brushCount = operations.Count;

            // Initial states: identity mapping (4 states = 4 categories)
            var currentStates = new PolygonCategory[]
            {
                PolygonCategory.Inside,
                PolygonCategory.Aligned,
                PolygonCategory.ReverseAligned,
                PolygonCategory.Outside,
            };

            var entries = new List<int[]>();

            for (int b = 0; b < brushCount; b++)
            {
                if (b == ownerIndex) continue;

                bool beyond = b > ownerIndex;
                int[,] table = beyond
                    ? OperationTables.GetBeyond(operations[b])
                    : OperationTables.GetStandard(operations[b]);

                int stateCount = currentStates.Length;

                // Map (state, category) → result category → compacted new state index
                var resultToIndex = new Dictionary<PolygonCategory, int>();
                var newStates = new List<PolygonCategory>();
                var transitions = new int[stateCount * 4];

                for (int s = 0; s < stateCount; s++)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        var result = OperationTables.Lookup(table, (PolygonCategory)c, currentStates[s]);

                        if (!resultToIndex.TryGetValue(result, out int idx))
                        {
                            idx = newStates.Count;
                            resultToIndex[result] = idx;
                            newStates.Add(result);
                        }
                        transitions[s * 4 + c] = idx;
                    }
                }

                entries.Add(transitions);
                currentStates = newStates.ToArray();
            }

            return new RoutingTable(entries.ToArray(), currentStates);
        }

        /// <summary>
        /// Number of non-owner brush entries in this routing table.
        /// </summary>
        public int EntryCount => _entries.Length;

        /// <summary>
        /// Walk the routing table for a polygon fragment.
        /// Categories array must have one entry per non-owner brush (same order as Build).
        /// Returns the final polygon category.
        /// </summary>
        public PolygonCategory Walk(PolygonCategory[] categoriesPerEntry)
        {
            // Owner brush's polygons are Aligned with themselves (initial state = 1)
            int state = (int)PolygonCategory.Aligned;

            for (int i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];
                int cat = (int)categoriesPerEntry[i];
                int idx = state * 4 + cat;
                state = idx < entry.Length ? entry[idx] : 0;
            }

            if (state < 0 || state >= _finalCategories.Length)
                return PolygonCategory.Outside;
            return _finalCategories[state];
        }

        /// <summary>
        /// Get the final category for a state index (for manual walking).
        /// </summary>
        public PolygonCategory GetFinalCategory(int stateIndex)
        {
            if (stateIndex < 0 || stateIndex >= _finalCategories.Length)
                return PolygonCategory.Outside;
            return _finalCategories[stateIndex];
        }
    }
}
