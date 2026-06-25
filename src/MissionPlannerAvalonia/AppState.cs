using System.Collections.Generic;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia;

public static class AppState {
  public static MAVLinkInterface comPort { get; } = new MAVLinkInterface();

  // Raised after the link opens or closes so connection-gated nav (Setup/Config)
  // can refresh which pages are visible.
  public static event System.Action? ConnectionChanged;

  public static void RaiseConnectionChanged() => ConnectionChanged?.Invoke();

  public static bool IsConnected => comPort.BaseStream?.IsOpen == true;

  // Connection params (host/port/url) consumed by the Comms transports through
  // CommsBase.Settings. The UI prefills these before opening a network stream.
  public static Dictionary<string, string> CommsSettings { get; } = new();

  static AppState() {
    CommsBase.Settings += (name, value, set) => {
      if (set) {
        CommsSettings[name] = value;
        return value;
      }
      return CommsSettings.TryGetValue(name, out var v) ? v : "";
    };
    // Transports prompt via this when a value is missing; we feed them from the UI
    // instead, so never block — NotSet makes them keep the settings/default value.
    CommsBase.InputBoxShow += (string title, string prompt, ref string text) =>
        inputboxreturn.NotSet;
  }
}
