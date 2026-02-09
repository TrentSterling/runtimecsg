using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// Serializable brush data: planes, operation, order, and material.
    /// Can be used independently of MonoBehaviour for data-driven workflows.
    /// </summary>
    [Serializable]
    public class BrushDescriptor
    {
        [Tooltip("Planes defining the convex brush shape.")]
        public List<SerializablePlane> Planes = new List<SerializablePlane>();

        [Tooltip("CSG operation type.")]
        public CSGOperation Operation = CSGOperation.Additive;

        [Tooltip("Operation priority. Lower values are processed first.")]
        public int Order;

        [Tooltip("Default material index for all faces.")]
        public int MaterialIndex;

        /// <summary>
        /// Convert serializable planes to CSGPlanes.
        /// </summary>
        public List<CSGPlane> ToCSGPlanes()
        {
            var result = new List<CSGPlane>(Planes.Count);
            for (int i = 0; i < Planes.Count; i++)
            {
                var p = Planes[i];
                result.Add(new CSGPlane(p.A, p.B, p.C, p.D));
            }
            return result;
        }

        /// <summary>
        /// Set planes from CSGPlanes.
        /// </summary>
        public void FromCSGPlanes(List<CSGPlane> csgPlanes)
        {
            Planes.Clear();
            for (int i = 0; i < csgPlanes.Count; i++)
            {
                var p = csgPlanes[i];
                Planes.Add(new SerializablePlane(p.A, p.B, p.C, p.D));
            }
        }
    }

    /// <summary>
    /// Serializable plane data for Unity serialization (doubles stored as doubles).
    /// </summary>
    [Serializable]
    public struct SerializablePlane
    {
        public double A, B, C, D;

        public SerializablePlane(double a, double b, double c, double d)
        {
            A = a; B = b; C = c; D = d;
        }
    }
}
