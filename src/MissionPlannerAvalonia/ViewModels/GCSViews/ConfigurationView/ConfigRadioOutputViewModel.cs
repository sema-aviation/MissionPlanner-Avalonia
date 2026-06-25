using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigRadioOutputViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;

  public List<ServoOutputRow> Rows { get; } = new();

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigRadioOutputViewModel() {
    var numServos = 16;
    if (_comPort.MAV.param.ContainsKey("SERVO_32_ENABLE") &&
        _comPort.MAV.param["SERVO_32_ENABLE"].Value > 0) {
      numServos = 32;
    }

    var cs = _comPort.MAV.cs;
    var csType = cs.GetType();
    for (int n = 1; n <= numServos; n++) {
      var pwm = csType.GetProperty($"ch{n}out",
          BindingFlags.Public | BindingFlags.Instance);
      Rows.Add(new ServoOutputRow(n, pwm));
    }

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
  }

  private void Pump() {
    var cs = _comPort.MAV.cs;
    foreach (var row in Rows) {
      row.UpdatePwm(cs);
    }
  }

  public void Dispose() => _timer.Stop();
}

public partial class ServoOutputRow : ObservableObject {
  private readonly PropertyInfo? _pwmProp;

  public int Number { get; }
  public ParamField Reversed { get; }
  public ParamField Function { get; }
  public ParamField Min { get; }
  public ParamField Trim { get; }
  public ParamField Max { get; }

  [ObservableProperty]
  private double _pwm = 1500;

  public ServoOutputRow(int number, PropertyInfo? pwmProp) {
    Number = number;
    _pwmProp = pwmProp;
    var servo = $"SERVO{number}";
    Reversed = new ParamField($"{servo}_REVERSED", "bool");
    Function = new ParamField($"{servo}_FUNCTION", "combo");
    Min = new ParamField($"{servo}_MIN");
    Trim = new ParamField($"{servo}_TRIM");
    Max = new ParamField($"{servo}_MAX");
  }

  public void UpdatePwm(object cs) {
    if (_pwmProp?.GetValue(cs) is { } v) {
      Pwm = Convert.ToDouble(v);
    }
  }
}
