using System;
using System.Collections.Generic;
using System.Globalization;
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
    Plot.PointClicked += OnPlotPointClicked;
    RowsGrid.SelectionChanged += OnRowSelected;
    ExportCsvBtn.Click += OnExportCsv;
    KmlBtn.Click += OnKml;
    GpxBtn.Click += OnGpx;
    MatlabBtn.Click += OnMatlab;
    BinLogBtn.Click += OnBinToLog;
    TrackMap.LiveVehicle = false;
    DataContextChanged += OnDataContextChanged;
    OnDataContextChanged(this, EventArgs.Empty);
  }

  private LogBrowseViewModel? _wiredVm;

  private void OnDataContextChanged(object? sender, EventArgs e) {
    if (ReferenceEquals(_wiredVm, Vm)) {
      return;
    }
    if (_wiredVm != null) {
      _wiredVm.TrackChanged -= OnTrackChanged;
    }
    _wiredVm = Vm;
    if (_wiredVm != null) {
      _wiredVm.TrackChanged += OnTrackChanged;
    }
  }

  private void OnTrackChanged() {
    if (Vm is { } vm) {
      Dispatcher.UIThread.Post(() => TrackMap.ShowStaticTrack(vm.Track));
    }
  }

  private LogBrowseViewModel? Vm => DataContext as LogBrowseViewModel;

  private IReadOnlyList<string> _gridColumns = Array.Empty<string>();

  private void OnPlotPointClicked(double x) {
    if (Vm?.NearestTrackSample(x) is { } p) {
      TrackMap.ShowSampleMarker(p.lat, p.lng);
    }
  }

  private void OnRowSelected(object? sender, SelectionChangedEventArgs e) {
    if (RowsGrid.SelectedItem is not IReadOnlyList<string> row) {
      return;
    }
    int latIdx = IndexOfColumn("Lat");
    int lngIdx = IndexOfColumn("Lng");
    if (latIdx < 0 || lngIdx < 0 || latIdx >= row.Count || lngIdx >= row.Count) {
      return;
    }
    if (double.TryParse(row[latIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
        double.TryParse(row[lngIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var lng) &&
        !(lat == 0 && lng == 0)) {
      TrackMap.ShowSampleMarker(lat, lng);
    }
  }

  private int IndexOfColumn(string name) {
    for (int i = 0; i < _gridColumns.Count; i++) {
      if (string.Equals(_gridColumns[i], name, StringComparison.OrdinalIgnoreCase)) {
        return i;
      }
    }
    return -1;
  }

  private (double scale, double offset) ReadTransform() =>
      ((double)(ScaleBox.Value ?? 1m), (double)(OffsetBox.Value ?? 0m));

  private static IReadOnlyList<double> ApplyTransform(
      IReadOnlyList<double> ys, double scale, double offset) {
    if (scale == 1 && offset == 0) {
      return ys;
    }
    var outp = new double[ys.Count];
    for (int i = 0; i < ys.Count; i++) {
      outp[i] = ys[i] * scale + offset;
    }
    return outp;
  }

  private static string TransformSuffix(double scale, double offset) =>
      (scale == 1 && offset == 0)
          ? string.Empty
          : $" [×{scale:0.###}{(offset >= 0 ? "+" : "")}{offset:0.###}]";

  private void OnTreeDoubleTapped(object? sender, RoutedEventArgs e) {
    if (MsgTree.SelectedItem is LogFieldNode f) {
      PlotCurve(f.Type, f.Field, false);
    }
  }

  private async void OnGraphAxis(bool rightAxis) {
    if (Vm is not { } vm) {
      return;
    }

    var expr = vm.FieldExpression?.Trim();
    if (!string.IsNullOrEmpty(expr) && LogBrowseViewModel.IsExpression(expr)) {
      vm.Busy = true;
      vm.Status = $"Graphing {expr}…";
      try {
        var curve = await Task.Run(() => vm.ReadExpressionCurve(expr));
        if (curve is { } c) {
          var (scale, offset) = ReadTransform();
          var ys = ApplyTransform(c.ys, scale, offset);
          Plot.SetSeries($"{expr}{(rightAxis ? " (R)" : "")}{TransformSuffix(scale, offset)}",
              c.xs, ys, rightAxis: rightAxis);
          Plot.SetAxisLabels("Time (s)", "Value", "DataFlash");
          vm.Status = $"Plotted {c.xs.Count} points of {expr}.";
        } else {
          vm.Status = $"Could not evaluate '{expr}' (single message type only).";
        }
      } finally {
        vm.Busy = false;
      }
      return;
    }
    var resolved = vm.ResolveField();
    if (resolved is null) {
      vm.Status = "Pick a field in the tree or type TYPE.FIELD.";
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
      var (scale, offset) = ReadTransform();
      var ys = ApplyTransform(curve.Value.ys, scale, offset);
      Plot.SetSeries($"{type}.{field}{(rightAxis ? " (R)" : "")}{TransformSuffix(scale, offset)}",
          curve.Value.xs, ys, rightAxis: rightAxis);
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

      var data = await Task.Run(() => {
        if (LogBrowseViewModel.IsExpression(curve.Expression)) {
          return vm.ReadExpressionCurve(curve.Expression);
        }
        var parts = curve.Expression.Split('.');
        return parts.Length == 2 ? vm.ReadCurve(parts[0], parts[1]) : null;
      });
      if (data is { } d) {
        Plot.SetSeries($"{curve.Expression}{(curve.Axis == 2 ? " (R)" : "")}", d.xs, d.ys,
            rightAxis: curve.Axis == 2);
        plotted++;
      } else {
        skipped++;
      }
    }
    vm.Status = $"Preset '{preset.Name}': {plotted} plotted, {skipped} skipped (missing/cross-type).";
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
      _gridColumns = columns;
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
