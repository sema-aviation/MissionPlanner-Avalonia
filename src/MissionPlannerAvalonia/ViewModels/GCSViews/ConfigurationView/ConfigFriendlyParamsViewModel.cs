using System;
using System.Linq;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public class ConfigFriendlyParamsViewModel : ParamPageBase {
  private readonly bool _advanced;

  public ConfigFriendlyParamsViewModel(bool advanced) {
    _advanced = advanced;
    Title = advanced ? "Advanced Params" : "Standard Params";
    Intro = "Human-readable parameters with descriptions. Connect, then Refresh.";
    Build();
  }

  protected override void OnRefreshed() => Build();

  private void Build() {
    Fields.Clear();
    var fw = comPort.MAV.cs.firmware.ToString();
    var wanted = _advanced ? ParameterMetaDataConstants.Advanced : ParameterMetaDataConstants.Standard;

    foreach (var name in comPort.MAV.param.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)) {
      string display,
          mode;
      try {
        display =
            ParameterMetaDataRepository.GetParameterMetaData(
                name,
                ParameterMetaDataConstants.DisplayName,
                fw
            ) ?? "";
        mode =
            ParameterMetaDataRepository.GetParameterMetaData(
                name,
                ParameterMetaDataConstants.User,
                fw
            ) ?? "";
      } catch {
        continue;
      }

      if (string.IsNullOrEmpty(display)) {
        continue;
      }

      bool isAdv = string.Equals(
          mode,
          ParameterMetaDataConstants.Advanced,
          StringComparison.OrdinalIgnoreCase
      );
      if (_advanced != isAdv) {
        continue;
      }

      F(name);
    }
  }
}
