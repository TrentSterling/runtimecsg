using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Vertex with position, normal, and UV coordinates.
    /// </summary>
    [Serializable]
    public struct CSGVertex : IEquatable<CSGVertex>
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;

        public CSGVertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            Position = position;
            Normal = normal;
            UV = uv;
        }

        public CSGVertex(Vector3 position)
        {
            Position = position;
            Normal = Vector3.zero;
            UV = Vector2.zero;
        }

        /// <summary>
        /// Linearly interpolate between two vertices.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CSGVertex Lerp(CSGVertex a, CSGVertex b, float t)
        {
            return new CSGVertex(
                Vector3.Lerp(a.Position, b.Position, t),
                Vector3.Lerp(a.Normal, b.Normal, t).normalized,
                Vector2.Lerp(a.UV, b.UV, t)
            );
        }

        public void Flip()
        {
            Normal = -Normal;
        }

        public bool Equals(CSGVertex other)
        {
            return Position == other.Position &&
                   Normal == other.Normal &&
                   UV == other.UV;
        }

        public override bool Equals(object obj) => obj is CSGVertex v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(Position, Normal, UV);
        public override string ToString() => $"CSGVertex({Position}, {Normal}, {UV})";
    }
}
