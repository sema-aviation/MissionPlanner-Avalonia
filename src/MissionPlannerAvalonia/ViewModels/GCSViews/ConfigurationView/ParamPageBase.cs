using System.Collections.ObjectModel;
using System.Threading.Tasks;
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
