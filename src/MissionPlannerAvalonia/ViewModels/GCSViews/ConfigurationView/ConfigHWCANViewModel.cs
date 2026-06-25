using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigHWCANViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _suppress;

  public ObservableCollection<ParamOption> CanEnableOptions { get; } = new();

  [ObservableProperty]
  private ParamOption? _selectedCanEnable;

  [ObservableProperty]
  private bool _factoryResetArmed;

  [ObservableProperty]
  private string _status = "";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigHWCANViewModel() {
    Activate();
  }

  public void Activate() {
    _suppress = true;
    CanEnableOptions.Clear();
    var fw = _comPort.MAV.cs.firmware.ToString();
    foreach (var kv in SafeOptions("BRD_CAN_ENABLE", fw)) {
      CanEnableOptions.Add(new ParamOption(kv.Key, kv.Value));
    }
    if (_comPort.MAV.param.ContainsKey("BRD_CAN_ENABLE")) {
      var val = (int)Math.Round(_comPort.MAV.param["BRD_CAN_ENABLE"].Value);
      SelectedCanEnable = CanEnableOptions.FirstOrDefault(o => o.Value == val);
    }
    _suppress = false;
  }

  partial void OnSelectedCanEnableChanged(ParamOption? value) {
    if (_suppress || value == null) {
      return;
    }
    if (!IsConnected) {
      Status = "offline";
      return;
    }
    try {
      _comPort.setParam((byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, "BRD_CAN_ENABLE", value.Value);
      Status = "BRD_CAN_ENABLE = " + value.Value;
    } catch {
      Status = "Set BRD_CAN_ENABLE failed.";
    }
  }

  partial void OnFactoryResetArmedChanged(bool value) {
    FactoryResetCommand.NotifyCanExecuteChanged();
  }

  [RelayCommand]
  private void StartEnumeration() {
    DoCommand(MAVLink.MAV_CMD.PREFLIGHT_UAVCAN, 1, 0, "Start Enumeration");
  }

  [RelayCommand]
  private void StopEnumeration() {
    DoCommand(MAVLink.MAV_CMD.PREFLIGHT_UAVCAN, 0, 0, "Stop Enumeration");
  }

  [RelayCommand]
  private void SaveConfig() {
    DoCommand(MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 1, 0, "Save All Config");
  }

  [RelayCommand(CanExecute = nameof(FactoryResetArmed))]
  private void FactoryReset() {
    DoCommand(MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 2, 0, "Reset config");
  }

  private void DoCommand(MAVLink.MAV_CMD cmd, float p1, float p2, string label) {
    if (!IsConnected) {
      Status = "Not connected — open a link first.";
      return;
    }
    try {
      _comPort.doCommand((byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, cmd, p1, p2, 0, 0, 0, 0, 0, false);
      Status = label + " sent.";
    } catch (Exception ex) {
      Status = label + " failed: " + ex.Message;
    }
  }

  private static List<KeyValuePair<int, string>> SafeOptions(string name, string fw) {
    try {
      return ParameterMetaDataRepository.GetParameterOptionsInt(name, fw) ?? new();
    } catch {
      return new();
    }
  }
}
