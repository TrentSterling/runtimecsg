using System.Collections.Generic;
using UnityEngine;

namespace RuntimeCSG
{
    /// <summary>
    /// A convex CSG brush defined by a set of clipping planes.
    /// Attach as a child of a CSGModel to participate in CSG operations.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CSGBrush : MonoBehaviour
    {
        [SerializeField]
        BrushDescriptor _descriptor = new BrushDescriptor();

        public CSGOperation Operation
        {
            get => _descriptor.Operation;
            set
            {
                if (_descriptor.Operation != value)
                {
                    _descriptor.Operation = value;
                    NotifyDirty();
                }
            }
        }

        public int Order
        {
            get => _descriptor.Order;
            set
            {
                if (_descriptor.Order != value)
                {
                    _descriptor.Order = value;
                    NotifyDirty();
                }
            }
        }

        public int MaterialIndex
        {
            get => _descriptor.MaterialIndex;
            set
            {
                if (_descriptor.MaterialIndex != value)
                {
                    _descriptor.MaterialIndex = value;
                    NotifyDirty();
                }
            }
        }

        public BrushDescriptor Descriptor => _descriptor;

        Vector3 _lastPosition;
        Quaternion _lastRotation;
        Vector3 _lastScale;

        void OnEnable()
        {
            CacheTransform();
            NotifyDirty();
        }

        void OnDisable()
        {
            NotifyDirty();
        }

        void Update()
        {
            if (TransformChanged())
            {
                CacheTransform();
                NotifyDirty();
            }
        }

        void OnValidate()
        {
            NotifyDirty();
        }

        bool TransformChanged()
        {
            return transform.position != _lastPosition ||
                   transform.rotation != _lastRotation ||
                   transform.lossyScale != _lastScale;
        }

        void CacheTransform()
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _lastScale = transform.lossyScale;
        }

        void NotifyDirty()
        {
            var model = GetComponentInParent<CSGModel>();
            if (model != null)
                model.SetDirty(this);
        }

        /// <summary>
        /// Get the CSG planes in local space.
        /// </summary>
        public List<CSGPlane> GetLocalPlanes()
        {
            return _descriptor.ToCSGPlanes();
        }

        /// <summary>
        /// Get the CSG planes transformed to world space.
        /// </summary>
        public List<CSGPlane> GetWorldPlanes()
        {
            var localPlanes = _descriptor.ToCSGPlanes();
            var worldPlanes = new List<CSGPlane>(localPlanes.Count);
            var matrix = transform.localToWorldMatrix;

            for (int i = 0; i < localPlanes.Count; i++)
            {
                var p = localPlanes[i];
                // Transform plane normal and recalculate distance
                Vector3 localNormal = new Vector3((float)p.A, (float)p.B, (float)p.C);
                Vector3 localPoint = -localNormal * (float)p.D;

                Vector3 worldPoint = matrix.MultiplyPoint3x4(localPoint);
                Vector3 worldNormal = matrix.MultiplyVector(localNormal).normalized;

                worldPlanes.Add(new CSGPlane(worldNormal, worldPoint));
            }

            return worldPlanes;
        }

        /// <summary>
        /// Generate the convex polytope polygons from the brush's clipping planes in world space.
        /// Each pair of three planes that intersect within all other planes produces a vertex.
        /// </summary>
        public List<CSGPolygon> GeneratePolygons()
        {
            var planes = GetWorldPlanes();
            if (planes.Count < 4) return new List<CSGPolygon>();

            var polygons = new List<CSGPolygon>();

            for (int i = 0; i < planes.Count; i++)
            {
                // Start with a large polygon on this plane
                var polygon = CreateLargePolygon(planes[i], _descriptor.MaterialIndex);
                if (polygon == null) continue;

                // Clip against all other planes
                for (int j = 0; j < planes.Count; j++)
                {
                    if (i == j) continue;

                    PolygonClipper.Split(polygon, planes[j],
                        out var front, out _, out var cf, out _);

                    // Keep the front part (inside the convex volume)
                    polygon = front ?? cf;
                    if (polygon == null) break;
                }

                if (polygon != null && !polygon.IsDegenerate())
                {
                    polygons.Add(polygon);
                }
            }

            return polygons;
        }

        /// <summary>
        /// Create a large polygon on the given plane for clipping.
        /// </summary>
        static CSGPolygon CreateLargePolygon(CSGPlane plane, int materialIndex)
        {
            const float size = 10000f;
            Vector3 normal = plane.Normal;

            // Find a vector not parallel to the normal
            Vector3 up = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(normal, up).normalized * size;
            Vector3 v = Vector3.Cross(normal, u).normalized * size;

            Vector3 center = -normal * (float)plane.D;

            var vertices = new List<CSGVertex>(4)
            {
                new CSGVertex(center - u - v, normal, Vector2.zero),
                new CSGVertex(center + u - v, normal, Vector2.zero),
                new CSGVertex(center + u + v, normal, Vector2.zero),
                new CSGVertex(center - u + v, normal, Vector2.zero),
            };

            return new CSGPolygon(vertices, plane, materialIndex);
        }

        /// <summary>
        /// Get the world-space axis-aligned bounding box of this brush.
        /// </summary>
        public Bounds GetBounds()
        {
            return BrushBoundsUtil.ComputeBounds(GetWorldPlanes());
        }

        /// <summary>
        /// Set this brush to a box shape with the given half extents.
        /// </summary>
        public void SetBox(Vector3 halfExtents)
        {
            var planes = BrushFactory.Box(halfExtents);
            _descriptor.FromCSGPlanes(planes);
            NotifyDirty();
        }
    }
}
