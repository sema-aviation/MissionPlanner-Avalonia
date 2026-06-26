using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace MissionPlannerAvalonia.Controls;

// Live mag-sample cloud visualisation — the Avalonia equivalent of upstream
// MissionPlanner.Controls.Sphere (ExtLibs/Controls/Sphere.cs + ProgressReporterSphere), which during
// onboard compass calibration shows the operator the distribution of magnetometer samples as the
// vehicle is rotated.
//
// NOTE: this is NOT an OpenGL 3D control. Full GL (OpenTK.GLControl) is out of scope on the
// Avalonia/.NET 10 stack used here, so this renders an *orthographic projection* of the incoming 3D
// mag-sample point cloud (a rotating wire-frame with the six +/- axes, exactly like Sphere draws),
// drawn with the 2D DrawingContext. Samples are fed from real MAG_CAL_PROGRESS telemetry
// (direction_x/y/z, the body-frame direction-for-display unit vector the autopilot streams while
// calibrating). Coverage is conveyed by colouring each point from its normalised position, and the
// most recent sample is highlighted — mirroring Sphere's per-point colouring + red "current" dot.
public class MagCalSphere : Control {
  private const int MaxPoints = 6000;
  private const double Deg2Rad = Math.PI / 180.0;

  private readonly List<Point3> _points = new();
  private readonly object _lock = new();
  private readonly DispatcherTimer _spin;

  private double _yaw = 35 * Deg2Rad;
  private double _pitch = 25 * Deg2Rad;
  private float _minx, _maxx, _miny, _maxy, _minz, _maxz;

  private bool _dragging;
  private Point _lastPointer;

  public MagCalSphere() {
    ClipToBounds = true;
    _spin = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
    _spin.Tick += (_, _) => {
      _yaw += 1.5 * Deg2Rad;
      InvalidateVisual();
    };
  }

  // When true the cloud slowly rotates on its own (mirrors Sphere.rotatewithdata).
  public bool AutoRotate {
    get => _spin.IsEnabled;
    set {
      if (value) {
        _spin.Start();
      } else {
        _spin.Stop();
      }
    }
  }

  // Add a mag sample (body-frame direction vector / raw mag). Thread-safe; safe to call from the
  // MAVLink receive path after marshalling to the UI thread.
  public void AddPoint(double x, double y, double z) {
    var p = new Point3((float)x, (float)y, (float)z);
    lock (_lock) {
      _minx = Math.Min(_minx, p.X);
      _maxx = Math.Max(_maxx, p.X);
      _miny = Math.Min(_miny, p.Y);
      _maxy = Math.Max(_maxy, p.Y);
      _minz = Math.Min(_minz, p.Z);
      _maxz = Math.Max(_maxz, p.Z);
      _points.Add(p);
      if (_points.Count > MaxPoints) {
        _points.RemoveAt(0);
      }
    }

    if (Dispatcher.UIThread.CheckAccess()) {
      InvalidateVisual();
    } else {
      Dispatcher.UIThread.Post(InvalidateVisual);
    }
  }

  public void Clear() {
    lock (_lock) {
      _points.Clear();
      _minx = _maxx = _miny = _maxy = _minz = _maxz = 0;
    }

    if (Dispatcher.UIThread.CheckAccess()) {
      InvalidateVisual();
    } else {
      Dispatcher.UIThread.Post(InvalidateVisual);
    }
  }

  protected override void OnPointerPressed(PointerPressedEventArgs e) {
    base.OnPointerPressed(e);
    _dragging = true;
    _lastPointer = e.GetPosition(this);
    e.Pointer.Capture(this);
  }

  protected override void OnPointerMoved(PointerEventArgs e) {
    base.OnPointerMoved(e);
    if (!_dragging) {
      return;
    }

    var pos = e.GetPosition(this);
    _yaw += (pos.X - _lastPointer.X) * 0.01;
    _pitch += (pos.Y - _lastPointer.Y) * 0.01;
    _pitch = Math.Clamp(_pitch, -89 * Deg2Rad, 89 * Deg2Rad);
    _lastPointer = pos;
    InvalidateVisual();
  }

  protected override void OnPointerReleased(PointerReleasedEventArgs e) {
    base.OnPointerReleased(e);
    _dragging = false;
    e.Pointer.Capture(null);
  }

