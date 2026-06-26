using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MissionPlannerAvalonia.Services;
using MissionPlannerAvalonia.ViewModels;

namespace MissionPlannerAvalonia.Views;

public partial class LogBrowseView : UserControl {
  public LogBrowseView() {
    InitializeComponent();
    OpenBtn.Click += OnOpen;
    GraphBtn.Click += (s, e) => OnGraphAxis(false);
    GraphRightBtn.Click += (s, e) => OnGraphAxis(true);
    RemoveBtn.Click += OnRemove;
    ClearBtn.Click += OnClear;
    PresetBtn.Click += OnApplyPreset;
    ModeBtn.Click += (s, e) => OnOverlay("MODE", "Mode", ScottPlot.Colors.Yellow);
    ErrBtn.Click += (s, e) => OnOverlay("ERR", "Subsys", ScottPlot.Colors.Red);
    EvBtn.Click += (s, e) => OnOverlay("EV", "Id", ScottPlot.Colors.Cyan);
    GridToggle.IsCheckedChanged += OnGridToggle;
    ExportCsvBtn.Click += OnExportCsv;
    KmlBtn.Click += OnKml;
    GpxBtn.Click += OnGpx;
    MatlabBtn.Click += OnMatlab;
    BinLogBtn.Click += OnBinToLog;
  }

  private LogBrowseViewModel? Vm => DataContext as LogBrowseViewModel;

  // Double-click a field node in the tree → graph it on the left axis.
  private void OnTreeDoubleTapped(object? sender, RoutedEventArgs e) {
    if (MsgTree.SelectedItem is LogFieldNode f) {
      PlotCurve(f.Type, f.Field, false);
    }
  }

  private void OnGraphAxis(bool rightAxis) {
    var resolved = Vm?.ResolveField();
    if (resolved is null) {
      if (Vm != null) {
        Vm.Status = "Pick a field in the tree or type TYPE.FIELD.";
      }
      return;
    }
    PlotCurve(resolved.Value.type, resolved.Value.field, rightAxis);
  }

  private async void PlotCurve(string type, string field, bool rightAxis) {
    if (Vm is not { } vm || vm.CurrentPath is null) {
      return;
    }
    vm.Busy = true;
    vm.Status = $"Graphing {type}.{field}…";
    try {
      var curve = await Task.Run(() => vm.ReadCurve(type, field));
      if (curve is null) {
        vm.Status = $"No data for {type}.{field}.";
        return;
      }
      Plot.SetSeries($"{type}.{field}{(rightAxis ? " (R)" : "")}", curve.Value.xs, curve.Value.ys,
          rightAxis: rightAxis);
      Plot.SetAxisLabels("Time (s)", "Value", "DataFlash");
      vm.Status = $"Plotted {curve.Value.xs.Count} points of {type}.{field}.";
    } catch (Exception ex) {
      vm.Status = $"Graph failed: {ex.Message}";
    } finally {
      vm.Busy = false;
    }
  }

  private void OnRemove(object? sender, RoutedEventArgs e) {
    if (MsgTree.SelectedItem is LogFieldNode f) {
      Plot.RemoveByLabel($"{f.Type}.{f.Field}");
      Plot.RemoveByLabel($"{f.Type}.{f.Field} (R)");
    }
  }

  private async void OnApplyPreset(object? sender, RoutedEventArgs e) {
    if (Vm is not { } vm || vm.SelectedPreset is not { } preset || vm.CurrentPath is null) {
      return;
    }
    Plot.ClearAll();
    int plotted = 0, skipped = 0;
    foreach (var curve in preset.Curves) {
      // Only literal TYPE.FIELD curves are supported; derived-math expressions are skipped (noted).
      var parts = curve.Expression.Split('.');
      if (parts.Length == 2 && !curve.Expression.Any(c => "()+-*/".Contains(c))) {
        var data = await Task.Run(() => vm.ReadCurve(parts[0], parts[1]));
        if (data is { } d) {
          Plot.SetSeries($"{curve.Expression}{(curve.Axis == 2 ? " (R)" : "")}", d.xs, d.ys,
              rightAxis: curve.Axis == 2);
          plotted++;
          continue;
        }
      }
      skipped++;
    }
    vm.Status = $"Preset '{preset.Name}': {plotted} plotted, {skipped} skipped (derived/missing).";
  }

  private void OnOverlay(string type, string field, ScottPlot.Color color) {
    if (Vm is not { } vm) {
      return;
    }
    var marks = vm.ReadOverlay(type, field);
    foreach (var (x, label) in marks) {
      Plot.AddVerticalLine(x, color, label);
    }
    vm.Status = $"{type}: {marks.Count} markers.";
  }

  private void OnGridToggle(object? sender, RoutedEventArgs e) {
    if (GridToggle.IsChecked == true && Vm is { SelectedType: { } type } vm) {
      var (columns, rows) = vm.ReadRows(type);
      RowsGrid.Columns.Clear();
      for (int c = 0; c < columns.Count; c++) {
        int idx = c;
        RowsGrid.Columns.Add(new DataGridTextColumn {
          Header = columns[c],
          Binding = new Avalonia.Data.Binding($"[{idx}]"),
        });
      }
      RowsGrid.ItemsSource = rows;
    }
  }

  private async void OnExportCsv(object? sender, RoutedEventArgs e) {
    if (Vm is not { SelectedType: { } type } vm || vm.CurrentPath is null) {
      return;
    }
    var outp = await PickSaveAsync("Export Visible CSV", "csv", SuggestName(vm.CurrentPath, "csv"));
    if (outp is null) {
      return;
    }
    var (columns, rows) = vm.ReadRows(type);
    await RunExportAsync(vm, "CSV", () => {
      using var w = new StreamWriter(outp);
      w.WriteLine(string.Join(",", columns));
      foreach (var r in rows) {
        w.WriteLine(string.Join(",", r));
      }
    }, outp);
  }

