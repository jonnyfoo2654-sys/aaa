using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Microsoft.Extensions.DependencyInjection;
using QTO.Core.Interfaces;
using QTO.DataExtraction.Extractors;
using QTO.Plugin.ViewModels;
using QTO.Plugin.Views;
using QTO.TextRecognition;

// AutoCAD plugin registration attribute
[assembly: ExtensionApplication(typeof(QTO.Plugin.PluginApp))]
[assembly: CommandClass(typeof(QTO.Plugin.Commands))]

namespace QTO.Plugin
{
    /// <summary>
    /// Plugin lifecycle – called once on load and unload.
    /// </summary>
    public class PluginApp : IExtensionApplication
    {
        private static ServiceProvider? _services;
        internal static ServiceProvider Services => _services!;

        public void Initialize()
        {
            var services = new ServiceCollection();

            // Register core services
            services.AddSingleton<ITextRecognizer, TextRecognizer>();
            services.AddSingleton<IElectricalModule, ElectricalModule>();
            services.AddSingleton<IEntityExtractor, EntityExtractor>();
            services.AddSingleton<Core.Interfaces.IQuantityEngine, QuantityEngine.QuantityEngine>();
            services.AddSingleton<ExcelExport.ExcelExporter>();
            services.AddSingleton<ExcelExport.CsvExporter>();
            services.AddSingleton<ExcelExport.JsonExporter>();
            services.AddSingleton<ExcelExport.XmlExporter>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            _services = services.BuildServiceProvider();

            Application.DocumentManager.DocumentCreated += (_, _) => { };

            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage("\n[QTO] Quantity Takeoff Plugin loaded. Type QTO to open.\n");
        }

        public void Terminate()
        {
            _services?.Dispose();
        }
    }

    /// <summary>
    /// AutoCAD command definitions.
    /// </summary>
    public class Commands
    {
        private static QTOPalette? _palette;

        /// <summary>Opens the QTO dockable panel.</summary>
        [CommandMethod("QTO", CommandFlags.Modal)]
        public void OpenQTOPanel()
        {
            if (_palette == null)
            {
                var vm = PluginApp.Services.GetRequiredService<MainViewModel>();
                _palette = new QTOPalette(vm);
            }

            _palette.Show();
        }

        /// <summary>Runs a quick takeoff on entire drawing, exporting to Excel.</summary>
        [CommandMethod("QTO_QUICK", CommandFlags.UsePickSet)]
        public async void QuickTakeoff()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var vm = PluginApp.Services.GetRequiredService<MainViewModel>();
            await vm.RunExtractionAsync();
        }

        /// <summary>Batch-processes multiple DWG files in a folder.</summary>
        [CommandMethod("QTO_BATCH", CommandFlags.Session)]
        public async void BatchProcess()
        {
            var vm = PluginApp.Services.GetRequiredService<MainViewModel>();
            await vm.RunBatchAsync();
        }
    }
}
