using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

// Mirrors upstream GCSViews/ConfigurationView/ConfigFrameClassType.cs: FRAME_CLASS /
// FRAME_TYPE selection where the available types are filtered per class via
// ArduPilot.Common.ValidList, and a diagram image is shown for the chosen type.
// Combos are used here instead of upstream's radio-button + PictureBox grid.
public partial class ConfigFrameClassTypeViewModel : ParamPageBase {
  private bool _suppress;

  public ObservableCollection<ParamOption> ClassOptions { get; } = new();
  public ObservableCollection<ParamOption> TypeOptions { get; } = new();

  [ObservableProperty]
  private ParamOption? _selectedClass;

  [ObservableProperty]
  private ParamOption? _selectedType;

  [ObservableProperty]
  private bool _typeEnabled = true;

  [ObservableProperty]
  private bool _isAvailable;

  // ponytail: upstream shows per-type frame diagrams (Resources.frames_plus / frames_x /
  // new_3DR_04 / frames_h / y6b ...). Those bitmaps are not yet imported into the Avalonia
  // asset pipeline, so FrameImage stays null and the View shows a textual placeholder.
  // Once the art is bundled under Assets/, set this to the matching avares:// path in DoType.
  [ObservableProperty]
  private string? _frameImage;

  [ObservableProperty]
  private string _frameImageCaption = "";

  [ObservableProperty]
  private string _status = "";

  public ConfigFrameClassTypeViewModel() {
    Title = "Frame Class / Type";
    Activate();
  }

  protected override void OnRefreshed() {
    Activate();
  }

  public void Activate() {
    ClassOptions.Clear();
    if (!comPort.MAV.param.ContainsKey("FRAME_CLASS") ||
        !comPort.MAV.param.ContainsKey("FRAME_TYPE")) {
      IsAvailable = false;
      Status = "FRAME_CLASS / FRAME_TYPE not present on this vehicle.";
      return;
    }

    IsAvailable = true;

    // distinct classes present in the valid list, friendly labelled
    foreach (var cls in Common.ValidList.Select(a => a.Item1).Distinct()) {
      ClassOptions.Add(new ParamOption((int)cls, Friendly(cls.ToString())));
    }

    try {
      var curClass = (motor_frame_class)(int)Math.Round(comPort.MAV.param["FRAME_CLASS"].Value);
      var curType = (motor_frame_type)(int)Math.Round(comPort.MAV.param["FRAME_TYPE"].Value);

      _suppress = true;
      SelectedClass = ClassOptions.FirstOrDefault(o => o.Value == (int)curClass);
      RebuildTypes(curClass, curType);
      _suppress = false;
    } catch {
      Status = "Unable to read existing FRAME_CLASS / FRAME_TYPE.";
    }
  }

  partial void OnSelectedClassChanged(ParamOption? value) {
    if (_suppress || value == null) {
      return;
    }

    var cls = (motor_frame_class)value.Value;
    RebuildTypes(cls, (motor_frame_type)(SelectedType?.Value ?? 0));
    WriteParam("FRAME_CLASS", value.Value);
  }

  partial void OnSelectedTypeChanged(ParamOption? value) {
    if (_suppress || value == null) {
      return;
    }

    WriteParam("FRAME_TYPE", value.Value);
    UpdateImage((motor_frame_type)value.Value);
  }

  private void RebuildTypes(motor_frame_class cls, motor_frame_type preferred) {
    var validTypes = Common.ValidList
        .Where(a => a.Item1 == cls && a.Item2.HasValue)
        .Select(a => a.Item2!.Value)
        .Distinct()
        .ToList();

    bool wasSuppressed = _suppress;
    _suppress = true;

    TypeOptions.Clear();
    foreach (var t in validTypes) {
      TypeOptions.Add(new ParamOption((int)t, Friendly(t.ToString())));
    }

    TypeEnabled = TypeOptions.Count > 0;

    SelectedType = TypeOptions.FirstOrDefault(o => o.Value == (int)preferred)
        ?? TypeOptions.FirstOrDefault();

    _suppress = wasSuppressed;

    if (SelectedType != null) {
      UpdateImage((motor_frame_type)SelectedType.Value);
    } else {
      FrameImage = null;
      FrameImageCaption = "(no sub-types for this class)";
    }
  }

  private void UpdateImage(motor_frame_type type) {
    // ponytail: art assets not imported yet — only set the caption.
    FrameImage = null;
    FrameImageCaption = Friendly(type.ToString()) + " (diagram pending asset import)";
  }

  private void WriteParam(string name, int value) {
    if (comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }
    try {
      comPort.setParam((byte)comPort.sysidcurrent, (byte)comPort.compidcurrent, name, value);
      Status = name + " = " + value;
    } catch {
      Status = "Set " + name + " failed.";
    }
  }

  private static string Friendly(string enumName) {
    return enumName
        .Replace("MOTOR_FRAME_TYPE_", "")
        .Replace("MOTOR_FRAME_", "");
  }
}
