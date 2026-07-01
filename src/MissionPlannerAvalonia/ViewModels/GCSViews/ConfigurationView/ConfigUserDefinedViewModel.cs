using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigUserDefinedViewModel : ViewModelBase {
  private static readonly char[] _separators = { ',', '\n', '\r', ' ' };

  private static readonly string[] _defaultOptions = {
    "CH6_OPT", "CH7_OPT", "CH8_OPT", "CH9_OPT", "CH10_OPT", "CH11_OPT",
    "CH12_OPT", "CH13_OPT", "CH14_OPT", "CH15_OPT", "CH16_OPT",
    "RC6_OPTION", "RC7_OPTION", "RC8_OPTION", "RC9_OPTION", "RC10_OPTION",
    "RC11_OPTION", "RC12_OPTION", "RC13_OPTION", "RC14_OPTION", "RC15_OPTION",
    "RC16_OPTION",
  };

  private readonly MAVLinkInterface _comPort = AppState.comPort;

  public string[] Options { get; private set; } = _defaultOptions;
  public ObservableCollection<ParamField> Fields { get; } = new();

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true;
  public string OptionsText => string.Join("\r\n", Options);

  public ConfigUserDefinedViewModel() {
    if (Settings.Instance.ContainsKey("UserParams")) {
      Options = Settings.Instance["UserParams"]
          .Split(_separators, StringSplitOptions.RemoveEmptyEntries);
    }

    LoadOptions();
  }

  public void LoadOptions() {
    Fields.Clear();
    foreach (var option in Options) {
      if (!_comPort.MAV.param.ContainsKey(option)) {
        continue;
      }

      Fields.Add(new ParamField(option));
    }
  }

  public void ApplyOptions(string raw) {
    Options = raw.Split(_separators, StringSplitOptions.RemoveEmptyEntries)
        .Select(o => o.Trim())
        .Where(o => o.Length > 0)
        .Distinct()
        .ToArray();

    if (Options.Length > 0) {
      Settings.Instance["UserParams"] = string.Join(",", Options);
    }

    LoadOptions();
  }

  [RelayCommand]
  private async Task Refresh() {
    if (IsConnected) {
      await Task.Run(() => _comPort.getParamList());
    }

    LoadOptions();
  }
}
