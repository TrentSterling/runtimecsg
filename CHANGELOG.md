# Changelog

## [0.2.0] - 2026-02-09

### Added
- Chisel-style per-brush CSG engine (`ChiselCSGEngine`) replacing monolithic BSP approach
- Per-brush boolean evaluation with fragment splitting and chain evaluation
- `BrushPairIntersection` for brush overlap detection and polygon categorization
- `RoutingTable` and `OperationTables` for CSG operation lookups
- `PolygonCategory` enum (Inside, Aligned, ReverseAligned, Outside)
- 143 unit tests covering all CSG operations, edge cases, and engine internals

### Fixed
- Touching brushes incorrectly detected as overlapping (separating plane epsilon)
- Polygon categorization failures on edge/corner vertices (now uses centroid)
- Intersect operations on non-overlapping brushes incorrectly producing geometry
- Coplanar surface z-fighting via later-brush-wins tiebreaker

### Changed
- `CSGModel.RebuildChunk` now uses `ChiselCSGEngine` instead of `BSPTree`
- Version bump to 0.2.0

## [0.1.0] - 2026-02-09

### Added
- Initial scaffold: CSG math layer (CSGPlane, CSGVertex, CSGPolygon, PolygonClipper)
- BSP tree with Union, Subtract, Intersect operations
- CSGBrush and CSGModel MonoBehaviours with ExecuteAlways
- Chunk-based spatial system with dirty tracking and time-sliced rebuilds
- Mesh generation from BSP tree output
- Planar UV projection
- Brush primitives: box, wedge, cylinder, arch, sphere
- Basic editor inspectors and scene handles
