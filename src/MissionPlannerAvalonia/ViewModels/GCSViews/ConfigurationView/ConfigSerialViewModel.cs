using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigSerialViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public ObservableCollection<SerialPortRow> Ports { get; } = new();

  [ObservableProperty]
  private string _note = "Note: Changes to the serial port settings will not take effect until the board is rebooted.";

  public ConfigSerialViewModel() {
    Activate();
  }

  public void Activate() {
    Ports.Clear();

    // Divergence from upstream: the uarts.txt MAVFtp lookup is skipped; rows are derived
    // purely from the SERIALx_* parameters already present in comPort.MAV.param.
    int serialPorts = 0;
    foreach (var key in _comPort.MAV.param.Keys) {
      if (key.StartsWith("SERIAL") && key.EndsWith("_BAUD")) {
        if (int.TryParse(key.Substring(6, 1), out var port) && port > serialPorts) {
          serialPorts = port;
        }
      }
    }

    var fw = _comPort.MAV.cs.firmware.ToString();
    for (int i = 1; i <= serialPorts; i++) {
      string portName = "SERIAL" + i;
      if (!_comPort.MAV.param.ContainsKey(portName + "_BAUD")) {
        continue;
      }

      string label = "SERIAL PORT " + i;
      string ctsrts = "BRD_SER" + i + "_RTSCTS";
      if (_comPort.MAV.param.ContainsKey(ctsrts)) {
        var cts = _comPort.MAV.param[ctsrts].Value;
        if (cts == 1) {
          label += " (RTS/CTS)";
        } else if (cts == 2) {
          label += " (RTS/CTS Auto)";
        }
      }

      Ports.Add(new SerialPortRow(_comPort, portName, label, fw));
    }
  }
}

public partial class SerialPortRow : ObservableObject {
  private readonly MAVLinkInterface _comPort;
  private readonly string _fw;
  private bool _suppress;

  public string PortName { get; }
  public string Label { get; }

  public ObservableCollection<ParamOption> BaudOptions { get; } = new();
  public ObservableCollection<ParamOption> ProtocolOptions { get; } = new();

  public bool HasOptions { get; }

  [ObservableProperty]
  private ParamOption? _selectedBaud;

  [ObservableProperty]
  private ParamOption? _selectedProtocol;

  [ObservableProperty]
  private double _optionsValue;

  [ObservableProperty]
  private string _optionsText = "";

  [ObservableProperty]
  private string _status = "";

  public SerialPortRow(MAVLinkInterface comPort, string portName, string label, string fw) {
    _comPort = comPort;
    _fw = fw;
    PortName = portName;
    Label = label;

    _suppress = true;
    foreach (var kv in SafeOptions(portName + "_BAUD", fw)) {
      BaudOptions.Add(new ParamOption(kv.Key, kv.Value));
    }
    foreach (var kv in SafeOptions(portName + "_PROTOCOL", fw)) {
      ProtocolOptions.Add(new ParamOption(kv.Key, kv.Value));
    }

    if (comPort.MAV.param.ContainsKey(portName + "_BAUD")) {
      var v = (int)Math.Round(comPort.MAV.param[portName + "_BAUD"].Value);
      SelectedBaud = BaudOptions.FirstOrDefault(o => o.Value == v);
    }
    if (comPort.MAV.param.ContainsKey(portName + "_PROTOCOL")) {
      var v = (int)Math.Round(comPort.MAV.param[portName + "_PROTOCOL"].Value);
      SelectedProtocol = ProtocolOptions.FirstOrDefault(o => o.Value == v);
    }
    if (comPort.MAV.param.ContainsKey(portName + "_OPTIONS")) {
      HasOptions = true;
      OptionsValue = comPort.MAV.param[portName + "_OPTIONS"].Value;
      UpdateOptionsText();
    }
    _suppress = false;
  }

  partial void OnSelectedBaudChanged(ParamOption? value) {
    if (!_suppress && value != null)
      SetParam(PortName + "_BAUD", value.Value);
  }

  partial void OnSelectedProtocolChanged(ParamOption? value) {
    if (!_suppress && value != null)
      SetParam(PortName + "_PROTOCOL", value.Value);
  }

  partial void OnOptionsValueChanged(double value) {
    if (_suppress)
      return;
    SetParam(PortName + "_OPTIONS", value);
    UpdateOptionsText();
  }

  private void UpdateOptionsText() {
    var binlist = SafeBitMask(PortName + "_OPTIONS", _fw);
    var val = (uint)OptionsValue;
    OptionsText = string.Join(" / ", binlist.Where(b => (val & (1 << b.Key)) > 0).Select(b => b.Value));
  }

  private void SetParam(string name, double value) {
    if (_comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }
    try {
      bool ok = _comPort.setParam((byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, name, value);
      Status = ok ? "✓" : "write failed";
    } catch (Exception ex) {
      Status = ex.Message;
    }
  }

  private static List<KeyValuePair<int, string>> SafeOptions(string name, string fw) {
    try {
      return ParameterMetaDataRepository.GetParameterOptionsInt(name, fw) ?? new();
    } catch {
      return new();
    }
  }

  private static List<KeyValuePair<int, string>> SafeBitMask(string name, string fw) {
    try {
      return ParameterMetaDataRepository.GetParameterBitMaskInt(name, fw) ?? new();
    } catch {
      return new();
    }
  }
}
