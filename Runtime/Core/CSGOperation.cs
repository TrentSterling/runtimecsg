namespace RuntimeCSG
{
    /// <summary>
    /// CSG boolean operation type.
    /// </summary>
    public enum CSGOperation
    {
        /// <summary>Add geometry (union).</summary>
        Additive = 0,
        /// <summary>Carve geometry (subtract).</summary>
        Subtractive = 1,
        /// <summary>Keep only intersecting geometry.</summary>
        Intersect = 2
    }
}
