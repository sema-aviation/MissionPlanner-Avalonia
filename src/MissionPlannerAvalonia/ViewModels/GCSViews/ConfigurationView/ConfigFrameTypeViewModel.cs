using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner;
using MissionPlanner.ArduPilot;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigFrameTypeViewModel : ViewModelBase {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private bool _suppress;

  [ObservableProperty]
  private bool _isPlus;

  [ObservableProperty]
  private bool _isX;

  [ObservableProperty]
  private bool _isV;

  [ObservableProperty]
  private bool _isH;

  [ObservableProperty]
  private bool _isY;

  [ObservableProperty]
  private bool _isVTail;

  [ObservableProperty]
  private bool _isAvailable;

  [ObservableProperty]
  private string _status = "";

  public ConfigFrameTypeViewModel() {
    Activate();
  }

  public void Activate() {
    if (!_comPort.MAV.param.ContainsKey("FRAME")) {
      IsAvailable = false;
      Status = "FRAME parameter not present on this vehicle.";
      return;
    }

    IsAvailable = true;
    try {
      var frame = (Frame)Enum.Parse(typeof(Frame), _comPort.MAV.param["FRAME"].ToString());
      DoChange(frame);
    } catch {
      Status = "Unable to read FRAME value.";
    }
  }

  private void DoChange(Frame frame) {
    _suppress = true;
    IsPlus = frame == Frame.Plus;
    IsX = frame == Frame.X;
    IsV = frame == Frame.V;
    IsH = frame == Frame.H;
    IsY = frame == Frame.Y;
    IsVTail = frame == Frame.VTail;
    _suppress = false;
    SetFrameParam(frame);
  }

  private void SetFrameParam(Frame frame) {
    if (_comPort.BaseStream?.IsOpen != true) {
      Status = "offline";
      return;
    }
    try {
      _comPort.setParam((byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent, "FRAME", (int)frame);
      Status = "FRAME = " + frame;
    } catch {
      Status = "Set FRAME failed.";
    }
  }

  partial void OnIsPlusChanged(bool value) {
    if (value && !_suppress)
      DoChange(Frame.Plus);
  }

  partial void OnIsXChanged(bool value) {
    if (value && !_suppress)
      DoChange(Frame.X);
  }

  partial void OnIsVChanged(bool value) {
    if (value && !_suppress)
      DoChange(Frame.V);
  }

  partial void OnIsHChanged(bool value) {
    if (value && !_suppress)
      DoChange(Frame.H);
  }

  partial void OnIsYChanged(bool value) {
    if (value && !_suppress)
      DoChange(Frame.Y);
  }

  partial void OnIsVTailChanged(bool value) {
    if (value && !_suppress)
      DoChange(Frame.VTail);
  }
}
