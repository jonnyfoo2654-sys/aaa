using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using QTO.Core.Enums;
using QTO.Core.Interfaces;
using QTO.Core.Models;
using QTO.ExcelExport;

namespace QTO.Plugin.ViewModels
{
    /// <summary>
    /// Primary ViewModel for the QTO dockable panel.
    /// Exposes all commands and observable state for the WPF view.
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IEntityExtractor _extractor;
        private readonly IQuantityEngine _engine;
        private readonly ExcelExporter _excelExporter;
        private readonly CsvExporter _csvExporter;
        private readonly JsonExporter _jsonExporter;
        private readonly XmlExporter _xmlExporter;

        private CancellationTokenSource? _cts;
        private BOQSummary? _lastSummary;

        public MainViewModel(
            IEntityExtractor extractor,
            IQuantityEngine engine,
            ExcelExporter excelExporter,
            CsvExporter csvExporter,
            JsonExporter jsonExporter,
            XmlExporter xmlExporter)
        {
            _extractor    = extractor;
            _engine       = engine;
            _excelExporter = excelExporter;
            _csvExporter  = csvExporter;
            _jsonExporter = jsonExporter;
            _xmlExporter  = xmlExporter;
        }

        // ── Observable properties ────────────────────────────────────────────

        [ObservableProperty] private string _statusText = "Ready";
        [ObservableProperty] private double _progressValue;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _projectName = string.Empty;
        [ObservableProperty] private string _preparedBy = string.Empty;
        [ObservableProperty] private double _textSearchRadius = 1000;
        [ObservableProperty] private bool _enableTextRecognition = true;
        [ObservableProperty] private bool _enableElectricalModule = true;
        [ObservableProperty] private bool _includeCoordinates = false;
        [ObservableProperty] private ExtractionScope _selectedScope = ExtractionScope.EntireDrawing;
        [ObservableProperty] private GroupByMode _selectedGroupBy = GroupByMode.Layer;
        [ObservableProperty] private string _outputFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        public ObservableCollection<QuantityRowViewModel> Rows { get; } = new();
        public ObservableCollection<string> AvailableLayers { get; } = new();
        public ObservableCollection<string> SelectedLayers { get; } = new();
        public ObservableCollection<string> StatusLog { get; } = new();

        public Array ScopeValues => Enum.GetValues<ExtractionScope>();
        public Array GroupByValues => Enum.GetValues<GroupByMode>();

