using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ParamPageBase : ViewModelBase {
  protected readonly MAVLinkInterface comPort = AppState.comPort;

  public string Title { get; protected set; } = "";
  public string? Intro { get; protected set; }
  public ObservableCollection<ParamField> Fields { get; } = new();

  public bool IsConnected => comPort.BaseStream?.IsOpen == true;

  protected ParamField F(string name, string? kind = null) {
    var f = new ParamField(name, kind);
    Fields.Add(f);
    return f;
  }

  protected void F(params string[] names) {
    foreach (var n in names) {
      F(n);
    }
  }

  protected void FByPrefix(string prefix) {
    foreach (var key in System.Linq.Enumerable.ToList(comPort.MAV.param.Keys)) {
      if (key.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) {
        F(key);
      }
    }
  }

  [RelayCommand]
  private async Task Refresh() {
    if (!IsConnected) {
      await Services.Dialogs.Alert("Refresh parameters", "Not connected — cannot fetch params.");
      return;
    }

    // getParamListMavftp (what Open uses); the no-arg getParamList() NREs here because it builds a
    // WinForms progress dialog via a static event that's unregistered in this port — that null-event
    // NRE was the ESC-calibration "Refresh Params" crash. try/catch keeps any other fault contained.
    try {
      await Task.Run(() => comPort.getParamListMavftp(comPort.MAV.sysid, comPort.MAV.compid));
    } catch (System.Exception ex) {
      await Services.Dialogs.Alert("Refresh failed", ex.Message);
      return;
    }

    OnRefreshed();
    foreach (var f in Fields) {
      f.Reload();
    }
  }

  protected virtual void OnRefreshed() { }
}
