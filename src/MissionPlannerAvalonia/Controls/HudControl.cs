using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MissionPlannerAvalonia.Controls;

public class HudControl : Control {
  // Raised with "ekf" / "vibe" / "prearm" when the matching HUD indicator is clicked,
  // so the host can open the EKF/Vibration/Prearm status windows (mirrors MP hud1_*click).
  public event Action<string>? IndicatorClicked;
  private Rect _ekfRect, _vibeRect, _prearmRect;
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

  // ---- new upstream HUD.cs draw items ----
  public static readonly StyledProperty<double> WindDirProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(WindDir));
  public static readonly StyledProperty<double> WindVelProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(WindVel));
  public static readonly StyledProperty<double> AoaProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(Aoa));
  public static readonly StyledProperty<double> SsaProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(Ssa));
  public static readonly StyledProperty<double> XTrackErrorProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(XTrackError));
  public static readonly StyledProperty<double> TurnRateProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(TurnRate));
  public static readonly StyledProperty<double> BatteryVoltage2Property =
      AvaloniaProperty.Register<HudControl, double>(nameof(BatteryVoltage2));
  public static readonly StyledProperty<int> BatteryRemaining2Property =
      AvaloniaProperty.Register<HudControl, int>(nameof(BatteryRemaining2));
  public static readonly StyledProperty<double> CurrentAmps2Property =
      AvaloniaProperty.Register<HudControl, double>(nameof(CurrentAmps2));
  public static readonly StyledProperty<double> ThrottlePercentProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(ThrottlePercent));
  public static readonly StyledProperty<bool> FailsafeProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(Failsafe));
  public static readonly StyledProperty<bool> SafetyActiveProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(SafetyActive));
  public static readonly StyledProperty<double> LinkQualityProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(LinkQuality), 100);
  public static readonly StyledProperty<bool> DisplayBattery2Property =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayBattery2), true);
  public static readonly StyledProperty<bool> DisplayAoaProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayAoa));
  public static readonly StyledProperty<bool> DisplayXTrackProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayXTrack), true);
  public static readonly StyledProperty<bool> DisplayConnectionProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(DisplayConnection), true);

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
  public double WindDir {
    get => GetValue(WindDirProperty);
    set => SetValue(WindDirProperty, value);
  }
  public double WindVel {
    get => GetValue(WindVelProperty);
    set => SetValue(WindVelProperty, value);
  }
  public double Aoa {
    get => GetValue(AoaProperty);
    set => SetValue(AoaProperty, value);
  }
  public double Ssa {
    get => GetValue(SsaProperty);
    set => SetValue(SsaProperty, value);
  }
  public double XTrackError {
    get => GetValue(XTrackErrorProperty);
    set => SetValue(XTrackErrorProperty, value);
  }
  public double TurnRate {
    get => GetValue(TurnRateProperty);
    set => SetValue(TurnRateProperty, value);
  }
  public double BatteryVoltage2 {
    get => GetValue(BatteryVoltage2Property);
    set => SetValue(BatteryVoltage2Property, value);
  }
  public int BatteryRemaining2 {
    get => GetValue(BatteryRemaining2Property);
    set => SetValue(BatteryRemaining2Property, value);
  }
  public double CurrentAmps2 {
    get => GetValue(CurrentAmps2Property);
    set => SetValue(CurrentAmps2Property, value);
  }
  public double ThrottlePercent {
    get => GetValue(ThrottlePercentProperty);
    set => SetValue(ThrottlePercentProperty, value);
  }
  public bool Failsafe {
    get => GetValue(FailsafeProperty);
    set => SetValue(FailsafeProperty, value);
  }
  public bool SafetyActive {
    get => GetValue(SafetyActiveProperty);
    set => SetValue(SafetyActiveProperty, value);
  }
  public double LinkQuality {
    get => GetValue(LinkQualityProperty);
    set => SetValue(LinkQualityProperty, value);
  }
  public bool DisplayBattery2 {
    get => GetValue(DisplayBattery2Property);
    set => SetValue(DisplayBattery2Property, value);
  }
  public bool DisplayAoa {
    get => GetValue(DisplayAoaProperty);
    set => SetValue(DisplayAoaProperty, value);
  }
  public bool DisplayXTrack {
    get => GetValue(DisplayXTrackProperty);
    set => SetValue(DisplayXTrackProperty, value);
  }
  public bool DisplayConnection {
    get => GetValue(DisplayConnectionProperty);
    set => SetValue(DisplayConnectionProperty, value);
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
        CustomItemsTextProperty,
        WindDirProperty,
        WindVelProperty,
        AoaProperty,
        SsaProperty,
        XTrackErrorProperty,
        TurnRateProperty,
        BatteryVoltage2Property,
        BatteryRemaining2Property,
        CurrentAmps2Property,
        ThrottlePercentProperty,
        FailsafeProperty,
        SafetyActiveProperty,
        LinkQualityProperty,
        DisplayBattery2Property,
        DisplayAoaProperty,
        DisplayXTrackProperty,
        DisplayConnectionProperty
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
          if (Math.Abs(y) > h * 0.38) {
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

    // ---- x-track error + rate-of-turn indicator (under heading, mirrors HUD.cs displayxtrack) ----
    if (DisplayXTrack) {
      DrawXTrackTurn(context, w, h, headH);
    }

    // ---- wind direction + speed arrow (top-right corner) ----
    DrawWind(context, w, headH, fontsize);

    // ---- AOA / SSA vertical bar (right) ----
    if (DisplayAoa) {
      DrawAoaSsa(context, w, h);
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
      // throttle % just under the air/ground readouts (mirrors HUD.cs ch3percent)
      DrawText(context, $"Thr {ThrottlePercent:0}%",
          new Point(2, speedRect.Bottom + fontsize * 2 + 12), fontsize, Brushes.White);
      // link quality / connection just above the speed tape (mirrors HUD.cs linkqualitygcs)
      if (DisplayConnection) {
        var lc = LinkQuality <= 0 ? Brushes.Red : LinkQuality < 50 ? Brushes.Orange : TextBrush;
        DrawText(context, $"{LinkQuality:0}%", new Point(2, speedRect.Top - fontsize - 4),
            fontsize, lc);
      }
    }
    if (DisplayAlt) {
      DrawScrollTape(context, altRect, Alt, TargetAlt, true, fontsize, "");
      DrawVsi(context, altRect, VerticalSpeed);
      var modeBrush = TextBrush;
      DrawText(context, Mode, new Point(altRect.Left - 4, altRect.Bottom + 4), fontsize, modeBrush);
    }

    // ---- bottom-left battery (Bat1, + optional Bat2 line above it) ----
    if (DisplayBattery) {
      var bb = BatteryRemaining > 0 && BatteryRemaining < 20 ? Brushes.Red
          : BatteryRemaining > 0 && BatteryRemaining < 30 ? Brushes.Orange
          : TextBrush;
      double batY = h - fontsize - 4;
      // Battery2 line (mirrors HUD.cs batteryon2 / Bat2) drawn just above Bat1.
      if (DisplayBattery2 && BatteryVoltage2 > 0) {
        string batt2 =
            $"Bat2 {BatteryVoltage2:0.00}v {CurrentAmps2:0.0} A {BatteryRemaining2}%";
        DrawText(context, batt2, new Point(2, batY - fontsize - 4), fontsize, TextBrush);
      } else if (BatteryCells > 0) {
        DrawText(context, $"Cell {BatteryVoltage / BatteryCells:0.00}v",
            new Point(2, batY - fontsize - 4), fontsize, bb);
      }
      string batt = $"Bat1 {BatteryVoltage:0.00}v {CurrentAmps:0.0} A {BatteryRemaining}%";
      DrawText(context, batt, new Point(2, batY), fontsize, bb);
    }

    // ---- bottom cluster (matches MP tempref): GPS bottom-right; EKF/Vibe + prearm
    //      centred at the bottom; AS/GS + battery bottom-left ----
    double byline = h - fontsize - 4;
    if (DisplayGps) {
      bool gps = SatCount >= 3;
      string g = gps ? $"GPS: 3D Fix ({SatCount:0})" : "GPS: No Fix";
      DrawTextRight(context, g, w - 6, byline, fontsize, gps ? Brushes.LimeGreen : Brushes.Red);
    }
    _ekfRect = _vibeRect = _prearmRect = default;
    if (DisplayEkf || DisplayVibe) {
      double ekfW = DisplayEkf ? MakeText("EKF", fontsize, null).Width : 0;
      double vibeW = DisplayVibe ? MakeText("Vibe", fontsize, null).Width : 0;
      double gap = DisplayEkf && DisplayVibe ? 16 : 0;
      double startX = cx - (ekfW + gap + vibeW) / 2;
      if (DisplayEkf) {
        DrawText(context, "EKF", new Point(startX, byline), fontsize, Brushes.White);
        _ekfRect = new Rect(startX, byline, ekfW, fontsize + 4);
      }
      if (DisplayVibe) {
        double vx = startX + ekfW + gap;
        DrawText(context, "Vibe", new Point(vx, byline), fontsize, Brushes.White);
        _vibeRect = new Rect(vx, byline, vibeW, fontsize + 4);
      }
    }
    if (DisplayPrearm && !Armed) {
      double py = byline - fontsize - 6;
      DrawTextCenter(context, "Not Ready to Arm", cx, py, fontsize, Brushes.Red);
      double pw = MakeText("Not Ready to Arm", fontsize, null).Width;
      _prearmRect = new Rect(cx - pw / 2, py, pw, fontsize + 4);
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
    DrawTextCenter(context, Armed ? "ARMED" : "DISARMED", cx, h / 3.0, fontsize + 10, Brushes.Red);

    // ---- alert flashes (failsafe / safety / low-voltage), mirrors HUD.cs ----
    // Half-second blink so the alerts grab attention, like the upstream flashing text.
    bool lowVoltage = BatteryRemaining > 0 && BatteryRemaining < 20;
    bool blink = DateTime.UtcNow.Millisecond < 500;
    double ay = h / 3.0 + fontsize + 14;
    if (SafetyActive) {
      DrawTextCenter(context, "SAFE", cx, ay, fontsize + 10, Brushes.Red);
      ay += fontsize + 16;
    }
    if (Failsafe && blink) {
      DrawTextCenter(context, "FAILSAFE", cx, ay, fontsize + 16, Brushes.Red);
      ay += fontsize + 22;
    }
    if (lowVoltage && blink) {
      DrawTextCenter(context, "LOW VOLTAGE", cx, ay, fontsize + 8, Brushes.Red);
    }
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

  // X-track error (green bar) + rate-of-turn marker under the heading tape.
  // Faithful port of HUD.cs displayxtrack block (centred at Width/10 like upstream).
  private void DrawXTrackTurn(DrawingContext ctx, double w, double h, double headH) {
    double xtspace = w / 10.0 / 3.0;
    double cx = w / 10.0;
    double top = headH + 5;
    double bot = headH + h / 10.0;
    double pad = 10;

    double xt = Math.Clamp(XTrackError, -40, 40);
    double loc = xt / 20.0 * xtspace;
    var green = new Pen(
        new SolidColorBrush(Color.FromArgb(Math.Abs(xt) >= 40 ? (byte)128 : (byte)255, 0, 200, 0)), 2);
    ctx.DrawLine(green, new Point(cx + loc, top), new Point(cx + loc, bot));
    ctx.DrawLine(WhitePen, new Point(cx, top), new Point(cx, bot));
    foreach (int s in new[] { -2, -1, 1, 2 }) {
      ctx.DrawLine(ThinPen, new Point(cx + s * xtspace, top + pad),
          new Point(cx + s * xtspace, bot - pad));
    }

    // rate-of-turn: three reference ticks + a green slider clamped to ±6 deg/s.
    double trY = bot + 10;
    var wp = new Pen(Brushes.White, 4);
    foreach (int s in new[] { -2, 0, 2 }) {
      double bx = cx + s * xtspace - xtspace / 2;
      ctx.DrawLine(wp, new Point(bx, trY), new Point(bx + xtspace, trY));
    }
    double trwidth = (cx + 2 * xtspace - xtspace / 2) - (cx - 2 * xtspace - xtspace / 2);
    double range = 12;
    double tr = Math.Clamp(TurnRate, -range / 2, range / 2);
    double tloc = tr / range * trwidth;
    var trPen = new Pen(
        new SolidColorBrush(Color.FromArgb(Math.Abs(tr) >= range / 2 ? (byte)128 : (byte)255, 0, 200, 0)), 4);
    ctx.DrawLine(trPen, new Point(cx + tloc - xtspace / 2, trY + 3),
        new Point(cx + tloc + xtspace / 2, trY + 3));
    ctx.DrawLine(trPen, new Point(cx + tloc, trY + 3), new Point(cx + tloc, trY + 10));
  }

  // Wind direction (relative to heading) + speed, top-right corner.
  private void DrawWind(DrawingContext ctx, double w, double headH, double fontsize) {
    double cx = w - fontsize * 3;
    double cy = headH + fontsize * 2.5;
    double r = fontsize * 1.6;
    var ring = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1);
    ctx.DrawEllipse(null, ring, new Point(cx, cy), r, r);
    // arrow points the way the wind blows TO (wind_dir is where it comes FROM), relative to yaw.
    double a = (WindDir - Yaw + 180) * Math.PI / 180.0;
    double dx = Math.Sin(a), dy = -Math.Cos(a);
    var tip = new Point(cx + dx * r, cy + dy * r);
    var tail = new Point(cx - dx * r, cy - dy * r);
    var ap = new Pen(Brushes.Cyan, 2);
    ctx.DrawLine(ap, tail, tip);
    // arrowhead
    double ha = a + Math.PI;
    double left = ha + 0.4, right = ha - 0.4;
    ctx.DrawLine(ap, tip, new Point(tip.X + Math.Sin(left) * r * 0.4, tip.Y - Math.Cos(left) * r * 0.4));
    ctx.DrawLine(ap, tip, new Point(tip.X + Math.Sin(right) * r * 0.4, tip.Y - Math.Cos(right) * r * 0.4));
    DrawTextCenter(ctx, $"{WindVel:0.0}", cx, cy + r + 2, fontsize - 1, Brushes.Cyan);
  }

  // AOA / SSA coloured vertical scale + black arrow (mirrors HUD.cs displayAOASSA bands).
  private void DrawAoaSsa(DrawingContext ctx, double w, double h) {
    double halfh = h / 2;
    double left = w - w / 6.0;
    double top = halfh + halfh / 10.0;
    double bw = w / 25.0;
    double bh = h / 5.0;
    const double redSSAp = 90, yellowSSAp = 60, greenSSAp = 10, critAOA = 25;
    ctx.FillRectangle(Brushes.Red, new Rect(left, top, bw, bh * (100 - redSSAp) / 100));
    ctx.FillRectangle(Brushes.Yellow,
        new Rect(left, top + bh * (100 - redSSAp) / 100, bw, bh * (redSSAp - yellowSSAp) / 100));
    ctx.FillRectangle(Brushes.Green,
        new Rect(left, top + bh * (100 - yellowSSAp) / 100, bw, bh * (yellowSSAp - greenSSAp) / 100));
    ctx.FillRectangle(Brushes.Blue,
        new Rect(left, top + bh * (100 - greenSSAp) / 100, bw, bh * greenSSAp / 100));
    ctx.DrawRectangle(null, WhitePen, new Rect(left, top, bw, bh));

    double ind = bh * (100 - greenSSAp) / 100 - (Aoa / critAOA) * (bh * (redSSAp - greenSSAp) / 100);
    ind = Math.Clamp(ind, 0, bh);
    var arrow = new StreamGeometry();
    using (var g = arrow.Open()) {
      g.BeginFigure(new Point(left + bw / 5, top + ind), true);
      g.LineTo(new Point(left - bw / 2 + bw / 5, top + bw / 2 + ind));
      g.LineTo(new Point(left - bw / 2 + bw / 5, top - bw / 2 + ind));
      g.EndFigure(true);
    }
    ctx.DrawGeometry(Brushes.Black, WhitePen, arrow);
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

  private void DrawTextCenter(DrawingContext ctx, string text, double centerX, double y, double size,
      IBrush? brush = null) {
    var ft = MakeText(text, size, brush);
    ctx.DrawText(ft, new Point(centerX - ft.Width / 2, y));
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

  protected override void OnPointerPressed(PointerPressedEventArgs e) {
    base.OnPointerPressed(e);
    var p = e.GetPosition(this);
    string? which = _ekfRect.Contains(p) ? "ekf"
        : _vibeRect.Contains(p) ? "vibe"
        : _prearmRect.Contains(p) ? "prearm"
        : null;
    if (which != null) {
      IndicatorClicked?.Invoke(which);
      e.Handled = true;
    }
  }
}
