using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;

namespace MissionPlannerAvalonia.Controls;

public class LivePlot : ScottPlot.Avalonia.AvaPlot {
  private readonly Dictionary<string, ScottPlot.Plottables.Scatter> _series = new();
  private readonly Dictionary<string, List<double>> _appendXs = new();
  private readonly Dictionary<string, List<double>> _appendYs = new();

  public void SetSeries(string label, IReadOnlyList<double> xs, IReadOnlyList<double> ys,
                        ScottPlot.Color? color = null, bool rightAxis = false) {
    RunOnUi(() => {
      RemoveSeries(label);
      var scatter = Plot.Add.Scatter(xs.ToArray(), ys.ToArray(), color);
      scatter.LegendText = label;
      // Right-axis curves share the X scale but get a separate Y (mirrors ZedGraph Y2).
      if (rightAxis) {
        scatter.Axes.YAxis = Plot.Axes.Right;
      }
      _series[label] = scatter;
      Refresh();
    });
  }

  // Drop a single curve by label (mirrors LogBrowse "Remove Item").
  public void RemoveByLabel(string label) {
    RunOnUi(() => {
      RemoveSeries(label);
      Refresh();
    });
  }

  // Vertical marker line for mode/error/event overlays at an X position.
  public void AddVerticalLine(double x, ScottPlot.Color color, string? label = null) {
    RunOnUi(() => {
      var vl = Plot.Add.VerticalLine(x, 1, color);
      if (!string.IsNullOrEmpty(label)) {
        vl.LabelText = label;
      }
      Refresh();
    });
  }

  public IReadOnlyCollection<string> SeriesLabels => _series.Keys.ToList();

  public void AppendPoint(string label, double x, double y, int maxPoints = 2000) {
    RunOnUi(() => {
      if (!_appendXs.TryGetValue(label, out var xs)) {
        xs = new List<double>();
        _appendXs[label] = xs;
        _appendYs[label] = new List<double>();
      }
      var ys = _appendYs[label];
      xs.Add(x);
      ys.Add(y);
      while (xs.Count > maxPoints && xs.Count > 0) {
        xs.RemoveAt(0);
        ys.RemoveAt(0);
      }
      RemoveSeries(label);
      var scatter = Plot.Add.Scatter(xs.ToArray(), ys.ToArray());
      scatter.LegendText = label;
      _series[label] = scatter;
      Refresh();
    });
  }

  public void ClearAll() {
    RunOnUi(() => {
      Plot.Clear();
      _series.Clear();
      _appendXs.Clear();
      _appendYs.Clear();
      Refresh();
    });
  }

  public void SetAxisLabels(string xLabel, string yLabel, string? title = null) {
    RunOnUi(() => {
      Plot.XLabel(xLabel);
      Plot.YLabel(yLabel);
      if (title is not null) {
        Plot.Title(title);
      }
      Refresh();
    });
  }

  private void RemoveSeries(string label) {
    if (_series.TryGetValue(label, out var existing)) {
      Plot.Remove(existing);
      _series.Remove(label);
    }
  }

  private void RunOnUi(Action action) {
    if (CheckAccess()) {
      action();
    } else {
      Dispatcher.UIThread.Post(action);
    }
  }
}
