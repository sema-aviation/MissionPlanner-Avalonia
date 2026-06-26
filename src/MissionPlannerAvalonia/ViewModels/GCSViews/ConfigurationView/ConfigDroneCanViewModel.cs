using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DroneCAN;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigDroneCanViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private DroneCAN.DroneCAN? _can;
  private CommsInjection? _port;
  private bool _mavlinkCanRun;
  private byte _busInUse;
  private int _subId = -1;

  public ObservableCollection<DroneCanNode> Nodes { get; } = new();

  // Per-node parameter list (uavcan.protocol.param.GetSet) for the selected node.
  public ObservableCollection<DroneCanParam> NodeParams { get; } = new();

  public string[] BusOptions { get; } = { "MAVLink CAN1", "MAVLink CAN2" };

  [ObservableProperty]
  private int _selectedBusIndex;

  [ObservableProperty]
  private string _status = "Connect over the active MAVLink link to enumerate DroneCAN / UAVCAN nodes.";

  [ObservableProperty]
  private bool _isConnected;

  [ObservableProperty]
  private DroneCanNode? _selectedNode;

  [ObservableProperty]
  private string _nodeStatus = "Select a node, then Get Parameters / Restart / Update Firmware.";

  [ObservableProperty]
  private bool _isBusy;

  private CancellationTokenSource? _fwCancel;

  partial void OnSelectedNodeChanged(DroneCanNode? value) {
    NodeParams.Clear();
    NodeStatus = value == null
        ? "Select a node, then Get Parameters / Restart / Update Firmware."
        : $"Node {value.Id} ({value.Name}) selected.";
  }

  public string ConnectLabel => IsConnected ? "Disconnect" : "Connect";

  partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectLabel));

  [RelayCommand]
  private void ToggleConnect() {
    if (IsConnected) {
      Disconnect();
      return;
    }

    if (_comPort.BaseStream?.IsOpen != true) {
      Status = "Not connected — open the MAVLink link first.";
      return;
    }

    Nodes.Clear();
    byte bus = (byte)(SelectedBusIndex == 1 ? 2 : 1);
    StartMavlinkCAN(bus);
    IsConnected = true;
    Status = $"Listening for nodes on MAVLink CAN{bus}…";
  }

  [RelayCommand]
  private void Refresh() {
    if (!IsConnected || _can == null) {
      Status = "Connect first to refresh the node list.";
      return;
    }

    Nodes.Clear();
    Status = "Re-requesting node status…";
  }

  // ---- per-node parameter editor (uavcan.protocol.param.GetSet) ----

  [RelayCommand]
  private async Task GetParameters() {
    var can = _can;
    var node = SelectedNode;
    if (can == null || node == null) {
      NodeStatus = "Connect and select a node first.";
      return;
    }

    IsBusy = true;
    NodeStatus = $"Requesting parameters from node {node.Id}…";
    var id = node.Id;

    List<DroneCAN.DroneCAN.uavcan_protocol_param_GetSet_res> list = new();
    try {
      await Task.Run(() => list = can.GetParameters(id));
    } catch (Exception ex) {
      NodeStatus = "Error getting parameters: " + ex.Message;
      IsBusy = false;
      return;
    }

    NodeParams.Clear();
    foreach (var p in list) {
      var name = Encoding.ASCII.GetString(p.name, 0, p.name_len);
      if (string.IsNullOrEmpty(name)) {
        continue;
      }

      NodeParams.Add(new DroneCanParam {
        Name = name,
        Value = Convert.ToString(p.value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        OriginalValue = Convert.ToString(p.value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        Min = Convert.ToString(p.min_value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        Max = Convert.ToString(p.max_value.GetValue(), CultureInfo.InvariantCulture) ?? "",
        Default = Convert.ToString(p.default_value.GetValue(), CultureInfo.InvariantCulture) ?? "",
      });
    }

    NodeStatus = $"Loaded {NodeParams.Count} parameters from node {node.Id}.";
    IsBusy = false;
  }

  [RelayCommand]
  private async Task WriteParameters() {
    var can = _can;
    var node = SelectedNode;
    if (can == null || node == null) {
      NodeStatus = "Connect and select a node first.";
      return;
    }

    var changed = NodeParams.Where(p => p.IsDirty).ToList();
    if (changed.Count == 0) {
      NodeStatus = "No modified parameters to write.";
      return;
    }

    IsBusy = true;
    var id = node.Id;
    int failed = 0;

    await Task.Run(() => {
      foreach (var p in changed) {
        try {
          // numeric params must be passed as a number; SetParameter coerces strings to 0.
          object value = double.TryParse(p.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
              ? d
              : p.Value;
          if (!can.SetParameter(id, p.Name, value)) {
            failed++;
          }
        } catch {
          failed++;
        }
      }

      // commit to non-volatile memory (ExecuteOpcode SAVE)
      try {
        can.SaveConfig(id);
      } catch {
      }
    });

    foreach (var p in changed) {
      p.OriginalValue = p.Value;
    }

    NodeStatus = failed == 0
        ? $"Wrote {changed.Count} parameters and saved to flash."
        : $"Wrote parameters with {failed} failure(s); saved to flash.";
    IsBusy = false;
  }

  [RelayCommand]
  private async Task SaveConfig() {
    var can = _can;
    var node = SelectedNode;
    if (can == null || node == null) {
      NodeStatus = "Connect and select a node first.";
      return;
    }

    IsBusy = true;
    var id = node.Id;
    bool ok = false;
    await Task.Run(() => {
      try {
        ok = can.SaveConfig(id);
      } catch {
      }
    });
    NodeStatus = ok ? "Parameters committed to non-volatile memory." : "Failed to save parameters.";
    IsBusy = false;
  }

  [RelayCommand]
  private async Task EraseConfig() {
    var can = _can;
    var node = SelectedNode;
    if (can == null || node == null) {
      NodeStatus = "Connect and select a node first.";
      return;
    }

    IsBusy = true;
    var id = node.Id;
    bool ok = false;
    await Task.Run(() => {
      try {
        ok = can.ExecuteOpCode(id, (byte)DroneCAN.DroneCAN.uavcan_protocol_param_ExecuteOpcode_req
            .UAVCAN_PROTOCOL_PARAM_EXECUTEOPCODE_REQ_OPCODE_ERASE);
      } catch {
      }
    });
    NodeStatus = ok ? "Erased parameters to defaults (node restart may be required)." : "Failed to erase parameters.";
    IsBusy = false;
  }

  [RelayCommand]
  private async Task RestartNode() {
    var can = _can;
    var node = SelectedNode;
    if (can == null || node == null) {
      NodeStatus = "Connect and select a node first.";
      return;
    }

    IsBusy = true;
    var id = node.Id;
    bool ok = false;
    await Task.Run(() => {
      try {
        ok = can.RestartNode(id);
      } catch {
      }
    });
    NodeStatus = ok ? $"Node {id} restart requested." : $"Node {id} did not acknowledge restart.";
    IsBusy = false;
  }

  // Called from the view after the user picks a firmware file (.bin / .apj).
  // Serves the file over the DroneCAN file server and issues BeginFirmwareUpdate
  // (DroneCAN.Update does ServeFile + uavcan.protocol.file.BeginFirmwareUpdate).
  public async Task UpdateFirmwareAsync(string firmwarePath) {
    var can = _can;
    var node = SelectedNode;
    if (can == null || node == null) {
      NodeStatus = "Connect and select a node first.";
      return;
    }

    if (string.IsNullOrEmpty(firmwarePath) || !File.Exists(firmwarePath)) {
      NodeStatus = "Firmware file not found.";
      return;
    }

    IsBusy = true;
    var id = node.Id;
    _fwCancel = new CancellationTokenSource();

    DroneCAN.DroneCAN.FileSendProgressArgs progress = (n, f, p) =>
        Dispatcher.UIThread.Post(() => NodeStatus = $"Firmware {f}: {p:0}%");
    DroneCAN.DroneCAN.FileSendCompleteArgs complete = (n, f) =>
        Dispatcher.UIThread.Post(() => NodeStatus = "Firmware send complete.");

    can.FileSendProgress += progress;
    can.FileSendComplete += complete;

    try {
      var devicename = can.GetNodeName(id);
      await Task.Run(() => {
        var file = firmwarePath;
        // .apj carries the image inside JSON; extract it to a raw .bin first.
        if (file.ToLowerInvariant().EndsWith(".apj")) {
          var fw = px4uploader.Firmware.ProcessFirmware(file);
          var tmp = Path.GetTempFileName();
          File.WriteAllBytes(tmp, fw.imagebyte);
          file = tmp;
        }

        can.Update(id, devicename, 0, file, _fwCancel.Token);
      });
      NodeStatus = $"Firmware update started for node {id} ({devicename}).";
    } catch (Exception ex) {
      NodeStatus = "Firmware update failed: " + ex.Message;
    } finally {
      can.FileSendProgress -= progress;
      can.FileSendComplete -= complete;
      IsBusy = false;
    }
  }

  private void StartMavlinkCAN(byte bus) {
    _busInUse = bus;
    _mavlinkCanRun = true;

    Task.Run(() => {
      Thread.Sleep(1000);
      while (_mavlinkCanRun) {
        try {
          _comPort.doCommand((byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent,
              MAVLink.MAV_CMD.CAN_FORWARD, bus, 0, 0, 0, 0, 0, 0, false);
        } catch {
        }

        if (_mavlinkCanRun) {
          Thread.Sleep(1000);
        }
      }
    });

    _port = new CommsInjection();
    _can = new DroneCAN.DroneCAN { SourceNode = 127 };

    var can = _can;
    var port = _port;

    can.FrameReceived += (frame, payload) => {
      if (payload.packet_data.Length > 8) {
        _comPort.sendPacket(new MAVLink.mavlink_canfd_frame_t(
                BitConverter.ToUInt32(frame.packet_data, 0) + (frame.Extended ? 0x80000000 : 0),
                (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, (byte)(bus - 1),
                (byte)DroneCAN.DroneCAN.dataLengthToDlc(payload.packet_data.Length), payload.packet_data),
            (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent);
      } else {
        _comPort.sendPacket(new MAVLink.mavlink_can_frame_t(
                BitConverter.ToUInt32(frame.packet_data, 0) + (frame.Extended ? 0x80000000 : 0),
                (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, (byte)(bus - 1),
                (byte)DroneCAN.DroneCAN.dataLengthToDlc(payload.packet_data.Length), payload.packet_data),
            (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent);
      }
    };

    port.WriteCallback += (_, bytes) => {
      var lines = Encoding.ASCII.GetString(bytes.ToArray())
          .Split(new[] { '\r' }, StringSplitOptions.RemoveEmptyEntries);
      foreach (var line in lines) {
        can.ReadMessageSLCAN(line);
      }
    };

    _subId = _comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.CAN_FRAME, m => {
      if (m.msgid == (uint)MAVLink.MAVLINK_MSG_ID.CAN_FRAME) {
        var pkt = (MAVLink.mavlink_can_frame_t)m.data;
        var cf = new DroneCAN.CANFrame(BitConverter.GetBytes(pkt.id));
        var payld = new DroneCAN.CANPayload(pkt.data);
        var ans = string.Format("{0}{1}{2}{3}\r", 'T', cf.ToHex(), pkt.len.ToString("X"),
            payld.ToHex(DroneCAN.DroneCAN.dlcToDataLength(pkt.len)));
        port.AppendBuffer(Encoding.ASCII.GetBytes(ans));
      } else if (m.msgid == (uint)MAVLink.MAVLINK_MSG_ID.CANFD_FRAME) {
        var pkt = (MAVLink.mavlink_canfd_frame_t)m.data;
        var cf = new DroneCAN.CANFrame(BitConverter.GetBytes(pkt.id));
        var payld = new DroneCAN.CANPayload(pkt.data);
        var ans = string.Format("{0}{1}{2}{3}\r", 'B', cf.ToHex(), pkt.len.ToString("X"),
            payld.ToHex(DroneCAN.DroneCAN.dlcToDataLength(pkt.len)));
        port.AppendBuffer(Encoding.ASCII.GetBytes(ans));
      }

      return true;
    }, (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, true);

    can.NodeAdded += (id, msg) => {
      Dispatcher.UIThread.Post(() => {
        if (Nodes.Any(n => n.Id == id)) {
          return;
        }

        Nodes.Add(new DroneCanNode {
          Id = id,
          Name = "?",
          Health = HealthString(msg.health),
          Mode = ModeString(msg.mode),
          Uptime = TimeSpan.FromSeconds(msg.uptime_sec),
        });
      });
    };

    can.MessageReceived += (frame, msg, transferID) => {
      if (msg is DroneCAN.DroneCAN.uavcan_protocol_NodeStatus ns) {
        Dispatcher.UIThread.Post(() => {
          foreach (var item in Nodes.Where(n => n.Id == frame.SourceNode)) {
            item.Health = HealthString(ns.health);
            item.Mode = ModeString(ns.mode);
            item.Uptime = TimeSpan.FromSeconds(ns.uptime_sec);
          }
        });
      } else if (msg is DroneCAN.DroneCAN.uavcan_protocol_GetNodeInfo_res gnires) {
        Dispatcher.UIThread.Post(() => {
          foreach (var item in Nodes.Where(n => n.Id == frame.SourceNode)) {
            item.Name = Encoding.ASCII.GetString(gnires.name, 0, gnires.name_len);
            item.SoftwareVersion = gnires.software_version.major + "." + gnires.software_version.minor +
                                   "." + gnires.software_version.vcs_commit.ToString("X");
            item.HardwareVersion = gnires.hardware_version.major + "." + gnires.hardware_version.minor;
          }
        });
      }
    };

    try {
      can.StartSLCAN(port.BaseStream);
      can.SetupFileServer();
      can.SetupDynamicNodeAllocator();
    } catch (Exception ex) {
      Dispatcher.UIThread.Post(() => Status = "CAN start failed: " + ex.Message);
    }
  }

  private static string HealthString(byte health) {
    return health switch {
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_HEALTH_OK => "OK",
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_HEALTH_WARNING => "WARNING",
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_HEALTH_ERROR => "ERROR",
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_HEALTH_CRITICAL => "CRITICAL",
      _ => health.ToString(),
    };
  }

  private static string ModeString(byte mode) {
    return mode switch {
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_MODE_OPERATIONAL => "OPERATIONAL",
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_MODE_INITIALIZATION => "INITIALIZATION",
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_MODE_MAINTENANCE => "MAINTENANCE",
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_MODE_SOFTWARE_UPDATE => "SOFTWARE_UPDATE",
      (byte)DroneCAN.DroneCAN.uavcan_protocol_NodeStatus.UAVCAN_PROTOCOL_NODESTATUS_MODE_OFFLINE => "OFFLINE",
      _ => mode.ToString(),
    };
  }

  private void Disconnect() {
    _mavlinkCanRun = false;

    try {
      _fwCancel?.Cancel();
    } catch {
    }

    if (_subId != -1) {
      try {
        _comPort.UnSubscribeToPacketType(_subId);
      } catch {
      }

      _subId = -1;
    }

    try {
      _can?.Stop(false);
    } catch {
    }

    _can = null;

    try {
      _port?.Close();
    } catch {
    }

    _port = null;
    IsConnected = false;
    NodeParams.Clear();
    SelectedNode = null;
    Status = "Disconnected.";
  }

  public void Dispose() => Disconnect();
}

public partial class DroneCanNode : ObservableObject {
  [ObservableProperty]
  private byte _id;

  [ObservableProperty]
  private string _name = "?";

  [ObservableProperty]
  private string _health = "";

  [ObservableProperty]
  private string _mode = "";

  [ObservableProperty]
  private TimeSpan _uptime;

  [ObservableProperty]
  private string _hardwareVersion = "";

  [ObservableProperty]
  private string _softwareVersion = "";
}

public partial class DroneCanParam : ObservableObject {
  [ObservableProperty]
  private string _name = "";

  [ObservableProperty]
  private string _value = "";

  [ObservableProperty]
  private string _min = "";

  [ObservableProperty]
  private string _max = "";

  [ObservableProperty]
  private string _default = "";

  // Snapshot of the on-device value so we can detect edits before writing.
  public string OriginalValue { get; set; } = "";

  public bool IsDirty => !string.Equals(Value, OriginalValue, StringComparison.Ordinal);
}
