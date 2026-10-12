# Machine config templates

Copy these into `%ProgramData%\GMP\Platform\config\` (or the root set by `GMP_PLATFORM_ROOT`) and edit the paths for this machine.

| File | Purpose |
|------|---------|
| `settings.example.json` → `settings.json` | Seed DWG, font map, plot/layout options |
| `standards.example.json` → `standards.json` | Layer/style/block/tag names for a plant seed (optional; built-in Tindall defaults apply if omitted) |

Do not commit machine-specific `settings.json` with absolute client paths into this repository.
