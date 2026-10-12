# Changelog

All notable changes to the GMP Production Platform are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning follows [SemVer](https://semver.org/).

## [0.2.0] — 2026-10-12

### Changed
- Product track: drop the `0.1.0-demo` version suffix; packages are `GmpPlatform-0.2.0`.
- About dialog reads the assembly version and the running Revit year; points at Ticket Drawing.
- README and usage guide describe the production AutoCAD Ticket Drawing path (not the retired Revit-sheet route).
- Repository scaffolding: `config/` examples, `.editorconfig`, GitHub Actions CI, architecture note.

### Added (carried from Phase 1 build on this track)
- Multi-version Revit builds (2024 / 2025 / 2026) and installer packaging.
- Standards profile (`standards.json`) for layer/style/block/tag names.
- Opening dimensions (B.O.-n), assembly-hardware BOM rule (EMS on insert), sheet composition fixes.

### Notes
- Phase 1 content still awaiting GMP input: section-cut rules, edge details G–U, notes text, fonts/CTB, model personnel and mix data.

## [0.1.0-demo] — 2026-09

Initial proof-of-concept add-in and AutoCAD drawing engine path (POC repository `gmp-poc-step1`).
