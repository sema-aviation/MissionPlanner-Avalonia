using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlannerAvalonia.Controls;

public class HudControl : Control {
  public static readonly StyledProperty<double> RollProperty = AvaloniaProperty.Register<
      HudControl,
      double
  >(nameof(Roll));
  public static readonly StyledProperty<double> PitchProperty = AvaloniaProperty.Register<
      HudControl,
      double
  >(nameof(Pitch));
  public static readonly StyledProperty<double> YawProperty = AvaloniaProperty.Register<HudControl, double>(
      nameof(Yaw)
  );
  public static readonly StyledProperty<double> AltProperty = AvaloniaProperty.Register<HudControl, double>(
      nameof(Alt)
  );
  public static readonly StyledProperty<double> AirSpeedProperty = AvaloniaProperty.Register<
      HudControl,
      double
  >(nameof(AirSpeed));
  public static readonly StyledProperty<double> GroundSpeedProperty = AvaloniaProperty.Register<
      HudControl,
      double
  >(nameof(GroundSpeed));
  public static readonly StyledProperty<double> VerticalSpeedProperty = AvaloniaProperty.Register<
      HudControl,
      double
  >(nameof(VerticalSpeed));
  public static readonly StyledProperty<double> SatCountProperty = AvaloniaProperty.Register<
      HudControl,
      double
  >(nameof(SatCount));
  public static readonly StyledProperty<bool> ArmedProperty = AvaloniaProperty.Register<HudControl, bool>(
      nameof(Armed)
  );
  public static readonly StyledProperty<string> ModeProperty = AvaloniaProperty.Register<
      HudControl,
      string
  >(nameof(Mode), "—");
  public static readonly StyledProperty<double> BatteryVoltageProperty = AvaloniaProperty.Register<
      HudControl,
      double
  >(nameof(BatteryVoltage));
  public static readonly StyledProperty<int> BatteryRemainingProperty = AvaloniaProperty.Register<
      HudControl,
      int
  >(nameof(BatteryRemaining));

