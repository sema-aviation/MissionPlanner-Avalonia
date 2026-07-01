using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public static class Tuning {
  public static string Resolve(params string[] names) {
    foreach (var n in names) {
      if (AppState.comPort.MAV.param.ContainsKey(n)) {
        return n;
      }
    }
    return names.Length > 0 ? names[0] : "";
  }

  internal static async Task<bool> ConfirmAsync(string message) {
    var top = (Avalonia.Application.Current?.ApplicationLifetime
               as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
    if (top == null) {
      return true;
    }

    var yes = new Button { Content = "Yes", MinWidth = 64 };
    var no = new Button { Content = "No", MinWidth = 64 };
    var dlg = new Window {
      Title = "Large Value",
      Width = 400,
      Height = 150,
      WindowStartupLocation = WindowStartupLocation.CenterOwner,
      Content = new StackPanel {
        Margin = new Avalonia.Thickness(12),
        Spacing = 12,
        Children = {
          new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
          new StackPanel {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { yes, no },
          },
        },
      },
    };
    yes.Click += (_, _) => dlg.Close(true);
    no.Click += (_, _) => dlg.Close(false);
    return await dlg.ShowDialog<bool>(top);
  }
}

public partial class TuningRow : ObservableObject {
  public ParamField Field { get; }
  public string Label { get; }

  public string Name => Field.Name;
  public bool IsCombo => Field.IsCombo;
  public bool IsBool => Field.IsBool;
  public bool IsNumeric => Field.IsNumeric;
  public double Min => Field.Min;
  public double Max => Field.Max;
  public double Increment => Field.Increment;
  public string Units => Field.Units;
  public string Description => Field.Description;
  public ObservableCollection<ParamOption> Options => Field.Options;
  public bool Exists => Field.Exists;

  [ObservableProperty]
  private double _value;

  [ObservableProperty]
  private bool _checked;

  [ObservableProperty]
  private ParamOption? _selectedOption;

  [ObservableProperty]
  private string _status = "";

  public bool Dirty { get; private set; }

  public TuningRow(string label, ParamField field) {
    Label = label;
    Field = field;
    Reload();
  }

  public void Reload() {
    Field.Reload();
    Value = Field.Value;
    OnPropertyChanged(nameof(Value));
    Checked = Field.Value != 0;
    OnPropertyChanged(nameof(Checked));
    SelectedOption = Field.Options.FirstOrDefault(o => o.Value == (int)Math.Round(Field.Value));
    Status = Exists ? "" : "n/a";
    Dirty = false;
  }

  public double Target =>
      IsCombo ? SelectedOption?.Value ?? Value
      : IsBool ? Checked ? 1 : 0
      : Value;

  public void MarkClean() => Dirty = false;

  partial void OnValueChanged(double value) {
    if (Exists) {
      Dirty = true;
    }
  }

  partial void OnCheckedChanged(bool value) {
    if (Exists) {
      Dirty = true;
    }
  }

  partial void OnSelectedOptionChanged(ParamOption? value) {
    if (Exists && value != null) {
      Dirty = true;
    }
  }
}

public class TuningGroup {
  public string Title { get; }
  public bool IsVisible { get; set; } = true;
  public ObservableCollection<TuningRow> Rows { get; } = new();

  public TuningGroup(string title) {
    Title = title;
  }

  public TuningGroup Num(string label, params string[] names) {
    Rows.Add(new TuningRow(label, new ParamField(Tuning.Resolve(names), "num")));
    return this;
  }

  public TuningGroup Combo(string label, params string[] names) {
    Rows.Add(new TuningRow(label, new ParamField(Tuning.Resolve(names), "combo")));
    return this;
  }
}

public abstract partial class TuningPageBase : ParamPageBase {
  public ObservableCollection<TuningGroup> Groups { get; } = new();

  protected abstract void Build();

  protected void Rebuild() {
    Groups.Clear();
    Build();
  }

  private IEnumerable<TuningRow> AllRows() => Groups.SelectMany(g => g.Rows);

  [RelayCommand]
  [Obsolete]
  private async Task Write() {
    foreach (var row in AllRows().ToList()) {
      if (!row.Dirty || !row.Exists) {
        continue;
      }

      var target = row.Target;
      var current = comPort.MAV.param[row.Name].Value;
      if (target > current * 2.0) {
        if (!await Tuning.ConfirmAsync(
                row.Name + " has more than doubled the last input. Are you sure?")) {
          row.Reload();
          continue;
        }
      }

      if (IsConnected) {
        var name = row.Name;
        bool ok = await Task.Run(() => comPort.setParam(name, target, true));
        row.Status = ok ? "✓" : "write failed";
        if (ok) {
          row.MarkClean();
        }
      } else {
        comPort.MAV.param[row.Name].Value = target;
        row.Status = "offline";
        row.MarkClean();
      }
    }
  }

  [RelayCommand]
  private async Task RefreshParams() {
    if (IsConnected) {
      await Task.Run(() => comPort.getParamList());
    }

    Rebuild();
  }

  [RelayCommand]
  private void RefreshScreen() {
    foreach (var row in AllRows()) {
      row.Reload();
    }
  }
}

public class ConfigArduplaneViewModel : TuningPageBase {
  public ConfigArduplaneViewModel() {
    Title = "ArduPlane Pids";
    Intro = "Plane basic tuning. Fields show n/a if not present on this firmware.";
    Rebuild();
  }

  protected override void Build() {
    Groups.Add(new TuningGroup("Throttle 0-100%")
        .Num("SlewRate", "THR_SLEWRATE")
        .Num("Max", "THR_MAX")
        .Num("Min", "THR_MIN")
        .Num("Cruise", "TRIM_THROTTLE"));

    Groups.Add(new TuningGroup("Airspeed m/s")
        .Num("Ratio", "ARSPD_RATIO")
        .Num("Max", "AIRSPEED_MAX", "ARSPD_FBW_MAX")
        .Num("Min", "AIRSPEED_MIN", "ARSPD_FBW_MIN")
        .Num("Cruise", "AIRSPEED_CRUISE", "TRIM_ARSPD_CM"));

    Groups.Add(new TuningGroup("Navigation Angles")
        .Num("Pitch Min", "PTCH_LIM_MIN_DEG", "LIM_PITCH_MIN")
        .Num("Pitch Max", "PTCH_LIM_MAX_DEG", "LIM_PITCH_MAX")
        .Num("Bank Max", "ROLL_LIMIT_DEG", "LIM_ROLL_CD"));

    Groups.Add(new TuningGroup("Other Mix's")
        .Num("P to T", "KFF_THR2PTCH", "KFF_PTCH2THR")
        .Num("Rudder Mix", "KFF_RDDRMIX"));

    Groups.Add(new TuningGroup("Energy/Alt Pid")
        .Num("P", "ENRGY2THR_P")
        .Num("I", "ENRGY2THR_I")
        .Num("D", "ENRGY2THR_D")
        .Num("INT_MAX", "ENRGY2THR_IMAX"));

    Groups.Add(new TuningGroup("Nav Pitch Alt Pid")
        .Num("P", "ALT2PTCH_P")
        .Num("I", "ALT2PTCH_I")
        .Num("D", "ALT2PTCH_D")
        .Num("INT_MAX", "ALT2PTCH_IMAX"));

    Groups.Add(new TuningGroup("Nav Pitch AS Pid")
        .Num("P", "ARSP2PTCH_P")
        .Num("I", "ARSP2PTCH_I")
        .Num("D", "ARSP2PTCH_D")
        .Num("INT_MAX", "ARSP2PTCH_IMAX"));

    Groups.Add(new TuningGroup("Servo Yaw")
        .Num("Yaw 2 roll", "YAW2SRV_RLL")
        .Num("Integral", "YAW2SRV_INT")
        .Num("Dampening", "YAW2SRV_DAMP")
        .Num("Intergrator Max", "YAW2SRV_IMAX"));

    Groups.Add(new TuningGroup("Servo Pitch Pid")
        .Num("P", "PTCH2SRV_P", "PTCH_RATE_P")
        .Num("I", "PTCH2SRV_I", "PTCH_RATE_I")
        .Num("D", "PTCH2SRV_D", "PTCH_RATE_D")
        .Num("INT_MAX", "PTCH2SRV_IMAX", "PTCH_RATE_IMAX"));

    Groups.Add(new TuningGroup("Servo Roll Pid")
        .Num("P", "RLL2SRV_P", "RLL_RATE_P")
        .Num("I", "RLL2SRV_I", "RLL_RATE_I")
        .Num("D", "RLL2SRV_D", "RLL_RATE_D")
        .Num("INT_MAX", "RLL2SRV_IMAX", "RLL_RATE_IMAX"));

    Groups.Add(new TuningGroup("L1 Control - Turn Control")
        .Num("Period", "NAVL1_PERIOD")
        .Num("Damping", "NAVL1_DAMPING"));

    Groups.Add(new TuningGroup("TECS")
        .Num("Climb Max (m/s)", "TECS_CLMB_MAX")
        .Num("Sink Min (m/s)", "TECS_SINK_MIN")
        .Num("Pitch Dampening", "TECS_PTCH_DAMP")
        .Num("Time Const", "TECS_TIME_CONST")
        .Num("Sink Max (m/s)", "TECS_SINK_MAX"));
  }
}
