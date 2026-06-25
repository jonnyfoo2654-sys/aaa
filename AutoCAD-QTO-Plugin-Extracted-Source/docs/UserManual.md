# QTO Plugin — User Manual

## 1. Installation

Run `installer\Install-QTO.ps1` from an elevated PowerShell prompt, or copy the `QTO.bundle` folder manually to:

- **Current user:** `%APPDATA%\Autodesk\ApplicationPlugins\QTO.bundle`
- **All users:** `%ProgramData%\Autodesk\ApplicationPlugins\QTO.bundle`

## 2. Opening the Panel

Type `QTO` in the AutoCAD command line. The dockable panel will open on the right side of the screen.

## 3. Configuring Options

| Field | Purpose |
|-------|---------|
| Project Name | Appears on the Excel cover |
| Prepared By | Printed in the title block |
| Scope | Entire Drawing, Selection Set, or Selected Layers |
| Group By | How rows are grouped in the output table |
| Text Search Radius | How far (in drawing units) to search for nearby text |
| Text Recognition | Toggle regex property extraction |
| Electrical Module | Toggle auto-classification of electrical items |

## 4. Running a Takeoff

1. Open the drawing.
2. Fill in the options panel.
3. Click **▶ Run** (or type `QTO_QUICK`).
4. Watch the progress bar and status line.
5. When complete, the preview table populates.

## 5. Filtering the Preview

Use the **Search** box (top-right) to filter by layer name or item name. Click column headers to sort.

## 6. Exporting

| Button | Output |
|--------|--------|
| 📊 Excel | Formatted .xlsx with 3 sheets |
| 📄 CSV | Comma-separated flat file |
| { } JSON | Machine-readable JSON |
| ⚙ Batch | Processes all DWGs in a selected folder |

## 7. Electrical Layer Conventions

See the README for the full table. Add custom layer→type mappings in `ElectricalModule.cs` under `LayerMappings`.

## 8. Troubleshooting

| Problem | Solution |
|---------|----------|
| Command `QTO` not found | Type `NETLOAD` and load `QTO.Plugin.dll` manually |
| Zero rows extracted | Check that entities are on the correct layers and the scope matches |
| Excel export fails | Ensure the output folder is writable and no existing file is open |
| Text not associated | Increase the Text Search Radius option |

## 9. Batch Processing

1. Click **⚙ Batch** and select the folder containing DWG files.
2. The plugin queues the files; each is opened, processed, and an Excel file saved next to it.

> **Note:** Batch processing requires AutoCAD to open each DWG sequentially. For very large sets, consider running overnight.
