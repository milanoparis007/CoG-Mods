# Source Material

This skill is backed by local source files that live outside the repo.

## Original files

- `C:\Users\User\Documents\COG Modding Stuff\cog-map-modding-part-1.pdf`
- `C:\Users\User\Documents\COG Modding Stuff\cog-map-modding-part-2.pdf`
- `C:\Users\User\Downloads\New Orleans.xlsx`

## Why the raw files are not vendored here

The skill uses compact markdown references by default so it does not load large PDFs or workbook blobs into context every time.

Use the derived docs first:

- `references/map-modding-source-summary.md`
- `references/new-orleans-workbook-example.md`
- `references/game-map-pipeline.md`

## Re-extraction notes

The PDFs were summarized from text extracted with:

```powershell
& 'C:\Program Files\Git\mingw64\bin\pdftotext.exe' -layout '<pdf>' '<output.txt>'
```

The workbook was inspected through OpenXML zip contents so formulas like `B2-B5` could be checked without Excel automation.
