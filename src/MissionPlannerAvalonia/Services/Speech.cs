using System;
using System.Diagnostics;

namespace MissionPlannerAvalonia.Services;

// Minimal cross-platform TTS shim (mirrors MP Utilities/Speech.cs intent). macOS uses the built-in
// `say` command. Windows SAPI (System.Speech) and Linux (festival/spd-say) are platform-specific:
// Windows is wired through PowerShell's SAPI here as a best-effort; Linux tries spd-say/festival.
public static class Speech {
  // Master toggle (mirrors MP speechEnable). Off by default.
  public static bool Enabled { get; set; }

  private static Process? _current;

  // Speak text asynchronously (fire-and-forget). No-op when disabled or empty.
  public static void Speak(string text) {
    if (!Enabled || string.IsNullOrWhiteSpace(text)) {
      return;
    }
    try {
      if (OperatingSystem.IsMacOS()) {
        Start("say", new[] { text });
      } else if (OperatingSystem.IsLinux()) {
        // spd-say if present, else festival; either is platform-specific.
        if (!TryStart("spd-say", new[] { text })) {
          StartShell("festival", $"echo {Quote(text)} | festival --tts");
        }
      } else if (OperatingSystem.IsWindows()) {
        // SAPI via PowerShell (System.Speech is not referenced in this build).
        var ps = "Add-Type -AssemblyName System.Speech; " +
                 "(New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak(" +
                 Quote(text) + ")";
        Start("powershell", new[] { "-NoProfile", "-Command", ps });
      }
    } catch {
      // TTS is best-effort; never throw to the caller.
    }
  }

  // Stop any in-progress speech.
  public static void Stop() {
    try {
      if (_current is { HasExited: false }) {
        _current.Kill(true);
      }
    } catch {
      // ignore
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
