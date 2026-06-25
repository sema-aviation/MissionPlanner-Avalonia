using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigPlannerAdvViewModel : ViewModelBase {
  public ObservableCollection<SettingRow> Params { get; } = new();

  public ConfigPlannerAdvViewModel() {
    Activate();
  }

  [RelayCommand]
  public void Activate() {
    Params.Clear();
    foreach (var key in Settings.Instance.Keys.OrderBy(k => k, System.StringComparer.OrdinalIgnoreCase)) {
      Params.Add(new SettingRow(key, Settings.Instance[key]));
    }
  }
}

public record SettingRow(string Name, string? Value);
