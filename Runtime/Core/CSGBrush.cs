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

        /// <summary>
        /// Incremented each time the brush is dirtied. Used by editor visuals for cache invalidation.
        /// </summary>
        public int Version { get; private set; }

        Vector3 _lastPosition;
        Quaternion _lastRotation;
        Vector3 _lastScale;

        // Track old bounds so we can dirty chunks the brush has LEFT
        Bounds _previousBounds;
        bool _hasPreviousBounds;

        void OnEnable()
        {
            CacheTransform();
            _hasPreviousBounds = false;

            // Structural change (brush added) — full rebuild is most reliable
            var model = GetComponentInParent<CSGModel>();
            if (model != null)
                model.RebuildAll();
        }

        void OnDisable()
        {
            // Structural change (brush removed) — full rebuild to clean up all chunks
            var model = GetComponentInParent<CSGModel>();
            if (model != null)
                model.RebuildAll();
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
            Version++;
            var model = GetComponentInParent<CSGModel>();
            if (model != null)
            {
                // Dirty old chunks (where the brush WAS)
                if (_hasPreviousBounds)
                    model.SetDirtyBounds(_previousBounds);

                // Dirty new chunks (where the brush IS)
                model.SetDirty(this);

                // Cache current bounds for next time
                if (isActiveAndEnabled && _descriptor.Planes.Count >= 4)
                {
                    _previousBounds = GetBounds();
                    _hasPreviousBounds = true;
                }
            }
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
        /// Uses three-plane intersection to compute exact vertex positions, avoiding the
        /// precision loss of the large-polygon-clipping approach.
        /// </summary>
        public List<CSGPolygon> GeneratePolygons()
        {
            var planes = GetWorldPlanes();
            if (planes.Count < 4) return new List<CSGPolygon>();

            int n = planes.Count;

            // For each face, collect vertex positions from three-plane intersections
            var faceVertexLists = new List<List<Vector3>>(n);
            for (int i = 0; i < n; i++)
                faceVertexLists.Add(new List<Vector3>());

            for (int i = 0; i < n - 2; i++)
            {
                for (int j = i + 1; j < n - 1; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        if (BrushBoundsUtil.IntersectThreePlanes(planes[i], planes[j], planes[k], out var point))
                        {
                            if (BrushBoundsUtil.IsInsideAllPlanes(point, planes))
                            {
                                faceVertexLists[i].Add(point);
                                faceVertexLists[j].Add(point);
                                faceVertexLists[k].Add(point);
                            }
                        }
                    }
                }
            }

            // Build polygons for each face
            var polygons = new List<CSGPolygon>(n);

            for (int i = 0; i < n; i++)
            {
                var facePoints = faceVertexLists[i];
                if (facePoints.Count < 3) continue;

                // Remove duplicate points (within epsilon)
                RemoveDuplicatePoints(facePoints);
                if (facePoints.Count < 3) continue;

                // Sort vertices in winding order around the face normal
                SortWindingOrder(facePoints, planes[i]);

                // Create polygon
                Vector3 normal = planes[i].Normal;
                var verts = new List<CSGVertex>(facePoints.Count);
                for (int v = 0; v < facePoints.Count; v++)
                    verts.Add(new CSGVertex(facePoints[v], normal, Vector2.zero));

                var polygon = new CSGPolygon(verts, planes[i], _descriptor.MaterialIndex);
                if (!polygon.IsDegenerate())
                    polygons.Add(polygon);
            }

            return polygons;
        }

        static void RemoveDuplicatePoints(List<Vector3> points, float epsilon = 1e-4f)
        {
            float epsSq = epsilon * epsilon;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if ((points[i] - points[j]).sqrMagnitude < epsSq)
                    {
                        points.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        static void SortWindingOrder(List<Vector3> points, CSGPlane plane)
        {
            if (points.Count < 3) return;

            Vector3 normal = plane.Normal;

            // Compute centroid
            Vector3 center = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                center += points[i];
            center /= points.Count;

            // Build tangent frame on the face plane
            Vector3 up = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 tangent = Vector3.Cross(normal, up).normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent);

            // Sort by angle in tangent/bitangent plane
            points.Sort((a, b) =>
            {
                Vector3 da = a - center;
                Vector3 db = b - center;
                float angleA = Mathf.Atan2(Vector3.Dot(da, bitangent), Vector3.Dot(da, tangent));
                float angleB = Mathf.Atan2(Vector3.Dot(db, bitangent), Vector3.Dot(db, tangent));
                return angleA.CompareTo(angleB);
            });

            // Verify winding matches face normal direction
            Vector3 edge1 = points[1] - points[0];
            Vector3 edge2 = points[2] - points[0];
            if (Vector3.Dot(Vector3.Cross(edge1, edge2), normal) < 0)
                points.Reverse();
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
