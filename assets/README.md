# assets

**`gmp.fmp`** — AutoCAD font map. The client's drawings reference `vfrc.shx`, `frc.shx` and
`archqwiku.shx`, which are Tindall's own SHX fonts and were not among the files GMP sent.
Without a substitution AutoCAD cannot render their text, and the proof is stark: replotting
GMP's *own* DWG without these fonts produces a sheet with every dimension and bill-of-material
value blank.

This maps them to `romans.shx` so the chain runs. **The shapes are not theirs** — line lengths
and text widths differ from the approved PDF. Ask GMP for the three SHX files; until they
arrive, do not present plotted text as matching theirs.

Point `FontMapPath` in `%ProgramData%\GMP\Platform\config\settings.json` at this file.
