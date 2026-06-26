using Mapsui.Styles;

namespace MissionPlannerAvalonia.Controls;

// Heading-aware vehicle marker for the maps (modern stand-in for upstream Common.getMAVMarker:
// a triangle pointing along the vehicle's yaw, red when this is the active vehicle, grey otherwise).
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
