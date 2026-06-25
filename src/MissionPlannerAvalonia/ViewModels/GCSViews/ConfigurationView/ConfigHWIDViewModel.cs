using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigHWIDViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public ObservableCollection<HwIdRow> Devices { get; } = new();

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigHWIDViewModel() {
    Load();
  }

  [RelayCommand]
  private void Load() {
    Devices.Clear();

    var param = _comPort.MAV?.param;
    if (param == null) {
      return;
    }

    var rows = param
        .Where(a => (a.Name.Contains("_ID") || a.Name.Contains("_DEVID"))
            && !a.Name.Contains("_IDX") && !a.Name.Contains("FRSKY"))
        .OrderBy(a => a.Name)
        .Select(a => Decode(a.Name, (uint)a.Value))
        .ToList();

    foreach (var row in rows) {
      Devices.Add(row);
    }
  }

  private static HwIdRow Decode(string paramName, uint id) {
    var devid = new Device.DeviceStructure(paramName, id);

    string busType = devid.bus_type.ToString().Replace("BUS_TYPE_", "");
    string devType;
    if (devid.bus_type == Device.BusType.BUS_TYPE_UAVCAN) {
      devType = "SENSOR_ID#" + devid.devtype;
    } else if (paramName.Contains("COMP")) {
      devType = devid.devtypecompass.ToString().Replace("DEVTYPE_", "");
    } else if (paramName.Contains("BARO")) {
      devType = devid.devtypebaro.ToString().Replace("DEVTYPE_", "");
    } else if (paramName.Contains("ASP")) {
      devType = devid.devtypeairspd.ToString().Replace("DEVTYPE_", "");
    } else {
      devType = devid.devtypeimu.ToString().Replace("DEVTYPE_", "");
    }

    return new HwIdRow {
      ParamName = paramName,
      DevID = (int)devid.devid,
      BusType = busType,
      Bus = devid.bus,
      Address = devid.address,
      DevType = devType,
    };
  }
}

public class HwIdRow {
  public string ParamName { get; init; } = "";
  public int DevID { get; init; }
  public string BusType { get; init; } = "";
  public int Bus { get; init; }
  public int Address { get; init; }
  public string DevType { get; init; } = "";
}