  public override void Render(DrawingContext context) {
    var b = Bounds;
    context.FillRectangle(Brushes.Black, new Rect(0, 0, b.Width, b.Height));

    double cx = b.Width / 2;
    double cy = b.Height / 2;

    // Axis half-length in sample units, matching Sphere's auto-scale heuristic.
    float rangex, rangey, rangez;
    Point3[] snapshot;
    lock (_lock) {
      rangex = _maxx - _minx;
      rangey = _maxy - _miny;
      rangez = _maxz - _minz;
      snapshot = _points.ToArray();
    }

    double max = Math.Max(Math.Max((_maxx - _minx) / 2, (_maxy - _miny) / 2), (_maxz - _minz) / 2);
    if (max < 300) {
      max = 400;
    }

    max *= 1.3;

    double fit = Math.Min(b.Width, b.Height) * 0.42;
    double scale = fit / max;

    // Six signed axes, same colours as Sphere (R=X, G=Y, B=Z and the inverted complements).
    DrawAxis(context, cx, cy, scale, max, 0, 0, Color.FromRgb(255, 60, 60));
    DrawAxis(context, cx, cy, scale, 0, max, 0, Color.FromRgb(60, 255, 60));
    DrawAxis(context, cx, cy, scale, 0, 0, max, Color.FromRgb(80, 120, 255));
    DrawAxis(context, cx, cy, scale, -max, 0, 0, Color.FromRgb(255, 255, 60));
    DrawAxis(context, cx, cy, scale, 0, -max, 0, Color.FromRgb(255, 60, 255));
    DrawAxis(context, cx, cy, scale, 0, 0, -max, Color.FromRgb(60, 255, 255));

    if (snapshot.Length == 0) {
      var txt = new FormattedText(
          "mag-sample cloud (orthographic projection)\nrotate the vehicle to fill it in",
          System.Globalization.CultureInfo.InvariantCulture,
          FlowDirection.LeftToRight,
          Typeface.Default,
          11,
          new SolidColorBrush(Color.FromRgb(140, 140, 140)));
      context.DrawText(txt, new Point(8, b.Height - 34));
      return;
    }

    // Painter's algorithm: project all, sort by depth, draw far→near.
    var projected = new (double Sx, double Sy, double Depth, Color Col)[snapshot.Length];
    for (int i = 0; i < snapshot.Length; i++) {
      var p = snapshot[i];
      Project(p.X, p.Y, p.Z, out double px, out double py, out double depth);
      int vx = rangex > 0 ? (int)Math.Abs(p.X / rangex * 254) & 0xff : 128;
      int vy = rangey > 0 ? (int)Math.Abs(p.Y / rangey * 254) & 0xff : 128;
      int vz = rangez > 0 ? (int)Math.Abs(p.Z / rangez * 254) & 0xff : 128;
      projected[i] = (cx + px * scale, cy - py * scale, depth,
          Color.FromRgb((byte)vx, (byte)vy, (byte)vz));
    }

    Array.Sort(projected, (a, c) => a.Depth.CompareTo(c.Depth));
    foreach (var pr in projected) {
      var brush = new SolidColorBrush(pr.Col);
      context.DrawEllipse(brush, null, new Point(pr.Sx, pr.Sy), 2, 2);
    }

    // Highlight the most recent sample (the "current" dot — Sphere draws it large + red).
    var last = snapshot[^1];
    Project(last.X, last.Y, last.Z, out double lx, out double ly, out _);
    context.DrawEllipse(Brushes.Red, null,
        new Point(cx + lx * scale, cy - ly * scale), 4, 4);

    var count = new FormattedText(
        $"{snapshot.Length} samples  (drag to rotate)",
        System.Globalization.CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        10,
        new SolidColorBrush(Color.FromRgb(160, 160, 160)));
    context.DrawText(count, new Point(6, b.Height - 16));
  }

  private void DrawAxis(DrawingContext context, double cx, double cy, double scale,
      double x, double y, double z, Color col) {
    Project(x, y, z, out double px, out double py, out _);
    var pen = new Pen(new SolidColorBrush(col), 1);
    context.DrawLine(pen, new Point(cx, cy), new Point(cx + px * scale, cy - py * scale));
  }

  // Orthographic projection: yaw about Z, then pitch about X; screen = (x', z''), depth = y''.
  private void Project(double x, double y, double z, out double sx, out double sy, out double depth) {
    double cosY = Math.Cos(_yaw);
    double sinY = Math.Sin(_yaw);
    double x1 = x * cosY - y * sinY;
    double y1 = x * sinY + y * cosY;
    double z1 = z;

    double cosP = Math.Cos(_pitch);
    double sinP = Math.Sin(_pitch);
    double y2 = y1 * cosP - z1 * sinP;
    double z2 = y1 * sinP + z1 * cosP;

    sx = x1;
    sy = z2;
    depth = y2;
  }

  private readonly struct Point3 {
    public Point3(float x, float y, float z) {
      X = x;
      Y = y;
      Z = z;
    }

    public float X { get; }
    public float Y { get; }
    public float Z { get; }
  }
}
