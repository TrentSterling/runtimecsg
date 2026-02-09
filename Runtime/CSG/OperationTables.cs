namespace RuntimeCSG
{
    /// <summary>
    /// 4x4 lookup tables for combining polygon categories across CSG operations.
    /// Row = category relative to brush B, Column = category relative to brush A.
    /// "Beyond" variants replace the center 2x2 (Aligned/ReverseAligned) with Outside
    /// to prevent z-fighting on coplanar faces after the owning brush.
    /// </summary>
    public static class OperationTables
    {
        const int In = (int)PolygonCategory.Inside;
        const int Al = (int)PolygonCategory.Aligned;
        const int RA = (int)PolygonCategory.ReverseAligned;
        const int Ou = (int)PolygonCategory.Outside;

        // Standard Additive (Union): A + B
        static readonly int[,] s_Additive =
        {
            { In, In, In, In },
            { In, Al, In, Al },
            { In, In, RA, RA },
            { In, Al, RA, Ou },
        };

        // Standard Subtractive: A - B
        static readonly int[,] s_Subtractive =
        {
            { Ou, Ou, Ou, Ou },
            { RA, In, RA, Ou },
            { Al, Al, In, Ou },
            { In, Al, RA, Ou },
        };

        // Standard Intersect: A & B
        static readonly int[,] s_Intersect =
        {
            { In, Al, RA, Ou },
            { Al, Al, In, Ou },
            { RA, In, RA, Ou },
            { Ou, Ou, Ou, Ou },
        };

        // Beyond Additive: same as standard but center 2x2 → Outside
        static readonly int[,] s_AdditiveBeyond =
        {
            { In, In, In, In },
            { In, Ou, Ou, Al },
            { In, Ou, Ou, RA },
            { In, Al, RA, Ou },
        };

        // Beyond Subtractive: same as standard but center 2x2 → Outside
        static readonly int[,] s_SubtractiveBeyond =
        {
            { Ou, Ou, Ou, Ou },
            { RA, Ou, Ou, Ou },
            { Al, Ou, Ou, Ou },
            { In, Al, RA, Ou },
        };

        // Beyond Intersect: same as standard but center 2x2 → Outside
        static readonly int[,] s_IntersectBeyond =
        {
            { In, Al, RA, Ou },
            { Al, Ou, Ou, Ou },
            { RA, Ou, Ou, Ou },
            { Ou, Ou, Ou, Ou },
        };

        /// <summary>
        /// Get the standard operation table for a CSG operation.
        /// Used for brushes BEFORE the owning brush in the chain.
        /// </summary>
        public static int[,] GetStandard(CSGOperation operation)
        {
            switch (operation)
            {
                case CSGOperation.Additive:    return s_Additive;
                case CSGOperation.Subtractive: return s_Subtractive;
                case CSGOperation.Intersect:   return s_Intersect;
                default:                       return s_Additive;
            }
        }

        /// <summary>
        /// Get the "beyond" operation table for a CSG operation.
        /// Used for brushes AFTER the owning brush in the chain.
        /// </summary>
        public static int[,] GetBeyond(CSGOperation operation)
        {
            switch (operation)
            {
                case CSGOperation.Additive:    return s_AdditiveBeyond;
                case CSGOperation.Subtractive: return s_SubtractiveBeyond;
                case CSGOperation.Intersect:   return s_IntersectBeyond;
                default:                       return s_AdditiveBeyond;
            }
        }

        /// <summary>
        /// Look up the result of combining two categories through an operation table.
        /// </summary>
        public static PolygonCategory Lookup(int[,] table, PolygonCategory row, PolygonCategory col)
        {
            return (PolygonCategory)table[(int)row, (int)col];
        }
    }
}
