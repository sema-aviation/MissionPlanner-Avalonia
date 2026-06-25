using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigParachuteViewModel : ParamPageBase {
  private bool _suppressServo;

  public ObservableCollection<string> ServoOptions { get; } =
      new() { "RC9", "RC10", "RC11", "RC12", "RC13", "RC14" };

  [ObservableProperty]
  private string? _selectedServo;

  [ObservableProperty]
  private string _servoStatus = "";

  public ConfigParachuteViewModel() {
    Title = "Parachute";
    Intro = "Configure parachute release. Ensure props are removed before testing.";
    Setup();
    DetectServo();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
    DetectServo();
  }

  private void Setup() {
    F("CHUTE_ENABLED", "combo");

    var type = F("CHUTE_TYPE", "combo");
    type.Options.Clear();
    type.Options.Add(new ParamOption(0, "First Relay"));
    type.Options.Add(new ParamOption(1, "Second Relay"));
    type.Options.Add(new ParamOption(2, "Third Relay"));
    type.Options.Add(new ParamOption(3, "Fourth Relay"));
    type.Options.Add(new ParamOption(10, "Servo"));
    type.Reload();

    F("CHUTE_SERVO_ON");
    F("CHUTE_SERVO_OFF");
    F("CHUTE_ALT_MIN");
    F("CHUTE_DELAY_MS");
    F("CHUTE_CRT_SINK");
  }

  private void DetectServo() {
    _suppressServo = true;
    SelectedServo = null;
    foreach (var item in new List<string>(comPort.MAV.param.Keys)) {
      if (item.EndsWith("_FUNCTION", StringComparison.Ordinal) &&
          (int)Math.Round(comPort.MAV.param[item].Value) == 27) {
        var name = item.Replace("_FUNCTION", "");
        if (ServoOptions.Contains(name)) {
          SelectedServo = name;
        }
        break;
      }
    }
    _suppressServo = false;
  }

  [System.Obsolete]
  partial void OnSelectedServoChanged(string? value) {
    if (_suppressServo || value == null) {
      return;
    }
    AssignServo(value);
  }

  [System.Obsolete]
  private async void AssignServo(string servo) {
    if (comPort.BaseStream?.IsOpen != true) {
      ServoStatus = "offline";
      return;
    }
    try {
      EnsureDisabled(servo);
      bool ok = await Task.Run(() => comPort.setParam(servo + "_FUNCTION", 27));
      ServoStatus = ok ? "✓" : "write failed";
    } catch (Exception ex) {
      ServoStatus = ex.Message;
    }
  }

  [System.Obsolete]
  private void EnsureDisabled(string exclude) {
    foreach (var item in ServoOptions) {
      if (item == exclude) {
        continue;
      }
      var key = item + "_FUNCTION";
      if (comPort.MAV.param.ContainsKey(key) &&
          (int)Math.Round(comPort.MAV.param[key].Value) == 27) {
        comPort.setParam(key, 0);
      }
    }
  }
}
