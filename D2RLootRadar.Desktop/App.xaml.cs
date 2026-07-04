using D2RLootRadar.Desktop.ViewModels;
using D2RLootRadar.Desktop.Views;
using D2RLootRadar.Application.Contracts;
using D2RLootRadar.Application.Monitoring;
using D2RLootRadar.Desktop.Services;
using D2RLootRadar.Infrastructure.Alert;
using D2RLootRadar.Infrastructure.Capture;
using D2RLootRadar.Infrastructure.Configuration;
using D2RLootRadar.Infrastructure.FuzzyMatcher;
using D2RLootRadar.Infrastructure.Input;
using D2RLootRadar.Infrastructure.ItemBases;
using D2RLootRadar.Infrastructure.Ocr;
using D2RLootRadar.Infrastructure.Processes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace D2RLootRadar.Desktop;

/// <summary>
/// WPF application entry point.
/// Owns the generic host (<see cref="IHost"/>) that provides dependency injection,
/// and drives the loot monitoring pipeline's lifetime alongside the main window's.
/// </summary>
public partial class App : System.Windows.Application
{
  private readonly IHost _host;

  /// <summary>
  /// Build the DI container.
  /// Does not start any services - see <see cref="OnStartup"/>.
  /// </summary>
  public App()
  {
    _host = Host
      .CreateDefaultBuilder()
      .ConfigureServices(ConfigureServices)
      .Build();

    DispatcherUnhandledException += (_, ea) =>
      _host.Services.GetRequiredService<ILogger<App>>()
        .LogError(ea.Exception, "Unhandled exception on the UI thread.");
    // Don't set ea.Handled = true - we want to know if something is broken,
    // not paper over it and keep running in a possibly-corrupt state.
  }

  /// <summary>
  /// Registers all services.
  /// Everything is a singleton except <see cref="SettingsWindow"/>/<see cref="SettingsViewModel"/>,
  /// which are transient so the Settings window can be opened, closed, and reopened without reusing a
  /// closed WPF <see cref="Window"/> instance.
  /// </summary>
  private static void ConfigureServices(IServiceCollection services)
  {
    // --- Infrastructure -----

    services.AddSingleton<IGameProcessService, GameProcessService>();
    services.AddSingleton<ISettingsStore, JsonSettingsStore>();
    services.AddSingleton<IItemBaseCatalog, JsonItemBaseCatalog>();
    services.AddSingleton<IKeyboardMonitor, GlobalKeyboardMonitor>();
    services.AddSingleton<IGameCaptureService, GameCaptureService>();
    services.AddSingleton<IOcrService, OcrService>();
    services.AddSingleton<IAlertService, AlertService>();
    services.AddSingleton<IFuzzyMatcher, FuzzyMatcher>();
    services.AddSingleton<IOverlayService, OverlayService>();

    // --- Application ------

    services.AddSingleton<LootMonitoringService>();

    // --- Presentation -----

    services.AddSingleton<MainViewModel>();
    services.AddSingleton<MainWindow>();
    services.AddSingleton<TrayIconService>();
    services.AddTransient<SettingsViewModel>();
    services.AddTransient<SettingsWindow>();
  }

  /// <summary>
  /// Starts the host, the keyboard hook / detection pipeline, and shows the main window.
  /// </summary>
  protected override async void OnStartup(StartupEventArgs ea)
  {
    await _host.StartAsync();

    // Start the keyboard hook and loot detection pipeline.
    _host.Services.GetRequiredService<LootMonitoringService>().Start();

    MainWindow mainWindow
      = _host.Services.GetRequiredService<MainWindow>();

    // Wire the tray icon to the main window before showing it,
    // so the very first X-button close correctly minimizes to tray instead of closing.
    _host.Services.GetRequiredService<TrayIconService>().AttachTo(mainWindow);

    mainWindow.Show();
    base.OnStartup(ea);
  }

  /// <summary>
  /// Stops the keyboard hook before the host, so no new pipeline run can start mid-shutdown,
  /// then stops and disposes the host (which in turn disposes any DI-registered <see cref="IDisposable"/> singletons,
  /// e.g. <see cref="LootMonitoringService"/> and the OCR engine).
  /// </summary>
  protected override async void OnExit(ExitEventArgs ea)
  {
    _host.Services.GetRequiredService<LootMonitoringService>().Stop();

    await _host.StopAsync();

    _host.Dispose();
    base.OnExit(ea);
  }
}
