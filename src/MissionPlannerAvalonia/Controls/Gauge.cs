using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlannerAvalonia.Controls;

// A coloured range band on the gauge dial (mirrors AGauge m_RangeColor/Start/End).
public class GaugeRange {
  public double Start { get; set; }
  public double End { get; set; }
  public IBrush Color { get; set; } = Brushes.LightGreen;
}

// Analog circular gauge — port of Mission Planner's AGauge: up to 5 needles, coloured
// range arcs, configurable min/max/caps. The original single-needle Value API still works.
public class Gauge : Control {
  public static readonly StyledProperty<double> ValueProperty =
      AvaloniaProperty.Register<Gauge, double>(nameof(Value));
  public static readonly StyledProperty<double> Value2Property =
      AvaloniaProperty.Register<Gauge, double>(nameof(Value2), double.NaN);
  public static readonly StyledProperty<double> Value3Property =
      AvaloniaProperty.Register<Gauge, double>(nameof(Value3), double.NaN);
  public static readonly StyledProperty<double> Value4Property =
      AvaloniaProperty.Register<Gauge, double>(nameof(Value4), double.NaN);
  public static readonly StyledProperty<double> Value5Property =
      AvaloniaProperty.Register<Gauge, double>(nameof(Value5), double.NaN);
  public static readonly StyledProperty<double> MinProperty =
      AvaloniaProperty.Register<Gauge, double>(nameof(Min), 0);
  public static readonly StyledProperty<double> MaxProperty =
      AvaloniaProperty.Register<Gauge, double>(nameof(Max), 100);
  public static readonly StyledProperty<string> LabelProperty =
      AvaloniaProperty.Register<Gauge, string>(nameof(Label), "");
  public static readonly StyledProperty<string> UnitsProperty =
      AvaloniaProperty.Register<Gauge, string>(nameof(Units), "");
  public static readonly StyledProperty<IList<GaugeRange>?> RangesProperty =
      AvaloniaProperty.Register<Gauge, IList<GaugeRange>?>(nameof(Ranges));
  public static readonly StyledProperty<string> CapColorProperty =
      AvaloniaProperty.Register<Gauge, string>(nameof(CapColor), "#94C11F");

  // Needle colours: needle 1 is the upstream green; the rest are distinct so multiple
  // needles read clearly (mirrors AGauge m_NeedleColor per-needle array).
  private static readonly IBrush[] NeedleBrushes = {
    new SolidColorBrush(Color.FromRgb(0x94, 0xC1, 0x1F)),
    Brushes.OrangeRed,
    Brushes.DeepSkyBlue,
    Brushes.Gold,
    Brushes.Violet,
  };

  static Gauge() {
    AffectsRender<Gauge>(ValueProperty, Value2Property, Value3Property, Value4Property,
        Value5Property, MinProperty, MaxProperty, LabelProperty, UnitsProperty, RangesProperty,
        CapColorProperty);
  }

  public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
  public double Value2 { get => GetValue(Value2Property); set => SetValue(Value2Property, value); }
  public double Value3 { get => GetValue(Value3Property); set => SetValue(Value3Property, value); }
  public double Value4 { get => GetValue(Value4Property); set => SetValue(Value4Property, value); }
  public double Value5 { get => GetValue(Value5Property); set => SetValue(Value5Property, value); }
  public double Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }
  public double Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
  public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
  public string Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
  public IList<GaugeRange>? Ranges {
    get => GetValue(RangesProperty);
    set => SetValue(RangesProperty, value);
  }
  public string CapColor { get => GetValue(CapColorProperty); set => SetValue(CapColorProperty, value); }

  private const double StartDeg = 135;
  private const double SweepDeg = 270;

  private double Frac(double value) {
    var range = Max - Min;
    return range <= 0 ? 0 : Math.Clamp((value - Min) / range, 0, 1);
  }

  public override void Render(DrawingContext ctx) {
    var w = Bounds.Width;
    var h = Bounds.Height;
    var size = Math.Min(w, h);
    if (size <= 0) {
      return;
    }
    var c = new Point(w / 2, h / 2);
    var r = size / 2 - 4;

    var face = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x14));
    var rim = new Pen(new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)), 2);
    ctx.DrawEllipse(face, rim, c, r, r);

    // ---- coloured range arcs (bands just inside the rim) ----
    if (Ranges != null) {
      foreach (var band in Ranges) {
        double f0 = Frac(band.Start);
        double f1 = Frac(band.End);
        if (f1 <= f0) {
          continue;
        }
        var arcPen = new Pen(band.Color, r * 0.12);
        double rr = r * 0.86;
        var geo = new StreamGeometry();
        using (var g = geo.Open()) {
          double a0 = (StartDeg + SweepDeg * f0) * Math.PI / 180.0;
          g.BeginFigure(new Point(c.X + Math.Cos(a0) * rr, c.Y + Math.Sin(a0) * rr), false);
          int steps = Math.Max(2, (int)((f1 - f0) * 60));
          for (int i = 1; i <= steps; i++) {
            double f = f0 + (f1 - f0) * i / steps;
            double a = (StartDeg + SweepDeg * f) * Math.PI / 180.0;
            g.LineTo(new Point(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr));
          }
          g.EndFigure(false);
        }
        ctx.DrawGeometry(null, arcPen, geo);
      }
    }

    // ---- tick marks ----
    var tickPen = new Pen(Brushes.Gray, 1);
    var majPen = new Pen(Brushes.White, 2);
    for (int i = 0; i <= 10; i++) {
      var a = (StartDeg + SweepDeg * i / 10.0) * Math.PI / 180.0;
      var outer = new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
      var inner = new Point(c.X + Math.Cos(a) * (r - (i % 5 == 0 ? 12 : 7)),
                            c.Y + Math.Sin(a) * (r - (i % 5 == 0 ? 12 : 7)));
      ctx.DrawLine(i % 5 == 0 ? majPen : tickPen, inner, outer);
    }

    // ---- needles (1..5; NaN = disabled) ----
    double[] vals = { Value, Value2, Value3, Value4, Value5 };
    for (int n = 0; n < vals.Length; n++) {
      if (double.IsNaN(vals[n])) {
        continue;
      }
      var na = (StartDeg + SweepDeg * Frac(vals[n])) * Math.PI / 180.0;
      IBrush nb = n == 0 && !string.IsNullOrEmpty(CapColor)
          ? new SolidColorBrush(Color.Parse(CapColor))
          : NeedleBrushes[n];
      var needle = new Pen(nb, 3);
      ctx.DrawLine(needle, c,
          new Point(c.X + Math.Cos(na) * (r - 14), c.Y + Math.Sin(na) * (r - 14)));
    }
    var capBrush = string.IsNullOrEmpty(CapColor)
        ? Brushes.White : (IBrush)new SolidColorBrush(Color.Parse(CapColor));
    ctx.DrawEllipse(capBrush, null, c, 4, 4);

    Draw(ctx, Value.ToString("0.#"), size * 0.16, Brushes.White,
         new Point(c.X, c.Y + r * 0.42), true);
    Draw(ctx, string.IsNullOrEmpty(Units) ? Label : $"{Label} ({Units})", size * 0.09,
         Brushes.LightGray, new Point(c.X, c.Y + r * 0.66), true);
  }

  private static void Draw(DrawingContext ctx, string text, double size, IBrush brush,
                           Point center, bool centered) {
    var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                               Typeface.Default, size, brush);
    var origin = centered ? new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2) : center;
    ctx.DrawText(ft, origin);
  }
}
