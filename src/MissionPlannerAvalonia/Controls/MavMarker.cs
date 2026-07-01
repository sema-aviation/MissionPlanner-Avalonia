using Mapsui.Styles;

namespace MissionPlannerAvalonia.Controls;

public static class MavMarker {
  public static SymbolStyle Vehicle(double headingDeg, bool active = true) => new() {
    SymbolType = SymbolType.Triangle,
    Fill = new Brush(active ? Color.Red : Color.Gray),
    Outline = new Pen(Color.White, 2),
    SymbolScale = 0.9,
    SymbolRotation = headingDeg,
    RotateWithMap = true,
  };
}
