using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlannerAvalonia.Controls;

// Analog circular gauge (face + ticks + needle), mirrors Mission Planner's Gauges tab.
public class Gauge : Control {
  public static readonly StyledProperty<double> ValueProperty =
      AvaloniaProperty.Register<Gauge, double>(nameof(Value));
  public static readonly StyledProperty<double> MinProperty =
      AvaloniaProperty.Register<Gauge, double>(nameof(Min), 0);
  public static readonly StyledProperty<double> MaxProperty =
      AvaloniaProperty.Register<Gauge, double>(nameof(Max), 100);
  public static readonly StyledProperty<string> LabelProperty =
      AvaloniaProperty.Register<Gauge, string>(nameof(Label), "");
  public static readonly StyledProperty<string> UnitsProperty =
      AvaloniaProperty.Register<Gauge, string>(nameof(Units), "");

  static Gauge() {
    AffectsRender<Gauge>(ValueProperty, MinProperty, MaxProperty, LabelProperty, UnitsProperty);
  }

  public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
  public double Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }
  public double Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
  public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
  public string Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }

  private const double StartDeg = 135;
  private const double SweepDeg = 270;

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

    var tickPen = new Pen(Brushes.Gray, 1);
    var majPen = new Pen(Brushes.White, 2);
    for (int i = 0; i <= 10; i++) {
      var a = (StartDeg + SweepDeg * i / 10.0) * Math.PI / 180.0;
      var outer = new Point(c.X + Math.Cos(a) * r, c.Y + Math.Sin(a) * r);
      var inner = new Point(c.X + Math.Cos(a) * (r - (i % 5 == 0 ? 12 : 7)),
                            c.Y + Math.Sin(a) * (r - (i % 5 == 0 ? 12 : 7)));
      ctx.DrawLine(i % 5 == 0 ? majPen : tickPen, inner, outer);
    }

    var range = Max - Min;
    var frac = range <= 0 ? 0 : Math.Clamp((Value - Min) / range, 0, 1);
    var na = (StartDeg + SweepDeg * frac) * Math.PI / 180.0;
    var needle = new Pen(new SolidColorBrush(Color.FromRgb(0x94, 0xC1, 0x1F)), 3);
    ctx.DrawLine(needle, c, new Point(c.X + Math.Cos(na) * (r - 14), c.Y + Math.Sin(na) * (r - 14)));
    ctx.DrawEllipse(Brushes.White, null, c, 3, 3);

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
