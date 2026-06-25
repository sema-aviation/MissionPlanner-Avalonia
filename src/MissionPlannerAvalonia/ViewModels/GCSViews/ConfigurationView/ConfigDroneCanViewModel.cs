using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

  public string[] BusOptions { get; } = { "MAVLink CAN1", "MAVLink CAN2" };

  [ObservableProperty]
  private int _selectedBusIndex;

  [ObservableProperty]
  private string _status = "Connect over the active MAVLink link to enumerate DroneCAN / UAVCAN nodes.";

  [ObservableProperty]
  private bool _isConnected;

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
