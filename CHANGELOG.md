# Changelog
All notable changes to this project will be documented in this file.

## [1.2.0] - 2026-09-07
### Added
- **Voxel rotation & group hierarchy** (collaborator contribution by **aqscithe** / [Allerium Labs](https://github.com/alleriumlabs)): full `nTRN` parsing with correct rotations and real root detection for grouped scenes.
- **Voxel animation data API**: `VoxAnimation`, `TransformKeyframe`, `ShapeKeyframe` and `VoxShapeAnimation` (data-only for now; playback integration is on the roadmap).
- Support for overriding the palette of colors in the editor import.
- Vengi (.vengi) file support.
### Changed
- Unified namespaces: readers now live in `Miventech.NativeVoxReader.Readers` (data in `Readers.Data`) and `VoxModelResult` in `Miventech.NativeVoxReader.VoxRenderer`.
- Runtime debug logs are now silent by default (opt-in via `BaseReaderFile.VerboseLogging`).
- Moved the `DecompressVengiData` debug utility to the `Editor` assembly.
### Fixed
- Vengi: palette buffer sized for 256 colors (crash with full palettes), model `size`/`position` Y-Z swap, color index 255 overflow producing invisible voxels.
- Null handling in the baked-UV pipeline: no more `NullReferenceException` when a model produces no quads, and safe defaults when settings are omitted.
- Runtime mesh creation now applies `VoxModel.rotation` (parity with editor import).
- `VoxReader` no longer destroys user objects parented to it, stops at the first valid reader, and the MagicaVoxel default palette fallback actually triggers when the file ships no `RGBA` chunk.
### Removed
- Qubicle (QBCL/QBT) reader support — it was non-functional.

## [1.0.0] - 2026-01-17
### Added
- Initial release.
- Scripted Importer for `.vox` files.
- Support for MagicaVoxel hierarchy (`nTRN`, `nGRP`, `nSHP`).
- Greedy Meshing for optimized geometry.
- Texture baking support for reduced draw calls.
- Assembly Definitions for faster compilation.

## [1.1.0] - 2026-02-15
### Added
- **Dynamic Rendering System**: New modular architecture for voxel rendering.
- **Multiple Rendering Modes**: Choose between "Baked Texture" (Atlas optimized) or "Palette Style" (Classic UV mapping).
- **Extensible Settings**: Integrated `[SerializeReference]` for dynamic, per-renderer configurations in the Inspector.
- **Custom Importer Editor**: Fully revamped Inspector for `.vox` assets that automatically discovers and lists available renderers.
- **Improved API**: New base classes `VoxRenderAbstract` and `VoxRenderSettings` for easier developer extension.
- **Organized Codebase**: Refactored internal tools and utilities for better maintenance.
