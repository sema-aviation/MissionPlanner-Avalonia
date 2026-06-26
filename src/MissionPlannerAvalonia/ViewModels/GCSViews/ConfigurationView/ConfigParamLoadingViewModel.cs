using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// Port of MissionPlanner.GCSViews.ConfigurationView.ConfigParamLoading — the transient
// "parameters still loading" page. Shows getParamList() progress and a Retry Now button.
public partial class ConfigParamLoadingViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private readonly DispatcherTimer _timer;

  [ObservableProperty]
  private int _progressPercent;

  [ObservableProperty]
  private string _status = "Parameters are still loading. Many screens will not work until all parameters are loaded.";

  [ObservableProperty]
  private string _count = "";

  public bool GotAllParams =>
      _comPort.MAV.param.TotalReceived >= _comPort.MAV.param.TotalReported;

  public ConfigParamLoadingViewModel() {
    _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
    _timer.Tick += (_, _) => Tick();
    _timer.Start();
    Tick();
  }

  private void Tick() {
    var reported = _comPort.MAV.param.TotalReported;
    var received = _comPort.MAV.param.TotalReceived;
    ProgressPercent = reported > 0 ? (int)Math.Min(100, received * 100.0 / reported) : 0;
    Count = received + " / " + reported;
    if (GotAllParams && reported > 0) {
      Status = "All parameters loaded.";
    }
  }

  [RelayCommand]
  private async Task Retry() {
    Status = "Requesting parameters…";
    await Task.Run(() => _comPort.getParamList());
  }

  public void Dispose() {
    _timer.Stop();
  }
}