  // HUD right-click options (mirror MP contextMenuStripHud)
  public static readonly StyledProperty<bool> ShowIconsProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(ShowIcons), true);
  public static readonly StyledProperty<bool> RussianProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(Russian));
  public static readonly StyledProperty<int> BatteryCellsProperty =
      AvaloniaProperty.Register<HudControl, int>(nameof(BatteryCells));
  public static readonly StyledProperty<string> GroundColorHexProperty =
      AvaloniaProperty.Register<HudControl, string>(nameof(GroundColorHex), "");

  // HUD Items submenu toggles (mirror MP HUD.display* flags)
  public static readonly StyledProperty<bool> DisplayHeadingProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayHeading), true);
  public static readonly StyledProperty<bool> DisplaySpeedProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplaySpeed), true);
  public static readonly StyledProperty<bool> DisplayAltProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayAlt), true);
  public static readonly StyledProperty<bool> DisplayRollPitchProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayRollPitch), true);
  public static readonly StyledProperty<bool> DisplayGpsProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayGps), true);
  public static readonly StyledProperty<bool> DisplayBatteryProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayBattery), true);
  public static readonly StyledProperty<bool> DisplayEkfProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayEkf), true);
  public static readonly StyledProperty<bool> DisplayVibeProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayVibe), true);
  public static readonly StyledProperty<bool> DisplayPrearmProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayPrearm), true);
  public static readonly StyledProperty<double> NavBearingProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(NavBearing));
  public static readonly StyledProperty<string> CustomItemsTextProperty =
      AvaloniaProperty.Register<HudControl, string>(nameof(CustomItemsText), "");

  public double Roll {
    get => GetValue(RollProperty);
    set => SetValue(RollProperty, value);
  }
  public double Pitch {
    get => GetValue(PitchProperty);
    set => SetValue(PitchProperty, value);
  }
  public double Yaw {
    get => GetValue(YawProperty);
    set => SetValue(YawProperty, value);
  }
  public double Alt {
    get => GetValue(AltProperty);
    set => SetValue(AltProperty, value);
  }
  public double AirSpeed {
    get => GetValue(AirSpeedProperty);
    set => SetValue(AirSpeedProperty, value);
  }
  public double GroundSpeed {
    get => GetValue(GroundSpeedProperty);
    set => SetValue(GroundSpeedProperty, value);
  }
  public double VerticalSpeed {
    get => GetValue(VerticalSpeedProperty);
    set => SetValue(VerticalSpeedProperty, value);
  }
  public double SatCount {
    get => GetValue(SatCountProperty);
    set => SetValue(SatCountProperty, value);
  }
  public bool Armed {
    get => GetValue(ArmedProperty);
    set => SetValue(ArmedProperty, value);
  }
  public string Mode {
    get => GetValue(ModeProperty);
    set => SetValue(ModeProperty, value);
  }
  public double BatteryVoltage {
    get => GetValue(BatteryVoltageProperty);
    set => SetValue(BatteryVoltageProperty, value);
  }
  public int BatteryRemaining {
    get => GetValue(BatteryRemainingProperty);
    set => SetValue(BatteryRemainingProperty, value);
  }
  public bool ShowIcons {
    get => GetValue(ShowIconsProperty);
    set => SetValue(ShowIconsProperty, value);
  }
  public bool Russian {
    get => GetValue(RussianProperty);
    set => SetValue(RussianProperty, value);
  }
  public int BatteryCells {
    get => GetValue(BatteryCellsProperty);
    set => SetValue(BatteryCellsProperty, value);
  }
  public string GroundColorHex {
    get => GetValue(GroundColorHexProperty);
    set => SetValue(GroundColorHexProperty, value);
  }
  public bool DisplayHeading {
    get => GetValue(DisplayHeadingProperty);
    set => SetValue(DisplayHeadingProperty, value);
  }
  public bool DisplaySpeed {
    get => GetValue(DisplaySpeedProperty);
    set => SetValue(DisplaySpeedProperty, value);
  }
  public bool DisplayAlt {
    get => GetValue(DisplayAltProperty);
    set => SetValue(DisplayAltProperty, value);
  }
  public bool DisplayRollPitch {
    get => GetValue(DisplayRollPitchProperty);
    set => SetValue(DisplayRollPitchProperty, value);
  }
  public bool DisplayGps {
    get => GetValue(DisplayGpsProperty);
    set => SetValue(DisplayGpsProperty, value);
  }
  public bool DisplayBattery {
    get => GetValue(DisplayBatteryProperty);
    set => SetValue(DisplayBatteryProperty, value);
  }
  public bool DisplayEkf {
    get => GetValue(DisplayEkfProperty);
    set => SetValue(DisplayEkfProperty, value);
  }
  public bool DisplayVibe {
    get => GetValue(DisplayVibeProperty);
    set => SetValue(DisplayVibeProperty, value);
  }
  public bool DisplayPrearm {
    get => GetValue(DisplayPrearmProperty);
    set => SetValue(DisplayPrearmProperty, value);
  }
  public double NavBearing {
    get => GetValue(NavBearingProperty);
    set => SetValue(NavBearingProperty, value);
  }
  public string CustomItemsText {
    get => GetValue(CustomItemsTextProperty);
    set => SetValue(CustomItemsTextProperty, value);
  }

  private static readonly IBrush SkyBrush = new LinearGradientBrush {
    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
    GradientStops =
      {
            new GradientStop(Color.Parse("#3A6FB0"), 0),
            new GradientStop(Color.Parse("#7FB3E0"), 1),
        },
  };
  private static readonly IBrush GroundBrush = new LinearGradientBrush {
    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
    GradientStops =
      {
            new GradientStop(Color.Parse("#9BB824"), 0),
            new GradientStop(Color.Parse("#414F07"), 1),
        },
  };
  private static readonly IBrush Tape = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
  private static readonly IBrush TextBrush = Brushes.White;
  private static readonly Pen WhitePen = new(Brushes.White, 1.5);
  private static readonly Pen ThinPen = new(Brushes.White, 1);
  private static readonly Pen RedPen = new(Brushes.Red, 2.5);

  static HudControl() {
    AffectsRender<HudControl>(
        RollProperty,
        PitchProperty,
        YawProperty,
        AltProperty,
        AirSpeedProperty,
        GroundSpeedProperty,
        VerticalSpeedProperty,
        SatCountProperty,
        ArmedProperty,
        ModeProperty,
        BatteryVoltageProperty,
        BatteryRemainingProperty,
        ShowIconsProperty,
        RussianProperty,
        BatteryCellsProperty,
        GroundColorHexProperty,
        DisplayHeadingProperty,
        DisplaySpeedProperty,
        DisplayAltProperty,
        DisplayRollPitchProperty,
        DisplayGpsProperty,
        DisplayBatteryProperty,
        DisplayEkfProperty,
        DisplayVibeProperty,
        DisplayPrearmProperty,
        NavBearingProperty,
        CustomItemsTextProperty
    );
  }

  private IBrush GroundFill() {
    if (string.IsNullOrEmpty(GroundColorHex)) {
      return GroundBrush;
    }
    try {
      var top = Color.Parse(GroundColorHex);
      var bot = Color.FromArgb(top.A, (byte)(top.R / 2), (byte)(top.G / 2), (byte)(top.B / 2));
      return new LinearGradientBrush {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(top, 0), new GradientStop(bot, 1) },
      };
    } catch {
      return GroundBrush;
    }
  }

  public override void Render(DrawingContext context) {
    var b = Bounds;
    double w = b.Width,
        h = b.Height;
    if (w <= 0 || h <= 0) {
      return;
    }

    context.FillRectangle(Brushes.Black, new Rect(0, 0, w, h));

    double cx = w / 2,
        cy = h / 2 + 14;
    double pxPerDeg = h / 60.0;

    // Russian-style HUD: horizon stays level, aircraft symbol banks instead.
    double horizonRoll = Russian ? 0 : Roll;
    var groundFill = GroundFill();
    using (context.PushClip(new Rect(0, 28, w, h - 28))) {
      var m =
          Matrix.CreateTranslation(-cx, -cy)
          * Matrix.CreateRotation(-horizonRoll * Math.PI / 180.0)
          * Matrix.CreateTranslation(cx, cy + Pitch * pxPerDeg);
      using (context.PushTransform(m)) {
        double big = Math.Max(w, h) * 2;
        double hor = cy;
        context.FillRectangle(SkyBrush, new Rect(cx - big, hor - big, big * 2, big));
        context.FillRectangle(groundFill, new Rect(cx - big, hor, big * 2, big));
        context.DrawLine(WhitePen, new Point(cx - big, hor), new Point(cx + big, hor));

        if (DisplayRollPitch) {
          for (int p = -40; p <= 40; p += 10) {
            if (p == 0) {
              continue;
            }

            double y = hor - p * pxPerDeg;
            double half = 44;
            context.DrawLine(ThinPen, new Point(cx - half, y), new Point(cx + half, y));
            DrawText(context, Math.Abs(p).ToString(), new Point(cx + half + 4, y - 7), 11);
            DrawText(context, Math.Abs(p).ToString(), new Point(cx - half - 22, y - 7), 11);
          }
        }
      }
    }

    if (ShowIcons && DisplayRollPitch) {
      DrawRollArc(context, cx, cy, Math.Min(w, h) * 0.42);
    }

    // center aircraft marker (banks with Roll in Russian mode)
    using (Russian
        ? context.PushTransform(Matrix.CreateRotation(-Roll * Math.PI / 180.0, new Point(cx, cy)))
        : context.PushTransform(Matrix.Identity)) {
      context.DrawLine(RedPen, new Point(cx - 45, cy), new Point(cx - 15, cy));
      context.DrawLine(RedPen, new Point(cx + 15, cy), new Point(cx + 45, cy));
      context.DrawLine(RedPen, new Point(cx - 15, cy), new Point(cx, cy + 12));
      context.DrawLine(RedPen, new Point(cx, cy + 12), new Point(cx + 15, cy));
    }

    if (DisplayHeading) {
      DrawCompassTape(context, w, Yaw, NavBearing);
    }

    if (DisplaySpeed) {
      DrawTape(context, AirSpeed, new Point(4, cy), "AS");
      DrawText(context, $"AS {AirSpeed:0.0}", new Point(6, h - 52), 12);
      DrawText(context, $"GS {GroundSpeed:0.0}", new Point(6, h - 38), 12);
    }
    if (DisplayAlt) {
      DrawTape(context, Alt, new Point(w - 70, cy), "ALT");
    }

    if (DisplayBattery) {
      string batt = BatteryCells > 0
          ? $"Batt {BatteryVoltage:0.00}v {BatteryVoltage / BatteryCells:0.00}v/cell {BatteryRemaining}%"
          : $"Batt {BatteryVoltage:0.00}v {BatteryRemaining}%";
      DrawText(
          context,
          batt,
          new Point(6, h - 22),
          12,
          BatteryRemaining > 0 && BatteryRemaining < 20 ? Brushes.Red : TextBrush
      );
    }

    DrawText(context, $"{VerticalSpeed:+0.0;-0.0;0.0}", new Point(w - 64, h - 38), 12);
    DrawText(context, Mode, new Point(w - 90, h - 22), 13);

    var arm = Armed ? "ARMED" : "DISARMED";
    var armBrush = Armed ? Brushes.LimeGreen : Brushes.Red;
    DrawText(context, arm, new Point(cx - 36, cy + 40), 16, armBrush);
    if (!Armed && DisplayPrearm) {
      DrawText(context, "Not Ready to Arm", new Point(cx - 52, cy + 60), 12, Brushes.Yellow);
    }

    // EKF/Vibe status indicators (MP draws these centered under the attitude)
    if (DisplayEkf) {
      DrawText(context, "EKF", new Point(cx - 30, cy + 78), 11, Brushes.LightGray);
    }
    if (DisplayVibe) {
      DrawText(context, "Vibe", new Point(cx + 10, cy + 78), 11, Brushes.LightGray);
    }

    if (DisplayGps) {
      bool gps = SatCount >= 3;
      DrawText(
          context,
          gps ? $"GPS: 3D ({SatCount:0})" : "GPS: No GPS",
          new Point(cx - 50, h - 18),
          12,
          gps ? Brushes.LimeGreen : Brushes.Red
      );
    }

    if (!string.IsNullOrEmpty(CustomItemsText)) {
      double yy = 30;
      foreach (var line in CustomItemsText.Split('\n')) {
        DrawText(context, line, new Point(w - 150, yy), 11, Brushes.White);
        yy += 14;
      }
    }
  }

  private void DrawCompassTape(DrawingContext ctx, double w, double yaw, double navBearing) {
    double cx = w / 2;
    ctx.FillRectangle(Tape, new Rect(0, 0, w, 24));
    double pxPerDeg = 4;
    for (int d = -60; d <= 60; d += 5) {
      int hdg = ((int)Math.Round(yaw) + d + 360) % 360;
      double x = cx + d * pxPerDeg;
      if (x < 0 || x > w) {
        continue;
      }

      bool major = hdg % 15 == 0;
      ctx.DrawLine(ThinPen, new Point(x, major ? 4 : 10), new Point(x, 16));
      if (major) {
        string lbl = hdg switch {
          0 => "N",
          90 => "E",
          180 => "S",
          270 => "W",
          _ => hdg.ToString(),
        };
        DrawText(ctx, lbl, new Point(x - 7, 2), 10);
      }
    }
    // nav bearing (target heading) marker — distinct color, like MP's map heading vectors
    if (navBearing != 0) {
      double delta = ((navBearing - yaw + 540) % 360) - 180;
      if (Math.Abs(delta) <= 60) {
        double nx = cx + delta * pxPerDeg;
        ctx.DrawLine(new Pen(Brushes.Magenta, 2), new Point(nx, 0), new Point(nx, 22));
      }
    }
    // current heading marker (yellow)
    ctx.DrawLine(new Pen(Brushes.Yellow, 2), new Point(cx, 0), new Point(cx, 22));
  }

  private void DrawRollArc(DrawingContext ctx, double cx, double cy, double r) {
    using (ctx.PushTransform(Matrix.CreateRotation(-Roll * Math.PI / 180.0, new Point(cx, cy)))) {
      foreach (int a in new[] { -45, -30, -20, -10, 0, 10, 20, 30, 45 }) {
        double rad = (a - 90) * Math.PI / 180.0;
        double x1 = cx + r * Math.Cos(rad),
            y1 = cy + r * Math.Sin(rad);
        double len = a == 0 ? 12 : 7;
        double x2 = cx + (r - len) * Math.Cos(rad),
            y2 = cy + (r - len) * Math.Sin(rad);
        ctx.DrawLine(ThinPen, new Point(x1, y1), new Point(x2, y2));
      }
    }
    ctx.DrawLine(new Pen(Brushes.Yellow, 2), new Point(cx, cy - r), new Point(cx - 6, cy - r + 10));
    ctx.DrawLine(new Pen(Brushes.Yellow, 2), new Point(cx, cy - r), new Point(cx + 6, cy - r + 10));
  }

  private void DrawTape(DrawingContext ctx, double value, Point at, string label) {
    var rect = new Rect(at.X, at.Y - 16, 64, 32);
    ctx.FillRectangle(Tape, rect);
    ctx.DrawRectangle(WhitePen, rect);
    DrawText(ctx, value.ToString("0.0", CultureInfo.InvariantCulture), new Point(at.X + 6, at.Y - 8), 13);
    DrawText(ctx, label, new Point(at.X + 6, at.Y - 30), 10);
  }

  private void DrawText(DrawingContext ctx, string text, Point at, double size, IBrush? brush = null) {
    var ft = new FormattedText(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        size,
        brush ?? TextBrush
    );
    ctx.DrawText(ft, at);
  }
}
