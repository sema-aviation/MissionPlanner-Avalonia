using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Utilities;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// Upstream ConfigRawParams / friendly params. Adds: debounced search box, favourite
// ordering (star, persisted via Settings.Instance), bitmask editor + out-of-range
// highlight (the latter two live in the shared ParamField / ParamFieldsView).
public partial class ConfigFriendlyParamsViewModel : ParamPageBase {
  private readonly bool _advanced;
  private readonly string _favKey;
  private readonly List<ParamField> _master = new();
  private readonly DispatcherTimer _searchDebounce;

  [ObservableProperty]
  private string _search = "";

  public ConfigFriendlyParamsViewModel(bool advanced) {
    _advanced = advanced;
    _favKey = advanced ? "fav_params_adv" : "fav_params_std";
    Title = advanced ? "Advanced Params" : "Standard Params";
    Intro = "Human-readable parameters with descriptions. Connect, then Refresh. "
        + "Star a row to pin it to the top; type to filter.";

    _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
    _searchDebounce.Tick += (_, _) => {
      _searchDebounce.Stop();
      ApplyFilter();
    };

    Build();
  }

  protected override void OnRefreshed() => Build();

  partial void OnSearchChanged(string value) {
    // Debounce: restart the timer on each keystroke; filter fires once it settles.
    _searchDebounce.Stop();
    _searchDebounce.Start();
  }

  private void Build() {
    foreach (var f in _master) {
      f.PropertyChanged -= OnFieldChanged;
    }
    _master.Clear();
    Fields.Clear();

    var fw = comPort.MAV.cs.firmware.ToString();
    var favs = Settings.Instance.GetList(_favKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

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

      // Use the bitmask editor when the param exposes bitmask metadata (our view renders it).
      var kind = ParamField.HasBitmask(name, fw) ? "bitmask" : null;
      var field = new ParamField(name, kind) { Fav = favs.Contains(name) };
      field.PropertyChanged += OnFieldChanged;
      _master.Add(field);
    }

    ApplyFilter();
  }

  private void OnFieldChanged(object? sender, PropertyChangedEventArgs e) {
    if (e.PropertyName == nameof(ParamField.Fav)) {
      PersistFavs();
      ApplyFilter();
    }
  }

  private void PersistFavs() {
    var favs = _master.Where(f => f.Fav).Select(f => f.Name).ToList();
    Settings.Instance.SetList(_favKey, favs);
  }

  private void ApplyFilter() {
    var q = Search?.Trim() ?? "";

    IEnumerable<ParamField> view = _master;
    if (q.Length > 0) {
      view = view.Where(f =>
          f.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
          || f.Label.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    // Favourites first, then alphabetical by name.
    var ordered = view
        .OrderByDescending(f => f.Fav)
        .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    Fields.Clear();
    foreach (var f in ordered) {
      Fields.Add(f);
    }
  }
}