        // Filtered view of rows based on SearchText
        public IEnumerable<QuantityRowViewModel> FilteredRows =>
            string.IsNullOrWhiteSpace(SearchText)
                ? Rows
                : Rows.Where(r =>
                    r.Layer.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    r.ItemName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        partial void OnSearchTextChanged(string value) =>
            OnPropertyChanged(nameof(FilteredRows));

        // ── Commands ─────────────────────────────────────────────────────────

        [RelayCommand(CanExecute = nameof(CanRun))]
        public async Task RunExtractionAsync()
        {
            _cts = new CancellationTokenSource();
            IsBusy = true;
            Rows.Clear();
            StatusLog.Clear();

            try
            {
                var options = BuildOptions();
                var progress = new Progress<ExtractionProgress>(OnProgress);

                Log("Starting extraction…");
                var sw = System.Diagnostics.Stopwatch.StartNew();

                var items = await _extractor.ExtractAsync(options, progress, _cts.Token);

                Log($"Extracted {items.Count()} entities in {sw.ElapsedMilliseconds} ms. Computing BOQ…");

                _lastSummary = _engine.Compute(items, options);
                _lastSummary.Stats.ProcessingTime = sw.Elapsed;

                PopulateRows(_lastSummary);
                StatusText = $"Done — {_lastSummary.Stats.TotalObjects} objects on {_lastSummary.Stats.TotalLayers} layers.";
                Log(StatusText);
            }
            catch (OperationCanceledException)
            {
                StatusText = "Cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                Log($"ERROR: {ex}");
            }
            finally
            {
                IsBusy = false;
                ProgressValue = 0;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        public async Task ExportToExcelAsync()
        {
            if (_lastSummary == null) return;
            var path = GetSavePath("Excel Files|*.xlsx", "BOQ_Report.xlsx");
            if (path == null) return;

            IsBusy = true;
            try
            {
                await _excelExporter.ExportAsync(_lastSummary, path);
                StatusText = $"Exported to {path}";
                Log(StatusText);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { StatusText = $"Export error: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        public async Task ExportToCsvAsync()
        {
            if (_lastSummary == null) return;
            var path = GetSavePath("CSV Files|*.csv", "BOQ_Report.csv");
            if (path == null) return;

            await _csvExporter.ExportAsync(_lastSummary, path);
            StatusText = $"CSV exported to {path}";
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        public async Task ExportToJsonAsync()
        {
            if (_lastSummary == null) return;
            var path = GetSavePath("JSON Files|*.json", "BOQ_Report.json");
            if (path == null) return;

            await _jsonExporter.ExportAsync(_lastSummary, path);
            StatusText = $"JSON exported to {path}";
        }

        [RelayCommand]
        public void CancelExtraction() => _cts?.Cancel();

        [RelayCommand]
        public async Task RunBatchAsync()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select folder containing DWG files"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            var dwgFiles = Directory.GetFiles(dlg.SelectedPath, "*.dwg", SearchOption.AllDirectories);
            Log($"Batch processing {dwgFiles.Length} files…");
            // Batch processing requires opening each DWG via AutoCAD COM – placeholder
            StatusText = $"Batch: {dwgFiles.Length} files queued (open each DWG manually or use script).";
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private bool CanRun() => !IsBusy;
        private bool CanExport() => !IsBusy && _lastSummary != null;

        private ExtractionOptions BuildOptions() => new()
        {
            Scope = SelectedScope,
            SelectedLayers = SelectedLayers.ToList(),
            EnableTextRecognition = EnableTextRecognition,
            EnableElectricalModule = EnableElectricalModule,
            TextSearchRadius = TextSearchRadius,
            IncludeCoordinates = IncludeCoordinates,
            GroupBy = SelectedGroupBy,
            OutputFolder = OutputFolder,
            ProjectName = ProjectName,
            PreparedBy = PreparedBy,
        };

        private void OnProgress(ExtractionProgress p)
        {
            ProgressValue = p.Percentage;
            StatusText = $"{p.CurrentOperation} ({p.Processed}/{p.Total})";
        }

        private void PopulateRows(BOQSummary summary)
        {
            Rows.Clear();
            foreach (var group in summary.Groups)
                foreach (var item in group.Items)
                    Rows.Add(new QuantityRowViewModel(item));
            OnPropertyChanged(nameof(FilteredRows));
        }

        private void Log(string msg)
        {
            StatusLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");
        }

        private static string? GetSavePath(string filter, string defaultName)
        {
            var dlg = new SaveFileDialog { Filter = filter, FileName = defaultName };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        public void Dispose() => _cts?.Dispose();
    }

    /// <summary>Row item displayed in the preview DataGrid.</summary>
    public class QuantityRowViewModel
    {
        public string Layer { get; }
        public string ItemName { get; }
        public string ObjectType { get; }
        public string Unit { get; }
        public double Length { get; }
        public double Area { get; }
        public int Count { get; }
        public string CableSize { get; }
        public string Voltage { get; }
        public string Description { get; }

        public QuantityRowViewModel(QuantityItem item)
        {
            Layer       = item.Layer;
            ItemName    = item.ItemName;
            ObjectType  = item.ObjectType;
            Unit        = item.Unit;
            Length      = Math.Round(item.Length, 3);
            Area        = Math.Round(item.Area, 3);
            Count       = item.Count;
            CableSize   = item.ElectricalData?.CableSize ?? string.Empty;
            Voltage     = item.ElectricalData?.VoltageLevel ?? string.Empty;
            Description = item.Description;
        }
    }
}
