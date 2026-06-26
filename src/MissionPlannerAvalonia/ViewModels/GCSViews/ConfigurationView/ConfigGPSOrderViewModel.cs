using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigGPSOrderViewModel : ParamPageBase {
  [ObservableProperty]
  private string _status = "";

  public ObservableCollection<GpsCanRow> Rows { get; } = new();

  public ConfigGPSOrderViewModel() {
    Title = "UAVCAN GPS Order";
    Intro = "Detected DroneCAN GPS nodes. Use Override to pin a node to GPS1 or GPS2.";
    Setup();
    BuildRows();
  }

  protected override void OnRefreshed() {
    Fields.Clear();
    Setup();
    BuildRows();
  }

  private void Setup() {
    F("GPS_CAN_NODEID1");
    F("GPS_CAN_NODEID2");
    F("GPS1_CAN_OVRIDE");
    F("GPS2_CAN_OVRIDE");
  }

  private int ParamInt(string name) {
    return comPort.MAV.param.ContainsKey(name)
        ? (int)Math.Round(comPort.MAV.param[name].Value)
        : 0;
  }

  private void BuildRows() {
    Rows.Clear();
    if (!comPort.MAV.param.ContainsKey("GPS1_CAN_OVRIDE")) {
      Status = "GPS1_CAN_OVRIDE not available — connect a DroneCAN-capable autopilot.";
      return;
    }

    int id1 = ParamInt("GPS_CAN_NODEID1");
    int id2 = ParamInt("GPS_CAN_NODEID2");
    int id1ovr = ParamInt("GPS1_CAN_OVRIDE");
    int id2ovr = ParamInt("GPS2_CAN_OVRIDE");

    if (id1ovr != 0) {
      Rows.Add(new GpsCanRow(1, "GPS Override 1", id1ovr));
    }
    if (id2ovr != 0) {
      Rows.Add(new GpsCanRow(2, "GPS Override 2", id2ovr));
    }
    if (id1 != 0 && id1 != id1ovr && id1 != id2ovr) {
      Rows.Add(new GpsCanRow(98, "GPS Detect 1", id1));
    }
    if (id2 != 0 && id2 != id1ovr && id2 != id2ovr) {
      Rows.Add(new GpsCanRow(99, "GPS Detect 2", id2));
    }
    Status = Rows.Count == 0 ? "No CAN GPS nodes detected." : "";
  }

  [RelayCommand]
  [System.Obsolete]
  private void Override1(GpsCanRow? row) => WriteOverride("GPS1_CAN_OVRIDE", row);

  [RelayCommand]
  [System.Obsolete]
  private void Override2(GpsCanRow? row) => WriteOverride("GPS2_CAN_OVRIDE", row);

  [System.Obsolete]
  private async void WriteOverride(string param, GpsCanRow? row) {
    if (row == null) {
      return;
    }
    if (comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }
    try {
      int node = row.NodeID;
      bool ok = await System.Threading.Tasks.Task.Run(() => comPort.setParam(param, node));
      Status = ok ? $"{param} = {node}" : "write failed";
      await System.Threading.Tasks.Task.Run(() => comPort.getParamList());
      OnRefreshed();
      foreach (var f in Fields) {
        f.Reload();
      }
    } catch (Exception ex) {
      Status = ex.Message;
    }
  }
}

public class GpsCanRow {
  public GpsCanRow(int order, string name, int nodeID) {
    Order = order;
    Name = name;
    NodeID = nodeID;
  }

  public int Order { get; }
  public string Name { get; }
  public int NodeID { get; }
}
