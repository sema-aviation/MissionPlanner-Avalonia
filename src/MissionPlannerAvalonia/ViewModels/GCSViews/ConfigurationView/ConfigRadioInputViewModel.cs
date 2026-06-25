using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigRadioInputViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private bool _calibrating;
  private readonly int[] _min = new int[8];
  private readonly int[] _max = new int[8];

  public RcChannel[] Channels { get; }

  [ObservableProperty]
  private string _status = "Move all transmitter sticks/switches to view live input.";

  [ObservableProperty]
  private string _calibrateLabel = "Calibrate Radio";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigRadioInputViewModel() {
    Channels = new RcChannel[8];
    for (int i = 0; i < 8; i++) {
      Channels[i] = new RcChannel(i + 1);
    }

    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
    _timer.Tick += (_, _) => Pump();
    _timer.Start();
  }

  private void Pump() {
    var cs = _comPort.MAV.cs;
    int[] vals =
    {
            (int)cs.ch1in,
            (int)cs.ch2in,
            (int)cs.ch3in,
            (int)cs.ch4in,
            (int)cs.ch5in,
            (int)cs.ch6in,
            (int)cs.ch7in,
            (int)cs.ch8in,
        };
    for (int i = 0; i < 8; i++) {
      Channels[i].Value = vals[i];
      if (_calibrating && vals[i] > 800 && vals[i] < 2200) {
        _min[i] = _min[i] == 0 ? vals[i] : Math.Min(_min[i], vals[i]);
        _max[i] = Math.Max(_max[i], vals[i]);
        Channels[i].Min = _min[i];
        Channels[i].Max = _max[i];
      }
    }
  }

  [RelayCommand]
  [Obsolete]
  private async Task ToggleCalibrate() {
    if (!IsConnected) {
      Status = "Not connected — cannot calibrate.";
      return;
    }

    if (!_calibrating) {
      _calibrating = true;
      CalibrateLabel = "Click when Done";
      Status = "Move ALL sticks and switches to their extremes, then click Done.";
      for (int i = 0; i < 8; i++) {
        _min[i] = 0;
        _max[i] = 0;
      }
    } else {
      _calibrating = false;
      CalibrateLabel = "Calibrate Radio";
      Status = "Writing RCn_MIN / RCn_MAX…";
      int ok = 0;
      for (int i = 0; i < 8; i++) {
        if (_min[i] == 0 || _max[i] == 0) {
          continue;
        }

        int n = i + 1,
            mn = _min[i],
            mx = _max[i];
        if (await Task.Run(() => _comPort.setParam($"RC{n}_MIN", mn))) {
          ok++;
        }

        await Task.Run(() => _comPort.setParam($"RC{n}_MAX", mx));
      }
      Status = $"Calibration written ({ok} channels).";
    }
  }

  public void Dispose() => _timer.Stop();
}

public partial class RcChannel : ObservableObject {
  public RcChannel(int number) {
    Number = number;
  }

  public int Number { get; }

  [ObservableProperty]
  private int _value;

  [ObservableProperty]
  private int _min = 1500;

  [ObservableProperty]
  private int _max = 1500;
}
