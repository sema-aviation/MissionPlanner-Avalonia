using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlannerAvalonia.Controls;

public class Hsi : Control {
  public static readonly StyledProperty<double> HeadingProperty =
      AvaloniaProperty.Register<Hsi, double>(nameof(Heading));
  public static readonly StyledProperty<double> NavHeadingProperty =
      AvaloniaProperty.Register<Hsi, double>(nameof(NavHeading));
  public static readonly StyledProperty<double> WpDistProperty =
      AvaloniaProperty.Register<Hsi, double>(nameof(WpDist));

  static Hsi() {
    AffectsRender<Hsi>(HeadingProperty, NavHeadingProperty, WpDistProperty);
  }

  public double Heading { get => GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
  public double NavHeading {
    get => GetValue(NavHeadingProperty);
    set => SetValue(NavHeadingProperty, value);
  }
  public double WpDist { get => GetValue(WpDistProperty); set => SetValue(WpDistProperty, value); }

  private static readonly Pen _whitePen = new(Brushes.White, 1);

  public override void Render(DrawingContext ctx) {
    double w = Bounds.Width, h = Bounds.Height;
    double size = Math.Min(w, h);
    if (size <= 0) {
      return;
    }
    var c = new Point(w / 2, h / 2);
    double rInside = size / 3.6;
    double rOutside = size / 2.2;
    double font = size / 14;

    ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x10)),
        new Pen(Brushes.DimGray, 2), c, rOutside + font, rOutside + font);

    using (ctx.PushTransform(Matrix.CreateRotation(-Heading * Math.PI / 180.0, c))) {
      for (int a = 0; a < 360; a += 5) {
        double rad = a * Math.PI / 180.0;

        double sin = Math.Sin(rad), cos = -Math.Cos(rad);
        double tickLen = a % 30 == 0 ? 11 : a % 10 == 0 ? 7 : 4;
        var p1 = new Point(c.X + sin * rInside, c.Y + cos * rInside);
        var p2 = new Point(c.X + sin * (rInside + tickLen), c.Y + cos * (rInside + tickLen));
        ctx.DrawLine(_whitePen, p1, p2);
        if (a % 30 == 0) {
          string lbl = a switch {
            0 => "N",
            90 => "E",
            180 => "S",
            270 => "W",
            _ => (a / 10).ToString("0"),
          };
          var tp = new Point(c.X + sin * rOutside, c.Y + cos * rOutside);
          DrawCenter(ctx, lbl, tp, font);
        }
      }
    }

    double s = size / 200.0;
    var or = new Pen(Brushes.DarkOrange, 2);
    ctx.DrawLine(or, new Point(c.X, c.Y + 30 * s), new Point(c.X, c.Y - 10 * s));
    ctx.DrawLine(or, new Point(c.X - 30 * s, c.Y), new Point(c.X + 30 * s, c.Y));
    ctx.DrawLine(or, new Point(c.X - 10 * s, c.Y + 25 * s), new Point(c.X + 10 * s, c.Y + 25 * s));

    ctx.DrawLine(new Pen(Brushes.White, 2),
        new Point(c.X, c.Y - rOutside), new Point(c.X, c.Y - rInside));

    using (ctx.PushTransform(
        Matrix.CreateRotation((NavHeading - Heading) * Math.PI / 180.0, c))) {
      var bug = new StreamGeometry();
      using (var g = bug.Open()) {
        g.BeginFigure(new Point(c.X - 5, c.Y - rOutside), false);
        g.LineTo(new Point(c.X - 5, c.Y - rOutside + 4));
        g.LineTo(new Point(c.X - 3, c.Y - rOutside + 4));
        g.LineTo(new Point(c.X, c.Y - rOutside + 8));
        g.LineTo(new Point(c.X + 3, c.Y - rOutside + 4));
        g.LineTo(new Point(c.X + 5, c.Y - rOutside + 4));
        g.LineTo(new Point(c.X + 5, c.Y - rOutside));
        g.EndFigure(false);
      }
      ctx.DrawGeometry(null, or, bug);
    }

    DrawCenter(ctx, $"{((int)Math.Round(Heading) % 360 + 360) % 360:000}",
        new Point(c.X, c.Y - rInside / 2), font * 1.1, Brushes.White);
    if (WpDist > 0) {
      DrawCenter(ctx, $"{WpDist:0} m", new Point(c.X, c.Y + rInside / 2), font, Brushes.Cyan);
    }
  }

  private static void DrawCenter(DrawingContext ctx, string text, Point center, double size,
      IBrush? brush = null) {
    var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        Typeface.Default, size, brush ?? Brushes.White);
    ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
  }
}
