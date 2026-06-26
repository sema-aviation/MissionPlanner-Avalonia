using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels;

// Backs the Connection Options dialog (mirrors MP's CTX "Connection Options" over the comms
// settings). Keeps to a handful of real, persisted settings — last-used baud, the GCS heartbeat
// toggle and the GCS system id — all round-tripped through Settings.Instance and saved on Apply.
// The GCS sysid is also pushed live into the link (MAVLinkInterface.gcssysid is static).
public partial class ConnectionOptionsViewModel : ViewModelBase {
  // Settings.Instance keys (persisted in the user config xml).
  private const string BaudKey = "baudrate";
  private const string HeartbeatKey = "CHK_GCSheartbeat";
  private const string GcsSysidKey = "GCS_sysid";

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
    SelectedBaud = Settings.Instance.GetInt32(BaudKey, 115200);
    SendGcsHeartbeat = Settings.Instance.GetBoolean(HeartbeatKey, true);
    GcsSysid = Settings.Instance.GetInt32(GcsSysidKey, MAVLinkInterface.gcssysid);
  }

  [RelayCommand]
  private void Apply() {
    Settings.Instance[BaudKey] = SelectedBaud.ToString();
    Settings.Instance[HeartbeatKey] = SendGcsHeartbeat.ToString();
    var sysid = GcsSysid is >= 1 and <= 255 ? GcsSysid : 255;
    Settings.Instance[GcsSysidKey] = sysid.ToString();
    MAVLinkInterface.gcssysid = (byte)sysid;
    Settings.Instance.Save();
    Status = "Saved.";
  }
}
