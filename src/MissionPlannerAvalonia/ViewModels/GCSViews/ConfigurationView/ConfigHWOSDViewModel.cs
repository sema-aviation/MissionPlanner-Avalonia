using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigHWOSDViewModel : ParamPageBase {

  private static readonly string[] _suffixes = {
      "EXT_STAT", "EXTRA1", "EXTRA2", "EXTRA3", "POSITION", "RAW_CTRL", "RAW_SENS", "RC_CHAN",
  };

  private static readonly string[] _streams = { "SR0", "SR1", "SR3" };

  [ObservableProperty]
  private string _status = "";

  public string Note =>
      "You only need to use this if you are having issue with your OSD not updating.";

  public ConfigHWOSDViewModel() {
    Title = "OSD";
    Intro = "MinimOSD telemetry helper.";
  }

  [RelayCommand]
  [System.Obsolete]
  private async Task EnableTelemetry() {
    if (comPort.BaseStream?.IsOpen != true) {
      Status = "offline — connect first.";
      return;
    }
    try {
      Status = "Setting stream rates…";
      await Task.Run(() => {
        foreach (var stream in _streams) {
          foreach (var suffix in _suffixes) {
            comPort.setParam(stream + "_" + suffix, 2);
          }
        }
      });
      Status = "✓ Telemetry streams enabled (2 Hz).";
    } catch (Exception ex) {
      Status = ex.Message;
    }
  }
}
