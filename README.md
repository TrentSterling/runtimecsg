<p align="center">
  <img src="docs/icon-256.png" alt="RuntimeCSG" width="128">
</p>

# RuntimeCSG

Non-destructive CSG brush modeling for Unity. Works in-editor and at runtime.

Build levels and geometry using boolean operations (union, subtract, intersect) on brush primitives - no baking, no export, just real-time constructive solid geometry.

## Features

- **Boolean Operations** - Union, subtract, and intersect brushes in real-time
- **Brush Primitives** - Box, wedge, cylinder, arch, sphere out of the box
- **Per-Brush Processing** - Chisel-style engine evaluates each brush independently for fast incremental updates
- **Chunk System** - Grid-based spatial chunking with dirty tracking for efficient mesh rebuilds
- **Editor Tools** - Scene overlay toolbar, wireframe visualization, face handles for direct manipulation
- **Runtime Ready** - Works at runtime, not just in-editor. Build geometry on the fly
- **Double Precision Math** - Plane math uses doubles internally to avoid precision issues at scale

## Installation

Add to your Unity project via the Package Manager using a git URL:

```
https://github.com/TrentSterling/runtimecsg.git
```

Or clone and reference locally in your `Packages/manifest.json`:

```json
"com.runtimecsg.core": "file:../path/to/runtimecsg"
```

Requires Unity 2021.3 or later.

## Quick Start

1. Create an empty GameObject and add the **CSG Model** component
2. Add child GameObjects with **CSG Brush** components
3. Set brush shapes (box, cylinder, etc.) and operations (additive, subtractive, intersect)
4. Geometry updates automatically as you move and modify brushes

## How It Works

RuntimeCSG uses a Chisel-style per-brush CSG algorithm:

1. Each brush's polygons are split against the planes of all overlapping brushes
2. Each fragment is evaluated against the full boolean chain to determine visibility
3. Visible fragments are collected and built into chunked meshes

This replaces the traditional monolithic BSP approach with per-brush processing, enabling future incremental updates where only affected brushes need recomputation.

## License

[MIT](LICENSE)
