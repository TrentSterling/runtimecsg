namespace RuntimeCSG
{
    /// <summary>
    /// Classification of a polygon fragment relative to a brush's solid volume.
    /// Used by the Chisel-style per-brush CSG algorithm.
    /// </summary>
    public enum PolygonCategory
    {
        /// <summary>Interior of solid - discard.</summary>
        Inside = 0,
        /// <summary>On surface, facing outward - keep.</summary>
        Aligned = 1,
        /// <summary>On surface, facing inward - flip and keep.</summary>
        ReverseAligned = 2,
        /// <summary>Exterior of solid - discard.</summary>
        Outside = 3
    }
}
