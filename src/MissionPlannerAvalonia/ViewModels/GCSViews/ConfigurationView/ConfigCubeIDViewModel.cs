using System;
using System.IO;
using System.Threading;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigCubeIDViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private const string _url =
      "https://firmware.cubepilot.org/UAVCAN/com.cubepilot.cubeid/1.0/serial_fw_update.bin";

  private volatile bool _done;
  private volatile bool _cancel;
  private double _progress;
  private uint _offset;
  private string _file = string.Empty;

  [ObservableProperty]
  private bool _forceBaud = true;

  [ObservableProperty]
  private bool _isBusy;

  [ObservableProperty]
  private bool _canUpload;

  [ObservableProperty]
  private bool _showPassthrough;

  [ObservableProperty]
  private int _progressPercent;

  [ObservableProperty]
  private string _status = "";

  // ponytail: upstream binds SERIAL passthrough to MAVlist[1,1].param; here they reflect the connected autopilot's params (normal CubeID passthrough).
  [ObservableProperty]
  private ParamField? _serialPass2;

  [ObservableProperty]
  private ParamField? _serialPassTimo;

  public ConfigCubeIDViewModel() {
    Activate();
  }

  public void Activate() {
    if (_comPort.compidcurrent != (int)MAVLink.MAV_COMPONENT.MAV_COMP_ID_ODID_TXRX_1) {
      if (_comPort.MAVlist.Contains(1, 1)) {
        ShowPassthrough = true;
        SerialPass2 = new ParamField("SERIAL_PASS2", "combo");
        SerialPassTimo = new ParamField("SERIAL_PASSTIMO");
      }
      CanUpload = false;
      Status = "Select the ODID/CubeID node from the component dropdown (top-right) to enable upload.";
    } else {
      CanUpload = true;
      Status = "Ready. Press Upload Firmware.";
    }
  }

  [RelayCommand]
  [Obsolete]
  private void UploadFirmware() {
    Start(string.Empty);
  }

  [Obsolete]
  public void UploadCustom(string path) {
    Start(path);
  }

  [RelayCommand]
  private void Cancel() {
    _cancel = true;
  }

  [Obsolete]
  private void Start(string path) {
    if (IsBusy) {
      return;
    }
    _done = false;
    _cancel = false;
    _progress = 0.0;
    _offset = 0;
    _file = path;
    IsBusy = true;
    ProgressPercent = 0;

    var t = new Thread(Worker) { IsBackground = true, Name = "CubeID FW" };
    t.Start();
  }

  private static uint Crc32Update(uint crc, byte[] data) {
    uint[] table = {
        0x00000000, 0x1db71064, 0x3b6e20c8, 0x26d930ac,
        0x76dc4190, 0x6b6b51f4, 0x4db26158, 0x5005713c,
        0xedb88320, 0xf00f9344, 0xd6d6a3e8, 0xcb61b38c,
        0x9b64c2b0, 0x86d3d2d4, 0xa00ae278, 0xbdbdf21c,
    };
    crc ^= 0xffffffff;
    foreach (var c in data) {
      crc = table[(crc ^ c) & 0x0f] ^ (crc >> 4);
      crc = table[(crc ^ ((uint)c >> 4)) & 0x0f] ^ (crc >> 4);
    }
    return crc ^ 0xffffffff;
  }

  [Obsolete]
  private void Worker() {
    try {
      if (_file == string.Empty) {
        _file = Path.GetTempFileName();
        if (!Download.getFilefromNet(_url, _file)) {
          Report(0, "Bad Download");
          return;
        }
      }

      var firmwareData = File.ReadAllBytes(_file);
      var firmwareSize = (uint)firmwareData.Length;
      var crc32 = Crc32Update(0, firmwareData);

      var seenResp = false;

      if (ForceBaud && _comPort.BaseStream != null) {
        _comPort.BaseStream.BaudRate = 57600;
      }

      var subid = _comPort.SubscribeToPacketType(
          MAVLink.MAVLINK_MSG_ID.CUBEPILOT_FIRMWARE_UPDATE_RESP, msg => {
            seenResp = true;
            var resp = (MAVLink.mavlink_cubepilot_firmware_update_resp_t)msg.data;

            _offset = resp.offset;
            _progress = resp.offset / (double)firmwareSize;

            if (resp.offset > firmwareSize) {
              _done = true;
              return true;
            }

            var rlen = (int)Math.Min(252, firmwareSize - _offset);
            if (rlen == 0) {
              _done = true;
              return true;
            }
            _comPort.generatePacket((int)MAVLink.MAVLINK_MSG_ID.ENCAPSULATED_DATA,
                new MAVLink.mavlink_encapsulated_data_t((ushort)(_offset / 252),
                    new ReadOnlySpan<byte>(firmwareData).Slice((int)_offset, rlen).ToArray()),
                _comPort.sysidcurrent, _comPort.compidcurrent);
            return true;
          }, (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, true);

      while (!_done && !_cancel) {
        _comPort.generatePacket((int)MAVLink.MAVLINK_MSG_ID.CUBEPILOT_FIRMWARE_UPDATE_START,
            new MAVLink.mavlink_cubepilot_firmware_update_start_t(firmwareSize, crc32,
                (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent),
            _comPort.sysidcurrent, _comPort.compidcurrent);

        Thread.Sleep(1000);
        Report((int)(_progress * 100), "Updating " + _offset + " Seen HW: " + seenResp);

        if (!seenResp) {
          continue;
        }

        if (_offset > firmwareSize) {
          _done = true;
          continue;
        }

        var len = (int)Math.Min(252, firmwareSize - _offset);
        if (len == 0) {
          _done = true;
          continue;
        }
        _comPort.generatePacket((int)MAVLink.MAVLINK_MSG_ID.ENCAPSULATED_DATA,
            new MAVLink.mavlink_encapsulated_data_t((ushort)(_offset / 252),
                new ReadOnlySpan<byte>(firmwareData).Slice((int)_offset, len).ToArray()),
            _comPort.sysidcurrent, _comPort.compidcurrent);
      }

      _comPort.UnSubscribeToPacketType(subid);
      Report(_cancel ? ProgressPercent : 100, _cancel ? "Cancelled" : "Update complete.");
    } catch (Exception ex) {
      Report(0, "Error: " + ex.Message);
    } finally {
      Dispatcher.UIThread.Post(() => IsBusy = false);
    }
  }

  private void Report(int percent, string status) {
    Dispatcher.UIThread.Post(() => {
      ProgressPercent = percent;
      Status = status;
    });
  }
}
