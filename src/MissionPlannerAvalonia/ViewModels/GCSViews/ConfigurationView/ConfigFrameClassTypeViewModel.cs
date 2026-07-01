using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

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

  [ObservableProperty]
  private Bitmap? _frameImage;

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
      FrameImage = LoadClassImage(cls);
      FrameImageCaption = FrameImage == null ? "(no sub-types for this class)" : "";
    }
  }

  private void UpdateImage(motor_frame_type type) {
    var bmp = LoadTypeImage(type);
    if (bmp == null && SelectedClass != null) {
      bmp = LoadClassImage((motor_frame_class)SelectedClass.Value);
    }
    FrameImage = bmp;
    FrameImageCaption = bmp == null ? Friendly(type.ToString()) : "";
  }

  private static Bitmap? LoadTypeImage(motor_frame_type type) => type switch {
    motor_frame_type.MOTOR_FRAME_TYPE_PLUS => Load("type_plus"),
    motor_frame_type.MOTOR_FRAME_TYPE_X => Load("type_x"),
    motor_frame_type.MOTOR_FRAME_TYPE_V => Load("type_v"),
    motor_frame_type.MOTOR_FRAME_TYPE_H => Load("type_h"),
    motor_frame_type.MOTOR_FRAME_TYPE_Y6B => Load("type_y6b"),
    _ => null,
  };

  private static Bitmap? LoadClassImage(motor_frame_class cls) => cls switch {
    motor_frame_class.MOTOR_FRAME_QUAD => Load("class_quad"),
    motor_frame_class.MOTOR_FRAME_HEXA => Load("class_hexa"),
    motor_frame_class.MOTOR_FRAME_OCTA => Load("class_octa"),
    motor_frame_class.MOTOR_FRAME_OCTAQUAD => Load("class_octaquad"),
    motor_frame_class.MOTOR_FRAME_Y6 => Load("class_y6"),
    motor_frame_class.MOTOR_FRAME_HELI => Load("class_heli"),
    motor_frame_class.MOTOR_FRAME_TRI => Load("class_tri"),
    _ => null,
  };

  private static readonly Dictionary<string, Bitmap?> _imageCache = new();

  private static Bitmap? Load(string name) {
    if (_imageCache.TryGetValue(name, out var cached)) {
      return cached;
    }
    Bitmap? bmp = null;
    try {
      var uri = new Uri($"avares://MissionPlannerAvalonia/Assets/Frames/{name}.png");
      using var stream = AssetLoader.Open(uri);
      bmp = new Bitmap(stream);
    } catch {
      bmp = null;
    }
    _imageCache[name] = bmp;
    return bmp;
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
