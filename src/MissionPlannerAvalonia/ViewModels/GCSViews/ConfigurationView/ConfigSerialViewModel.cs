using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public record SerialOptionRule(int Baudrate, int Options, string Comment);

public partial class ConfigSerialViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;

  // Mirror of upstream SerialOptionRules.json (keyed by SERIALx_PROTOCOL value). The upstream file
  // lives in the submodule but is not bundled into the app output (bundling would require a csproj
  // edit outside this page's owned files), so the rules are embedded here for 1:1 behaviour.
  public static readonly IReadOnlyDictionary<int, SerialOptionRule> OptionRules =
      new Dictionary<int, SerialOptionRule> {
        [1] = new SerialOptionRule(115, 0,
            "If connecting a Mavlink sensor, consider setting 'Do not forward Mavlink to/from'"),
        [2] = new SerialOptionRule(-1, 0,
            "If connecting a Mavlink sensor, consider setting 'Do not forward Mavlink to/from'"),
      };

  public ObservableCollection<SerialPortRow> Ports { get; } = new();

  [ObservableProperty]
  private string _note = "Note: Changes to the serial port settings will not take effect until the board is rebooted.";

  [ObservableProperty]
  private string _warning = "";

  public bool HasWarning => Warning.Length > 0;

  partial void OnWarningChanged(string value) => OnPropertyChanged(nameof(HasWarning));

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

      Ports.Add(new SerialPortRow(this, _comPort, portName, label, fw));
    }

    RecomputeStatus("");
  }

  // Apply baud/options presets when a protocol changes, then refresh the status line.
  internal void OnProtocolChanged(SerialPortRow row, int protocol) {
    string comment = "";
    if (OptionRules.TryGetValue(protocol, out var rule)) {
      if (rule.Baudrate > -1) {
        var opt = row.BaudOptions.FirstOrDefault(o => o.Value == rule.Baudrate);
        if (opt != null) {
          row.SelectedBaud = opt;
        }
      }
      if (rule.Options > -1 && row.HasOptions) {
        row.SetOptionsFromRule(rule.Options);
      }
      comment = row.PortName + " : " + rule.Comment;
    }
    RecomputeStatus(comment);
  }

  // ArduPilot allows MAVLINK_COMM_NUM_BUFFERS (5) mavlink ports including USB, so only 4 serial
  // ports may carry MAVLink (protocol 1 or 2) before the warning is shown.
  private void RecomputeStatus(string comment) {
    int mavlinkPorts = Ports.Count(p =>
        p.SelectedProtocol is { Value: 1 } || p.SelectedProtocol is { Value: 2 });

    string w = comment;
    if (mavlinkPorts >= 4) {
      if (w.Length > 0) {
        w += "\n";
      }
      w += "Warning: Maximum number of Mavlink ports are 5 including the USB port!";
    }
    Warning = w;
  }
}

public partial class SerialBitOption : ObservableObject {
  private readonly SerialPortRow _owner;

  public int Bit { get; }
  public string Label { get; }

  [ObservableProperty]
  private bool _isSet;

  public SerialBitOption(SerialPortRow owner, int bit, string label) {
    _owner = owner;
    Bit = bit;
    Label = $"{bit}: {label}";
  }

  partial void OnIsSetChanged(bool value) => _owner.OnBitToggled();
}

public partial class SerialPortRow : ObservableObject {
  private readonly ConfigSerialViewModel _vm;
  private readonly MAVLinkInterface _comPort;
  private readonly string _fw;
  private bool _suppress;

  public string PortName { get; }
  public string Label { get; }

  public ObservableCollection<ParamOption> BaudOptions { get; } = new();
  public ObservableCollection<ParamOption> ProtocolOptions { get; } = new();
  public ObservableCollection<SerialBitOption> OptionBits { get; } = new();

  public bool HasOptions { get; }
  public bool HasBits => OptionBits.Count > 0;

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

  public SerialPortRow(ConfigSerialViewModel vm, MAVLinkInterface comPort, string portName,
      string label, string fw) {
    _vm = vm;
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
      foreach (var kv in SafeBitMask(portName + "_OPTIONS", fw)) {
        OptionBits.Add(new SerialBitOption(this, kv.Key, kv.Value));
      }
      OptionsValue = comPort.MAV.param[portName + "_OPTIONS"].Value;
      SyncBits((uint)OptionsValue);
      UpdateOptionsText();
    }
    _suppress = false;
  }

  partial void OnSelectedBaudChanged(ParamOption? value) {
    if (!_suppress && value != null)
      SetParam(PortName + "_BAUD", value.Value);
  }

  partial void OnSelectedProtocolChanged(ParamOption? value) {
    if (_suppress || value == null)
      return;
    SetParam(PortName + "_PROTOCOL", value.Value);
    _vm.OnProtocolChanged(this, value.Value);
  }

  partial void OnOptionsValueChanged(double value) {
    if (_suppress)
      return;
    SetParam(PortName + "_OPTIONS", value);
    SyncBits((uint)value);
    UpdateOptionsText();
  }

  // Called when a bitmask checkbox toggles: rebuild the numeric value from the set bits.
  internal void OnBitToggled() {
    if (_suppress)
      return;
    long v = 0;
    foreach (var b in OptionBits) {
      if (b.IsSet) {
        v |= 1L << b.Bit;
      }
    }
    OptionsValue = v;
  }

  // Applied by the parent VM when a protocol preset carries an options byte.
  public void SetOptionsFromRule(int options) {
    OptionsValue = options;
  }

  private void SyncBits(uint value) {
    _suppress = true;
    foreach (var b in OptionBits) {
      b.IsSet = (value & (1u << b.Bit)) != 0;
    }
    _suppress = false;
  }

  private void UpdateOptionsText() {
    OptionsText = string.Join(" / ", OptionBits.Where(b => b.IsSet).Select(b => b.Label));
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
