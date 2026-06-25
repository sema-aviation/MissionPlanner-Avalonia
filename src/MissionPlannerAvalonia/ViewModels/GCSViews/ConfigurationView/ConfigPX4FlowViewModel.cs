using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner;
using MissionPlanner.Utilities;
using AvPixelFormat = Avalonia.Platform.PixelFormat;
using SdBitmap = System.Drawing.Bitmap;
using SdPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace MissionPlannerAvalonia.ViewModels.GCSViews.ConfigurationView;

public partial class ConfigPX4FlowViewModel : ViewModelBase, IDisposable {
  private readonly MAVLinkInterface _comPort = AppState.comPort;
  private OpticalFlow _flow;
  private bool _focusmode;

  [ObservableProperty]
  private WriteableBitmap _image;

  [ObservableProperty]
  private string _status = "Waiting for PX4Flow image stream…";

  [ObservableProperty]
  private string _focusLabel = "Focus";

  public bool IsConnected => _comPort.BaseStream?.IsOpen == true || _comPort.logreadmode;

  public ConfigPX4FlowViewModel() {
    if (!IsConnected) {
      Status = "Not connected — connect to a PX4Flow sensor to view its image.";
      return;
    }

    _flow = new OpticalFlow(_comPort, (byte)_comPort.sysidcurrent, (byte)_comPort.compidcurrent);
    _flow.newImage += OnNewImage;
  }

  private void OnNewImage(object sender, OpticalFlow.ImageEventHandle e) {
    var wb = Convert(e.Image);
    if (wb == null) {
      return;
    }

    Dispatcher.UIThread.Post(() => {
      Image = wb;
      Status = $"Receiving image ({e.Image.Width}x{e.Image.Height}).";
    });
  }

  private static WriteableBitmap Convert(Image source) {
    if (source is not SdBitmap bmp) {
      return null;
    }

    int w = bmp.Width, h = bmp.Height;
    if (w <= 0 || h <= 0) {
      return null;
    }

    using var rgb = new SdBitmap(w, h, SdPixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(rgb)) {
      g.DrawImage(bmp, 0, 0, w, h);
    }

    var data = rgb.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly,
      SdPixelFormat.Format32bppArgb);
    byte[] managed = new byte[data.Stride * h];
    try {
      Marshal.Copy(data.Scan0, managed, 0, managed.Length);
    } finally {
      rgb.UnlockBits(data);
    }

    var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
      AvPixelFormat.Bgra8888, AlphaFormat.Premul);
    using var fb = wb.Lock();
    int copyBytes = Math.Min(data.Stride, fb.RowBytes);
    for (int y = 0; y < h; y++) {
      Marshal.Copy(managed, y * data.Stride, fb.Address + y * fb.RowBytes, copyBytes);
    }

    return wb;
  }

  [RelayCommand]
  private void ToggleFocus() {
    if (_flow == null) {
      return;
    }

    _focusmode = !_focusmode;
    _flow.CalibrationMode(_focusmode);
    FocusLabel = _focusmode ? "Video" : "Focus";
  }

  public void Dispose() {
    if (_flow != null) {
      _flow.newImage -= OnNewImage;
      _flow.CalibrationMode(false);
      _flow.Close();
      _flow = null;
    }
  }
}
