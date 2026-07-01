using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

public partial class ConnectionOptionsViewModel : ViewModelBase {

  private const string _baudKey = "baudrate";
  private const string _heartbeatKey = "CHK_GCSheartbeat";
  private const string _gcsSysidKey = "GCS_sysid";

  public ObservableCollection<int> Bauds { get; } =
      new() { 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };

  [ObservableProperty]
  private int _selectedBaud;

  [ObservableProperty]
  private bool _sendGcsHeartbeat;

  [ObservableProperty]
  private int _gcsSysid;

  [ObservableProperty]
  private string _status = "";

  public ConnectionOptionsViewModel() {
    SelectedBaud = Settings.Instance.GetInt32(_baudKey, 115200);
    SendGcsHeartbeat = Settings.Instance.GetBoolean(_heartbeatKey, true);
    GcsSysid = Settings.Instance.GetInt32(_gcsSysidKey, MAVLinkInterface.gcssysid);
  }

  [RelayCommand]
  private void Apply() {
    Settings.Instance[_baudKey] = SelectedBaud.ToString();
    Settings.Instance[_heartbeatKey] = SendGcsHeartbeat.ToString();
    var sysid = GcsSysid is >= 1 and <= 255 ? GcsSysid : 255;
    Settings.Instance[_gcsSysidKey] = sysid.ToString();
    MAVLinkInterface.gcssysid = (byte)sysid;
    Settings.Instance.Save();
    Status = "Saved.";
  }
}