  private async void OnOpen(object? sender, RoutedEventArgs e) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return;
    }

    var files = await top.StorageProvider.OpenFilePickerAsync(
        new FilePickerOpenOptions {
          Title = "Open log",
          AllowMultiple = false,
          FileTypeFilter = new[]
            {
                    new FilePickerFileType("Logs") { Patterns = new[] { "*.tlog", "*.bin", "*.log" } },
            },
        }
    );
    var path = files.FirstOrDefault()?.TryGetLocalPath();
    if (path != null && DataContext is LogBrowseViewModel vm) {
      await vm.LoadFileAsync(path);
    }
  }

  private async void OnGraph(object? sender, RoutedEventArgs e) {
    if (DataContext is not LogBrowseViewModel vm) {
      return;
    }
    var path = vm.CurrentPath;
    if (path is null) {
      vm.Status = "Open a log first.";
      return;
    }
    var resolved = vm.ResolveField();
    if (resolved is null) {
      vm.Status = "Pick a type and field, or type TYPE.FIELD.";
      return;
    }
    var (type, field) = resolved.Value;

    vm.Busy = true;
    vm.Status = $"Graphing {type}.{field}…";
    try {
      var data = await Task.Run(() => DataFlashLog.ReadField(path, type, field));
      if (data.Count == 0) {
        vm.Status = $"No data for {type}.{field}.";
        return;
      }
      var xs = new double[data.Count];
      var ys = new double[data.Count];
      for (var i = 0; i < data.Count; i++) {
        xs[i] = data[i].time;
        ys[i] = data[i].value;
      }
      Plot.SetSeries($"{type}.{field}", xs, ys);
      Plot.SetAxisLabels("Time (s)", "Value", "DataFlash");
      vm.Status = $"Plotted {data.Count} points of {type}.{field}.";
    } catch (Exception ex) {
      vm.Status = $"Graph failed: {ex.Message}";
    } finally {
      vm.Busy = false;
    }
  }

  private void OnClear(object? sender, RoutedEventArgs e) {
    Plot.ClearAll();
    if (DataContext is LogBrowseViewModel vm) {
      vm.Status = "Graph cleared.";
    }
  }

  private async void OnKml(object? sender, RoutedEventArgs e) {
    if (DataContext is not LogBrowseViewModel vm || vm.CurrentPath is null) {
      return;
    }
    var outp = await PickSaveAsync("Export KML", "kml", SuggestName(vm.CurrentPath, "kml"));
    if (outp is null) {
      return;
    }
    await RunExportAsync(vm, "KML", () => DataFlashLog.ExportKml(vm.CurrentPath!, outp), outp);
  }

  private async void OnGpx(object? sender, RoutedEventArgs e) {
    if (DataContext is not LogBrowseViewModel vm || vm.CurrentPath is null) {
      return;
    }
    var outp = await PickSaveAsync("Export GPX", "gpx", SuggestName(vm.CurrentPath, "gpx"));
    if (outp is null) {
      return;
    }
    await RunExportAsync(vm, "GPX", () => DataFlashLog.ExportGpx(vm.CurrentPath!, outp), outp);
  }

  private async void OnMatlab(object? sender, RoutedEventArgs e) {
    if (DataContext is not LogBrowseViewModel vm || vm.CurrentPath is null) {
      return;
    }
    var path = vm.CurrentPath;
    vm.Busy = true;
    vm.Status = "Exporting Matlab…";
    try {
      await Task.Run(() => DataFlashLog.ExportMatlab(path,
          s => Dispatcher.UIThread.Post(() => vm.Status = s)));
      vm.Status = "Matlab export complete.";
    } catch (Exception ex) {
      vm.Status = $"Matlab failed: {ex.Message}";
    } finally {
      vm.Busy = false;
    }
  }

  private async void OnBinToLog(object? sender, RoutedEventArgs e) {
    if (DataContext is not LogBrowseViewModel vm || vm.CurrentPath is null) {
      return;
    }
    var outp = await PickSaveAsync("Convert Bin to Log", "log", SuggestName(vm.CurrentPath, "log"));
    if (outp is null) {
      return;
    }
    await RunExportAsync(vm, "Log", () => DataFlashLog.ConvertBinToLog(vm.CurrentPath!, outp), outp);
  }

  private async Task RunExportAsync(LogBrowseViewModel vm, string name, Action work, string outp) {
    vm.Busy = true;
    vm.Status = $"Exporting {name}…";
    try {
      await Task.Run(work);
      vm.Status = $"{name} written: {outp}";
    } catch (Exception ex) {
      vm.Status = $"{name} failed: {ex.Message}";
    } finally {
      vm.Busy = false;
    }
  }

  private async Task<string?> PickSaveAsync(string title, string ext, string suggested) {
    var top = TopLevel.GetTopLevel(this);
    if (top is null) {
      return null;
    }
    var file = await top.StorageProvider.SaveFilePickerAsync(
        new FilePickerSaveOptions {
          Title = title,
          SuggestedFileName = suggested,
          DefaultExtension = ext,
          FileTypeChoices = new[]
            {
                    new FilePickerFileType(ext.ToUpperInvariant()) { Patterns = new[] { "*." + ext } },
            },
        }
    );
    return file?.TryGetLocalPath();
  }

  private static string SuggestName(string path, string ext) =>
      Path.GetFileNameWithoutExtension(path) + "." + ext;
}
