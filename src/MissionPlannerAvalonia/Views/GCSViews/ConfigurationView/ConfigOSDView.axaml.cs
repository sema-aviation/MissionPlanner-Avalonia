using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

namespace MissionPlannerAvalonia.Views.GCSViews.ConfigurationView;

public partial class ConfigOSDView : UserControl {
  private readonly Canvas? _canvas;
  private OsdItemVm? _drag;

  public ConfigOSDView() {
    AvaloniaXamlLoader.Load(this);
    _canvas = this.FindControl<Canvas>("OsdCanvas");
    if (_canvas != null) {
      _canvas.PointerPressed += OnPointerPressed;
      _canvas.PointerMoved += OnPointerMoved;
      _canvas.PointerReleased += OnPointerReleased;
    }
  }

  private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {

    if (e.Source is Visual v) {
      var ctrl = v as Control ?? v.FindAncestorOfType<Control>();
      while (ctrl != null && ctrl != _canvas) {
        if (ctrl.DataContext is OsdItemVm item) {
          _drag = item;
          e.Pointer.Capture(_canvas);
          MoveTo(e);
          break;
        }
        ctrl = ctrl.GetVisualParent() as Control;
      }
    }
  }

  private void OnPointerMoved(object? sender, PointerEventArgs e) {
    if (_drag != null) {
      MoveTo(e);
    }
  }

  private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) {
    _drag = null;
    e.Pointer.Capture(null);
  }

  private void MoveTo(PointerEventArgs e) {
    if (_drag == null || _canvas == null || DataContext is not ConfigOSDViewModel vm) {
      return;
    }

    var p = e.GetPosition(_canvas);
    int col = (int)Math.Floor(p.X / vm.CellWidth);
    int row = (int)Math.Floor(p.Y / vm.CellHeight);
    _drag.SetCell(col, row);
  }
}
