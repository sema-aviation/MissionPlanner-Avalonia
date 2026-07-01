using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MissionPlanner;
using MissionPlanner.Comms;

namespace MissionPlannerAvalonia.ViewModels;

public sealed class DroneCanBridge : IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private DroneCAN.DroneCAN? _can;
  private CommsInjection? _port;
  private bool _run;
  private int _subId = -1;

  public DroneCAN.DroneCAN? Can => _can;
  public bool IsOpen => _can != null;

  public bool Start(byte bus) {
    if (_comPort.BaseStream?.IsOpen != true) {
      return false;
    }

    _run = true;

    Task.Run(() => {
      Thread.Sleep(1000);
      while (_run) {
        try {
          _comPort.doCommand((byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent,
              MAVLink.MAV_CMD.CAN_FORWARD, bus, 0, 0, 0, 0, 0, 0, false);
        } catch {
        }

        if (_run) {
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
                (byte)DroneCAN.DroneCAN.dataLengthToDlc(payload.packet_data.Length),
                payload.packet_data),
            (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent);
      } else {
        _comPort.sendPacket(new MAVLink.mavlink_can_frame_t(
                BitConverter.ToUInt32(frame.packet_data, 0) + (frame.Extended ? 0x80000000 : 0),
                (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, (byte)(bus - 1),
                (byte)DroneCAN.DroneCAN.dataLengthToDlc(payload.packet_data.Length),
                payload.packet_data),
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

    try {
      can.StartSLCAN(port.BaseStream);
      can.SetupFileServer();
      can.SetupDynamicNodeAllocator();
    } catch {
      Stop();
      return false;
    }

    return true;
  }

  public void Stop() {
    _run = false;

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
  }

  public void Dispose() => Stop();
}
