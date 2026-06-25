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
  public static readonly StyledProperty<double> CurrentAmpsProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(CurrentAmps));
  public static readonly StyledProperty<double> TargetAltProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(TargetAlt));
  public static readonly StyledProperty<double> TargetSpeedProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(TargetSpeed));
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
  public double CurrentAmps {
    get => GetValue(CurrentAmpsProperty);
    set => SetValue(CurrentAmpsProperty, value);
  }
  public double TargetAlt {
    get => GetValue(TargetAltProperty);
    set => SetValue(TargetAltProperty, value);
  }
  public double TargetSpeed {
    get => GetValue(TargetSpeedProperty);
    set => SetValue(TargetSpeedProperty, value);
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
        CurrentAmpsProperty,
        TargetAltProperty,
        TargetSpeedProperty,
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

  // Faithful port of MissionPlanner HUD.cs OnPaint (ExtLibs/Controls/HUD.cs).
  public override void Render(DrawingContext context) {
    var b = Bounds;
    double w = b.Width,
        h = b.Height;
    if (w <= 0 || h <= 0) {
      return;
    }

    context.FillRectangle(Brushes.Black, new Rect(0, 0, w, h));

    double fontsize = Math.Max(8, h / 30.0);
    double cx = w / 2,
        cy = h / 2;
    double perDeg = h / 65.0; // MP every5deg (per-degree) = -Height/65
    double headH = h / 14.0;
    var ground = GroundFill();

    // ---- attitude (sky/ground + pitch ladder), rotated by roll about centre ----
    double rollRad = (Russian ? 0 : Roll) * Math.PI / 180.0;
    using (context.PushClip(new Rect(0, headH, w, h - headH)))
    using (context.PushTransform(Matrix.CreateTranslation(cx, cy)))
    using (context.PushTransform(Matrix.CreateRotation(-rollRad))) {
      double big = Math.Max(w, h) * 2;
      double pitchoffset = Pitch * perDeg;
      context.FillRectangle(SkyBrush, new Rect(-big, -big, big * 2, big + pitchoffset));
      context.FillRectangle(ground, new Rect(-big, pitchoffset, big * 2, big * 2 - pitchoffset));
      context.DrawLine(WhitePen, new Point(-big, pitchoffset), new Point(big, pitchoffset));

      if (DisplayRollPitch) {
        for (int a = -90; a <= 90; a += 5) {
          if (a == 0) {
            continue;
          }

          double y = (Pitch - a) * perDeg;
          if (Math.Abs(y) > h * 0.55) {
            continue;
          }

          bool major = a % 10 == 0;
          double len = major ? w / 10.0 : w / 14.0;
          context.DrawLine(ThinPen, new Point(-len / 2, y), new Point(len / 2, y));
          if (major) {
            var br = TextBrush;
            DrawText(context, Math.Abs(a).ToString(), new Point(len / 2 + 3, y - fontsize), fontsize,
                br);
            DrawText(context, Math.Abs(a).ToString(), new Point(-len / 2 - fontsize * 1.6,
                y - fontsize), fontsize, br);
          }
        }
      }
    }

    // ---- roll/bank arc + pointer (ticks rotate with roll, pointer fixed at top) ----
    if (DisplayRollPitch) {
      DrawRollArc(context, cx, cy, Math.Min(w, h) * 0.46);
    }

    // ---- centre aircraft reticle (fixed; banks only in Russian mode) ----
    var reticle = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 0, 0)), 4);
    using (context.PushTransform(Russian
        ? Matrix.CreateRotation(-Roll * Math.PI / 180.0, new Point(cx, cy))
        : Matrix.Identity)) {
      double wing = w / 10.0;
      context.DrawLine(reticle, new Point(cx - wing - wing, cy), new Point(cx - wing, cy));
      context.DrawLine(reticle, new Point(cx + wing, cy), new Point(cx + wing + wing, cy));
      context.DrawLine(reticle, new Point(cx - wing, cy), new Point(cx, cy + h / 18.0));
      context.DrawLine(reticle, new Point(cx, cy + h / 18.0), new Point(cx + wing, cy));
    }

    // ---- heading ribbon (top) ----
    if (DisplayHeading) {
      DrawHeadingTape(context, w, headH, fontsize, Yaw, NavBearing);
    }

    // ---- speed tape (left) + altitude tape (right) ----
    double tapeW = Math.Max(34, w / 10.0);
    var speedRect = new Rect(0, cy - h / 4.0, tapeW, h / 2.0);
    var altRect = new Rect(w - tapeW, cy - h / 4.0, tapeW, h / 2.0);
    double spd = AirSpeed > 0 ? AirSpeed : GroundSpeed;
    if (DisplaySpeed) {
      DrawScrollTape(context, speedRect, spd, TargetSpeed, false, fontsize, "");
      DrawText(context, $"AS {AirSpeed:0.0}", new Point(2, speedRect.Bottom + 4), fontsize);
      DrawText(context, $"GS {GroundSpeed:0.0}", new Point(2, speedRect.Bottom + fontsize + 8),
          fontsize);
    }
    if (DisplayAlt) {
      DrawScrollTape(context, altRect, Alt, TargetAlt, true, fontsize, "");
      DrawVsi(context, altRect, VerticalSpeed);
      var modeBrush = TextBrush;
      DrawText(context, Mode, new Point(altRect.Left - 4, altRect.Bottom + 4), fontsize, modeBrush);
    }

    // ---- bottom-left battery ----
    if (DisplayBattery) {
      string cell = (DisplayBattery && BatteryCells > 0)
          ? $"Cell {BatteryVoltage / BatteryCells:0.00}v  "
          : "";
      string batt = $"{cell}Bat {BatteryVoltage:0.00}v {CurrentAmps:0.0} A {BatteryRemaining}%";
      var bb = BatteryRemaining > 0 && BatteryRemaining < 20 ? Brushes.Red
          : BatteryRemaining > 0 && BatteryRemaining < 30 ? Brushes.Orange
          : TextBrush;
      DrawText(context, batt, new Point(2, h - fontsize - 4), fontsize, bb);
    }

    // ---- bottom-right cluster: GPS (bottom), EKF/Vibe (mid), Prearm (top) ----
    double ry0 = h - fontsize - 4;
    double ry1 = h - 2 * fontsize - 8;
    double ry2 = h - 3 * fontsize - 12;
    if (DisplayGps) {
      bool gps = SatCount >= 3;
      string g = gps ? $"GPS: 3D Fix ({SatCount:0})" : "GPS: No Fix";
      DrawTextRight(context, g, w - 6, ry0, fontsize, gps ? Brushes.LimeGreen : Brushes.Red);
    }
    double rightX = w - 6;
    if (DisplayVibe) {
      rightX -= DrawTextRight(context, "Vibe", rightX, ry1, fontsize, Brushes.White) + 12;
    }
    if (DisplayEkf) {
      DrawTextRight(context, "EKF", rightX, ry1, fontsize, Brushes.White);
    }
    if (DisplayPrearm && !Armed) {
      DrawTextRight(context, "Not Ready to Arm", w - 6, ry2, fontsize, Brushes.Red);
    }

    // ---- custom user items (left) ----
    if (!string.IsNullOrEmpty(CustomItemsText)) {
      double yy = headH + 4;
      foreach (var line in CustomItemsText.Split('\n')) {
        DrawText(context, line, new Point(4, yy), fontsize, Brushes.White);
        yy += fontsize + 3;
      }
    }

    // ---- centre armed / disarmed ----
    var arm = Armed ? "ARMED" : "DISARMED";
    DrawText(context, arm, new Point(cx - fontsize * 2.5, h / 3.0), fontsize + 10, Brushes.Red);
  }

  private void DrawHeadingTape(DrawingContext ctx, double w, double headH, double fontsize,
      double yaw, double navBearing) {
    var bg = new Rect(0, 0, w, headH);
    ctx.FillRectangle(Tape, bg);
    ctx.DrawRectangle(null, new Pen(Brushes.Black, 2), bg);
    double space = (w - 10) / 120.0;
    int yawi = (int)Math.Round(yaw);
    for (int d = -60; d <= 60; d++) {
      int hdg = ((yawi + d) % 360 + 360) % 360;
      double x = w / 2 + d * space;
      bool major = hdg % 15 == 0;
      if (hdg % 5 == 0) {
        ctx.DrawLine(WhitePen, new Point(x, headH - 5), new Point(x, headH - 10));
      }
      if (major) {
        string lbl = hdg switch {
          0 => "N",
          45 => "NE",
          90 => "E",
          135 => "SE",
          180 => "S",
          225 => "SW",
          270 => "W",
          315 => "NW",
          _ => hdg.ToString("000"),
        };
        DrawText(ctx, lbl, new Point(x - fontsize, 1), fontsize, Brushes.White);
      }
    }
    // target heading (nav bearing) — green marker, like MP
    if (navBearing != 0) {
      double delta = ((navBearing - yaw + 540) % 360) - 180;
      if (Math.Abs(delta) >= 4 && Math.Abs(delta) <= 60) {
        double nx = w / 2 + delta * space;
        ctx.DrawLine(new Pen(Brushes.Green, 6), new Point(nx, 0), new Point(nx, headH));
      }
    }
    // current heading box + value (yellow centre line)
    double bw = fontsize * 2.6;
    var box = new Rect(w / 2 - bw / 2, 0, bw, headH);
    ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), box);
    DrawText(ctx, yawi.ToString("000"), new Point(w / 2 - fontsize, headH / 2 - fontsize / 2),
        fontsize, Brushes.Black);
  }

  private void DrawRollArc(DrawingContext ctx, double cx, double cy, double r) {
    var c = new Point(cx, cy);
    using (ctx.PushTransform(Matrix.CreateRotation(-Roll * Math.PI / 180.0, c))) {
      foreach (int a in new[] { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 }) {
        double rad = (a - 90) * Math.PI / 180.0;
        double len = a == 0 ? 12 : 7;
        ctx.DrawLine(WhitePen,
            new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad)),
            new Point(cx + (r - len) * Math.Cos(rad), cy + (r - len) * Math.Sin(rad)));
      }
      // arc curve
      var geo = new StreamGeometry();
      using (var g = geo.Open()) {
        double a0 = -150 * Math.PI / 180.0;
        g.BeginFigure(new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0)), false);
        for (int a = -60; a <= 60; a += 4) {
          double rad = (a - 90) * Math.PI / 180.0;
          g.LineTo(new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad)));
        }
        g.EndFigure(false);
      }
      ctx.DrawGeometry(null, WhitePen, geo);
    }
    // fixed bank pointer (red triangle) at top
    var pen = new Pen(Brushes.Red, Math.Abs(Roll) > 45 ? 4 : 2);
    ctx.DrawLine(pen, new Point(cx, cy - r), new Point(cx - 8, cy - r + 12));
    ctx.DrawLine(pen, new Point(cx, cy - r), new Point(cx + 8, cy - r + 12));
  }

  private void DrawScrollTape(DrawingContext ctx, Rect rect, double value, double target,
      bool leftSide, double fontsize, string suffix) {
    ctx.FillRectangle(Tape, rect);
    ctx.DrawRectangle(null, WhitePen, rect);
    const double viewrange = 26;
    double space = rect.Height / viewrange;
    double midY = rect.Center.Y;
    using (ctx.PushClip(rect)) {
      int start = (int)Math.Floor(value - viewrange / 2);
      int end = (int)Math.Ceiling(value + viewrange / 2);
      for (int iv = start; iv <= end; iv++) {
        double y = midY - (iv - value) * space;
        if (iv % 5 == 0) {
          if (leftSide) {
            ctx.DrawLine(WhitePen, new Point(rect.Left, y), new Point(rect.Left + 8, y));
            DrawText(ctx, iv.ToString(), new Point(rect.Left + 10, y - fontsize / 2), fontsize - 1,
                Brushes.White);
          } else {
            ctx.DrawLine(WhitePen, new Point(rect.Right - 8, y), new Point(rect.Right, y));
            DrawText(ctx, iv.ToString(), new Point(rect.Left + 2, y - fontsize / 2), fontsize - 1,
                Brushes.White);
          }
        }
      }
      // target marker (green)
      if (target != 0 && Math.Abs(target - value) < viewrange / 2) {
        double ty = midY - (target - value) * space;
        ctx.DrawLine(new Pen(Brushes.Green, 4), new Point(rect.Left, ty), new Point(rect.Right, ty));
      }
    }
    // centre value box
    double bh = fontsize + 6;
    var box = new Rect(rect.Left, midY - bh / 2, rect.Width, bh);
    ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), box);
    ctx.DrawRectangle(null, WhitePen, box);
    DrawText(ctx, ((int)Math.Round(value)).ToString(), new Point(rect.Left + 3, midY - fontsize / 2),
        fontsize, Brushes.AliceBlue);
  }

  private void DrawVsi(DrawingContext ctx, Rect altRect, double vspeed) {
    double vw = altRect.Width / 4.0;
    double left = altRect.Left - vw;
    var box = new StreamGeometry();
    using (var g = box.Open()) {
      g.BeginFigure(new Point(altRect.Left, altRect.Top), false);
      g.LineTo(new Point(left, altRect.Top + vw));
      g.LineTo(new Point(left, altRect.Bottom - vw));
      g.LineTo(new Point(altRect.Left, altRect.Bottom));
      g.EndFigure(false);
    }
    ctx.DrawGeometry(null, WhitePen, box);
    double mid = altRect.Center.Y;
    double scaled = Math.Clamp(vspeed / 12.0, -1, 1) * (altRect.Height / 2 - 4);
    var tri = new StreamGeometry();
    using (var g = tri.Open()) {
      g.BeginFigure(new Point(altRect.Left, mid), true);
      g.LineTo(new Point(left + 2, mid - scaled));
      g.LineTo(new Point(altRect.Left, mid - scaled));
      g.EndFigure(true);
    }
    ctx.DrawGeometry(Brushes.Blue, null, tri);
  }

  private void DrawText(DrawingContext ctx, string text, Point at, double size, IBrush? brush = null) {
    ctx.DrawText(MakeText(text, size, brush), at);
  }

  // Right-aligns text so rightX is the right edge; returns the text width.
  private double DrawTextRight(DrawingContext ctx, string text, double rightX, double y, double size,
      IBrush? brush = null) {
    var ft = MakeText(text, size, brush);
    ctx.DrawText(ft, new Point(rightX - ft.Width, y));
    return ft.Width;
  }

  private static FormattedText MakeText(string text, double size, IBrush? brush) {
    return new FormattedText(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        size,
        brush ?? TextBrush
    );
  }
}
