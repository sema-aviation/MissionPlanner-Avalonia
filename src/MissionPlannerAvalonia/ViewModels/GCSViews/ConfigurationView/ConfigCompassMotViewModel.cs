using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlannerAvalonia.Controls;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigCompassMotViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;
  private LivePlot? _plot;
  private int _sub = -1;
  private bool _inCompassMot;

  public ConfigCompassMotViewModel() {
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    _timer.Tick += (_, _) => PumpMessages();
  }

  public string Instructions =>
      "Measures interference on the compass from the motors. REMOVE ALL PROPELLERS, secure the "
      + "vehicle, then press Start and slowly raise the throttle to maximum over a few seconds. "
      + "Press Finish to stop and store the result. Requires AC 3.2+.";

  [ObservableProperty]
  private string _buttonLabel = "Start";

  [ObservableProperty]
  private string _throttle = "0 %";

  [ObservableProperty]
  private string _current = "0.00 A";

  [ObservableProperty]
  private string _interference = "0 %";

  [ObservableProperty]
  private string _compensation = "0.00, 0.00, 0.00";

  [ObservableProperty]
  private string _log = "";

  public void AttachPlot(LivePlot plot) {
    _plot = plot;
    _plot.SetAxisLabels("Throttle %", "Interference % / Amps", "Compass Motor Calibration");

    if (_sub == -1) {
      _sub = _comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.COMPASSMOT_STATUS,
          ProcessCompassMotMsg,
          (byte)_comPort.sysidcurrent,
          (byte)_comPort.compidcurrent);
    }
  }

  [RelayCommand]
  private void Toggle() {
    if (_inCompassMot) {
      Finish();
    } else {
      Start();
    }
  }

  private async void Start() {
    if (_comPort.BaseStream?.IsOpen != true) {
      Log = "Connect to a vehicle first.";
      return;
    }

    _plot?.ClearAll();
    _plot?.SetAxisLabels("Throttle %", "Interference % / Amps", "Compass Motor Calibration");
    _comPort.MAV.cs.messages.Clear();

    try {
      var ok = await Task.Run(() => _comPort.doCommand(
          (byte)_comPort.sysidcurrent,
          (byte)_comPort.compidcurrent,
          MAVLink.MAV_CMD.PREFLIGHT_CALIBRATION,
          0, 0, 0, 0, 0, 1, 0));
      if (!ok) {
        Log = "Compassmot requires AC 3.2+";
        return;
      }
    } catch {
      Log = "Compassmot requires AC 3.2+";
      return;
    }

    _inCompassMot = true;
    ButtonLabel = "Finish";
    _timer.Start();
  }

  private async void Finish() {
    _inCompassMot = false;
    ButtonLabel = "Start";
    _timer.Stop();
    try {
      await Task.Run(() => _comPort.SendAck());
    } catch {
    }
  }

  private bool ProcessCompassMotMsg(MAVLink.MAVLinkMessage arg) {
    if (arg.msgid != (uint)MAVLink.MAVLINK_MSG_ID.COMPASSMOT_STATUS) {
      return true;
    }

    var status = (MAVLink.mavlink_compassmot_status_t)arg.data;
    var throttle = status.throttle / 10.0;

    Dispatcher.UIThread.Post(() => {
      Throttle = throttle.ToString("0") + " %";
      Current = status.current.ToString("0.00") + " A";
      Interference = status.interference + " %";
      Compensation = status.CompensationX.ToString("0.00") + ", "
          + status.CompensationY.ToString("0.00") + ", "
          + status.CompensationZ.ToString("0.00");

      _plot?.AppendPoint("Interference", throttle, status.interference);
      _plot?.AppendPoint("Current", throttle, status.current);
    });

    return true;
  }

  private void PumpMessages() {
    var sb = new StringBuilder();
    _comPort.MAV.cs.messages.ForEach(x => sb.AppendLine(x.message));
    Log = sb.ToString();
  }

  public void Dispose() {
    _timer.Stop();
    try {
      if (_comPort.BaseStream?.IsOpen == true) {
        _comPort.SendAck();
      }
      if (_sub != -1) {
        _comPort.UnSubscribeToPacketType(_sub);
        _sub = -1;
      }
    } catch {
    }
  }
}
