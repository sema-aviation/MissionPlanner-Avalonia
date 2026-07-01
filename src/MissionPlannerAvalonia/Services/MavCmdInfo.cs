using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using MissionPlanner;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.Services;

public static class MavCmdInfo {
  private static readonly Dictionary<string, string[]> _names =
      new(StringComparer.OrdinalIgnoreCase);
  private static readonly string[] _keys = { "P1", "P2", "P3", "P4", "X", "Y", "Z" };
  private static string? _subtree;

  public static string CurrentSubtree() {
    try {
      return AppState.comPort.MAV.cs.firmware switch {
        Firmwares.ArduPlane => "APM",
        Firmwares.Ateryx => "APM",
        Firmwares.ArduRover => "APRover",
        _ => "AC2",
      };
    } catch {
      return "AC2";
    }
  }

  public static void EnsureLoaded(string subtree) {
    if (_subtree == subtree) {
      return;
    }
    _subtree = subtree;
    _names.Clear();
    try {
      var path = Path.Combine(AppContext.BaseDirectory, "mavcmd.xml");
      if (!File.Exists(path)) {
        return;
      }
      var doc = new XmlDocument();
      doc.Load(path);
      var root = doc.SelectSingleNode("/CMD/" + subtree);
      if (root == null) {
        return;
      }
      foreach (XmlNode cmd in root.ChildNodes) {
        if (cmd.NodeType != XmlNodeType.Element) {
          continue;
        }
        var arr = new string[7];
        for (int i = 0; i < 7; i++) {
          var el = cmd[_keys[i]];
          arr[i] = ApplyUnit(el?.InnerText ?? "", el?.Attributes?["unitType"]?.Value);
        }
        _names[cmd.Name] = arr;
      }
    } catch {

    }
  }

  public static string[]? Get(string cmdName) =>
      _names.TryGetValue(cmdName, out var a) ? a : null;

  private static string ApplyUnit(string name, string? unit) => unit switch {
    "alt" => Append(name, CurrentState.AltUnit),
    "speed" => Append(name, CurrentState.SpeedUnit),
    "dist" => Append(name, CurrentState.DistanceUnit),
    _ => name,
  };

  private static string Append(string name, string u) =>
      string.IsNullOrEmpty(name) ? name : $"{name} ({u})";
}
