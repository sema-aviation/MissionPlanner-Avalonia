using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigHWESP8266ViewModel : ViewModelBase {
  private const byte UdpBridge = (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_UDP_BRIDGE;

  private readonly MAVLinkInterface _comPort = AppState.comPort;

  [ObservableProperty]
  private string _ssid = "";

  [ObservableProperty]
  private string _password = "";

  [ObservableProperty]
  private string _baud = "115200";

  [ObservableProperty]
  private string _channel = "11";

  [ObservableProperty]
  private bool _staMode;

  [ObservableProperty]
  private string _ipSta = "192.168.4.1";

  [ObservableProperty]
  private string _gatewaySta = "192.168.4.1";

  [ObservableProperty]
  private string _subnetSta = "255.255.255.0";

  [ObservableProperty]
  private string _details = "";

  [ObservableProperty]
  private string _status = "";

  [ObservableProperty]
  private bool _isLoaded;

  public string[] ChannelOptions { get; } = {
    "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13",
  };

  public string[] BaudOptions { get; } = {
    "9600", "19200", "38400", "57600", "115200", "230400", "460800", "921600",
  };

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;

  public ConfigHWESP8266ViewModel() {
    if (IsConnected) {
      _ = Task.Run(Activate);
    } else {
      Status = "Not connected.";
    }
  }

  private async Task Activate() {
    if (!IsConnected) {
      await Dispatcher.UIThread.InvokeAsync(() => Status = "Not connected.");
      return;
    }

    await Task.Run(() => _comPort.sendPacket(new MAVLink.mavlink_param_request_list_t() {
      target_system = 0,
      target_component = UdpBridge,
    }, _comPort.sysidcurrent, UdpBridge));

    await Task.Delay(2000);

    byte sysid = _comPort.MAV.sysid;
    var mav = _comPort.MAVlist[sysid, UdpBridge];

    if (mav == null || !mav.param.ContainsKey("WIFI_SSID1")) {
      await Dispatcher.UIThread.InvokeAsync(() => {
        IsLoaded = false;
        Status = "No ESP8266 / UDP-bridge component responded.";
      });
      return;
    }

    string ssid = UnpackString(mav.param, "WIFI_SSID");
    string password = UnpackString(mav.param, "WIFI_PASSWORD");
    string baud = mav.param["UART_BAUDRATE"].ToString();
    string channel = mav.param["WIFI_CHANNEL"].ToString();

    string debugEnabled = mav.param["DEBUG_ENABLED"].ToString();
    string wifiMode = mav.param["WIFI_MODE"].ToString();
    string wifiIpAddress = UnpackIp(mav.param, "WIFI_IPADDRESS");
    string wifiUdpHport = mav.param["WIFI_UDP_HPORT"].ToString();
    string wifiUdpCport = mav.param["WIFI_UDP_CPORT"].ToString();

    string ipSta = UnpackIp(mav.param, "WIFI_IPSTA");
    string gatewaySta = UnpackIp(mav.param, "WIFI_GATEWAYSTA");
    string subnetSta = UnpackIp(mav.param, "WIFI_SUBNET_STA");

    string details = string.Format(
      "DEBUG_ENABLED {0},\n" +
      "WIFI_MODE {1},\n" +
      "WIFI_IPADDRESS {2},\n" +
      "WIFI_UDP_HPORT {3},\n" +
      "WIFI_UDP_CPORT {4},\n" +
      "WIFI_IPSTA {5},\n" +
      "WIFI_GATEWAYSTA {6},\n" +
      "WIFI_SUBNET_STA {7}\n",
      debugEnabled, wifiMode, wifiIpAddress, wifiUdpHport, wifiUdpCport,
      ipSta, gatewaySta, subnetSta);

    await Dispatcher.UIThread.InvokeAsync(() => {
      Ssid = ssid;
      Password = password;
      Baud = baud;
      Channel = channel;
      IpSta = ipSta;
      GatewaySta = gatewaySta;
      SubnetSta = subnetSta;
      StaMode = wifiMode != "0";
      Details = details;
      IsLoaded = true;
      Status = "";
    });
  }

  private static string UnpackString(MAVLink.MAVLinkParamList param, string prefix) {
    return (Encoding.ASCII.GetString(param[prefix + "1"].data) +
            Encoding.ASCII.GetString(param[prefix + "2"].data) +
            Encoding.ASCII.GetString(param[prefix + "3"].data) +
            Encoding.ASCII.GetString(param[prefix + "4"].data)).TrimEnd('\0');
  }

  private static string UnpackIp(MAVLink.MAVLinkParamList param, string name) {
    return new IPAddress(BitConverter.GetBytes((int)param[name])).ToString();
  }

  private static byte[] StringToByteArray(string input, int start, int length) {
    var ans = Encoding.ASCII.GetBytes(input ?? "");
    Array.Resize(ref ans, start + length);
    byte[] dst = new byte[length];
    Array.ConstrainedCopy(ans, start, dst, 0, length);
    return dst;
  }

  private void SetU32(string name, string source, int start) {
    _comPort.setParam((byte)_comPort.sysidcurrent, UdpBridge, name,
      BitConverter.ToUInt32(StringToByteArray(source, start, 4), 0));
  }

  [RelayCommand]
  private async Task Save() {
    if (!IsConnected) {
      Status = "Not connected.";
      return;
    }

    Status = "Saving…";
    bool pass = await Task.Run(() => {
      try {
        _comPort.setParam((byte)_comPort.sysidcurrent, UdpBridge, "WIFI_CHANNEL", int.Parse(Channel));
        _comPort.setParam((byte)_comPort.sysidcurrent, UdpBridge, "UART_BAUDRATE", int.Parse(Baud));

        SetU32("WIFI_SSID1", Ssid, 0);
        SetU32("WIFI_SSID2", Ssid, 4);
        SetU32("WIFI_SSID3", Ssid, 8);
        SetU32("WIFI_SSID4", Ssid, 12);

        SetU32("WIFI_PASSWORD1", Password, 0);
        SetU32("WIFI_PASSWORD2", Password, 4);
        SetU32("WIFI_PASSWORD3", Password, 8);
        SetU32("WIFI_PASSWORD4", Password, 12);

        SetU32("WIFI_SSIDSTA1", Ssid, 0);
        SetU32("WIFI_SSIDSTA2", Ssid, 4);
        SetU32("WIFI_SSIDSTA3", Ssid, 8);
        SetU32("WIFI_SSIDSTA4", Ssid, 12);

        SetU32("WIFI_PWDSTA1", Password, 0);
        SetU32("WIFI_PWDSTA2", Password, 4);
        SetU32("WIFI_PWDSTA3", Password, 8);
        SetU32("WIFI_PWDSTA4", Password, 12);

        _comPort.setParam((byte)_comPort.sysidcurrent, UdpBridge, "WIFI_IPSTA",
          BitConverter.ToUInt32(IPAddress.Parse(IpSta).GetAddressBytes(), 0));
        _comPort.setParam((byte)_comPort.sysidcurrent, UdpBridge, "WIFI_GATEWAYSTA",
          BitConverter.ToUInt32(IPAddress.Parse(GatewaySta).GetAddressBytes(), 0));
        _comPort.setParam((byte)_comPort.sysidcurrent, UdpBridge, "WIFI_SUBNET_STA",
          BitConverter.ToUInt32(IPAddress.Parse(SubnetSta).GetAddressBytes(), 0));

        _comPort.setParam((byte)_comPort.sysidcurrent, UdpBridge, "WIFI_MODE", StaMode ? 1 : 0);

        bool ok = _comPort.doCommand((byte)_comPort.sysidcurrent, UdpBridge,
          MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 1, 0, 0, 0, 0, 0, 0);
        ok = ok & _comPort.doCommand((byte)_comPort.sysidcurrent, UdpBridge,
          MAVLink.MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN, 0, 1, 0, 0, 0, 0, 0);
        return ok;
      } catch {
        return false;
      }
    });

    Status = pass ? "Programmed OK." : "Error setting parameter.";
  }

  [RelayCommand]
  private async Task ResetDefaults() {
    if (!IsConnected) {
      Status = "Not connected.";
      return;
    }

    Status = "Resetting to defaults…";
    bool pass = await Task.Run(() => {
      try {
        if (!_comPort.doCommand((byte)_comPort.sysidcurrent, UdpBridge,
          MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 2, 0, 0, 0, 0, 0, 0)) {
          return false;
        }
        return _comPort.doCommand((byte)_comPort.sysidcurrent, UdpBridge,
          MAVLink.MAV_CMD.PREFLIGHT_STORAGE, 1, 0, 0, 0, 0, 0, 0);
      } catch {
        return false;
      }
    });

    if (pass) {
      await Activate();
      Status = "Programmed OK.";
    } else {
      Status = "Error setting parameter.";
    }
  }
}
