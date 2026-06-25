# ⚡ AutoCAD Quantity Takeoff (QTO) Plugin

A professional AutoCAD plugin for automated **Bill of Quantities (BOQ)** generation from DWG drawings, built with C# / .NET 8 and the AutoCAD .NET API.

---

## ✨ Features

| Feature | Detail |
|---------|--------|
| **Entity Extraction** | Lines, Polylines (2D/3D), Arcs, Circles, Blocks, Dynamic Blocks, MText, Text, Hatches, Dimensions |
| **Quantity Calculation** | Length, Area, Perimeter, Volume, Count |
| **Layer-Based Classification** | Groups quantities by layer, object type, or block name |
| **Attribute Extraction** | Block attributes: Type, Rating, Size, Voltage, Manufacturer, TagNo |
| **Text Recognition** | Regex-based parsing of nearby notes (Depth, CableSize, Voltage, Material…) |
| **Spatial Association** | Links nearby text objects to the nearest entity within a user-defined radius |
| **Electrical Module** | Auto-classifies LV/MV Cables, Trays, Conduits, Poles, Panels, Manholes… |
| **WPF UI** | Modern dockable panel with live preview, search, filtering, and progress bar |
| **Multi-format Export** | Excel (.xlsx), CSV, JSON, XML |
| **Batch Processing** | Process multiple DWG files in a folder |
| **Performance** | Parallel batch processing; supports 500 000+ entities |

---

## 🏗️ Solution Architecture

```
AutoCAD-QTO-Plugin/
├── src/
│   ├── Core/                  # Models, interfaces, enums (no AutoCAD dependency)
│   ├── DataExtraction/        # AutoCAD entity extraction + electrical module
│   ├── QuantityEngine/        # BOQ aggregation and statistics
│   ├── TextRecognition/       # Regex text parsing and spatial association
│   ├── ExcelExport/           # Excel (EPPlus), CSV, JSON, XML exporters
│   └── UI/                    # WPF + MVVM AutoCAD plugin (entry point)
├── tests/
│   ├── Core.Tests/            # TextRecognizer unit tests
│   └── DataExtraction.Tests/  # QuantityEngine unit tests
├── installer/                 # Bundle manifest + PowerShell installer
└── docs/                      # User manual and sample drawings
```

**Architectural pattern:** Clean Architecture with MVVM (CommunityToolkit.Mvvm).  
**DI:** `Microsoft.Extensions.DependencyInjection` wired up in `PluginApp.Initialize()`.

---

## 🚀 Getting Started

### Prerequisites

| Tool | Version |
|------|---------|
| Windows 10/11 x64 | — |
| AutoCAD | 2021 – 2025 (R24+) |
| .NET SDK | 8.0+ |
| Visual Studio | 2022 17.8+ |

### 1 — Clone the repository

```bash
git clone https://github.com/your-org/AutoCAD-QTO-Plugin.git
cd AutoCAD-QTO-Plugin
```

### 2 — Set the AutoCAD directory

Edit `Directory.Build.props` or pass the path at build time:

```bash
dotnet build -p:AcadDir="C:\Program Files\Autodesk\AutoCAD 2025"
```

### 3 — Build

```bash
dotnet build AutoCAD-QTO-Plugin.sln -c Release
```

### 4 — Run unit tests

```bash
dotnet test
```

### 5 — Install the plugin

Copy the built output into an AutoCAD `.bundle` folder, then run the installer:

```powershell
# Current user only
.\installer\Install-QTO.ps1

# All users (run as Administrator)
.\installer\Install-QTO.ps1 -AllUsers
```

### 6 — Load in AutoCAD

1. Launch AutoCAD.
2. Type `NETLOAD` → select `QTO.Plugin.dll`, **or** let the bundle auto-load.
3. Type **`QTO`** to open the panel.

---

## 🖥️ Commands

| Command | Description |
|---------|-------------|
| `QTO` | Opens the Quantity Takeoff dockable panel |
| `QTO_QUICK` | Runs a full takeoff on the entire drawing and exports to Excel |
| `QTO_BATCH` | Prompts for a folder and batch-processes all DWG files |

---

## 📊 Excel Output

The plugin generates a workbook with three sheets:

**Sheet 1 – BOQ Summary**

| Item No. | Description | Layer | Unit | Quantity | Remarks |
|----------|-------------|-------|------|----------|---------|
| 1 | LV Cable 4C×240 mm² | EL-CABLE | m | 1 250.000 | |
| 2 | Lighting Pole | EL-LIGHT | No | 35 | |

**Sheet 2 – Detailed Takeoff** — per-object row with all extracted properties.

**Sheet 3 – Statistics** — layer lengths, object type counts, totals.

---

## ⚡ Electrical Layer Naming Conventions

The electrical module maps layer prefixes to equipment types:

| Layer | Item |
|-------|------|
| `EL-CABLE` / `EL-LV` | LV Cable (0.6/1 kV) |
| `EL-MV` / `EL-HV` | MV Cable |
| `EL-TRAY` | Cable Tray |
| `EL-DUCT` / `EL-CONDUIT` | Conduit |
| `EL-LIGHT` | Lighting Fixture |
| `EL-POLE` | Lighting Pole |
| `EL-JB` | Junction Box |
| `EL-PANEL` / `EL-DB` | Panel / Distribution Board |
| `EL-EARTH` / `EL-GND` | Grounding Conductor |
| `EL-MANHOLE` | Manhole |
| `EL-HH` | Handhole |

Custom mappings can be added in `ElectricalModule.cs`.

---

## 🧩 Text Recognition Patterns

The plugin parses nearby text and MText for properties like:

```
Depth = 800 mm          → Depth: 800 mm
4C x 240 mm²            → CableSize: 4C x 240 mm²
Voltage = 13.8 kV       → VoltageLevel: 13.8 kV
Material: XLPE          → InsulationMaterial: XLPE
Location: Basement      → Description: Basement
Tag No: CB-101          → TagNumber: CB-101
```

---

## 🛠️ Configuration

All options are exposed in `ExtractionOptions` and in the UI panel:

| Option | Default | Description |
|--------|---------|-------------|
| `TextSearchRadius` | 1000 | Search radius for nearby text (drawing units) |
| `EnableTextRecognition` | true | Enable regex text parsing |
| `EnableElectricalModule` | true | Enable electrical classification |
| `GroupBy` | Layer | Grouping mode (Layer / Type / Block) |
| `MaxDegreeOfParallelism` | `Environment.ProcessorCount` | Parallel threads |
| `BatchSize` | 5000 | Entities per processing batch |

---

## 🤝 Contributing

1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-feature`.
3. Commit your changes: `git commit -m "feat: add my feature"`.
4. Push and open a Pull Request.

Please follow [Conventional Commits](https://www.conventionalcommits.org/).

---

## 📄 License

MIT — see [LICENSE](LICENSE).

---

## 📬 Support

Open a [GitHub Issue](https://github.com/your-org/AutoCAD-QTO-Plugin/issues) for bugs and feature requests.
