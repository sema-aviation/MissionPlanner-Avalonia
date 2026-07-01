using Avalonia.Controls;
using ScottPlot.Avalonia;

namespace MissionPlannerAvalonia.Views;

public class ElevationGraphWindow : Window {
  public ElevationGraphWindow(double[] dist, double[] terrain, double[] planned, string altUnit,
      string distUnit) {
    Title = "Elevation Graph";
    Width = 820;
    Height = 420;

    var view = new AvaPlot();
    var terr = view.Plot.Add.Scatter(dist, terrain, new ScottPlot.Color(150, 100, 50));
    terr.LegendText = "Terrain";
    var plan = view.Plot.Add.Scatter(dist, planned, new ScottPlot.Color(220, 40, 40));
    plan.LegendText = "Planned";
    view.Plot.XLabel($"Distance ({distUnit})");
    view.Plot.YLabel($"Altitude ({altUnit})");
    view.Plot.ShowLegend();
    view.Refresh();

    Content = view;
  }
}
