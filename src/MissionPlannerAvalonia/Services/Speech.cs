using System;
using System.Diagnostics;

namespace MissionPlannerAvalonia.Services;

public static class Speech {

  public static bool Enabled { get; set; }

  private static Process? _current;

  public static void Speak(string text) {
    if (!Enabled || string.IsNullOrWhiteSpace(text)) {
      return;
    }
    try {
      if (OperatingSystem.IsMacOS()) {
        Start("say", new[] { text });
      } else if (OperatingSystem.IsLinux()) {

        if (!TryStart("spd-say", new[] { text })) {
          StartShell("festival", $"echo {Quote(text)} | festival --tts");
        }
      } else if (OperatingSystem.IsWindows()) {

        var ps = "Add-Type -AssemblyName System.Speech; " +
                 "(New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak(" +
                 Quote(text) + ")";
        Start("powershell", new[] { "-NoProfile", "-Command", ps });
      }
    } catch {

    }
  }

  public static void Stop() {
    try {
      if (_current is { HasExited: false }) {
        _current.Kill(true);
      }
    } catch {

    } finally {
      _current = null;
    }
  }

  private static void Start(string file, string[] args) {
    var psi = new ProcessStartInfo(file) { UseShellExecute = false };
    foreach (var a in args) {
      psi.ArgumentList.Add(a);
    }
    _current = Process.Start(psi);
  }

  private static bool TryStart(string file, string[] args) {
    try {
      Start(file, args);
      return _current != null;
    } catch {
      return false;
    }
  }

  private static void StartShell(string _, string command) {
    var psi = new ProcessStartInfo("/bin/bash") { UseShellExecute = false };
    psi.ArgumentList.Add("-c");
    psi.ArgumentList.Add(command);
    _current = Process.Start(psi);
  }

  private static string Quote(string text) => "\"" + text.Replace("\"", "\\\"") + "\"";
}
