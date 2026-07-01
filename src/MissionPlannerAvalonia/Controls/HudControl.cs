using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace MissionPlannerAvalonia.Controls;

public class HudControl : Control {

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

  public static readonly StyledProperty<bool> ShowIconsProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(ShowIcons), true);
  public static readonly StyledProperty<bool> RussianProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(Russian));
  public static readonly StyledProperty<int> BatteryCellsProperty =
      AvaloniaProperty.Register<HudControl, int>(nameof(BatteryCells));
  public static readonly StyledProperty<bool> GroundBrownProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(GroundBrown), false);

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
  public static readonly StyledProperty<bool> PrearmOkProperty =
      AvaloniaProperty.Register<HudControl, bool>(nameof(PrearmOk));
  public static readonly StyledProperty<int> GpsFixTypeProperty =
      AvaloniaProperty.Register<HudControl, int>(nameof(GpsFixType));
  public static readonly StyledProperty<double> WpDistProperty =
      AvaloniaProperty.Register<HudControl, double>(nameof(WpDist));
  public static readonly StyledProperty<int> WpNoProperty =
      AvaloniaProperty.Register<HudControl, int>(nameof(WpNo));

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
  public bool GroundBrown {
    get => GetValue(GroundBrownProperty);
    set => SetValue(GroundBrownProperty, value);
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
  public bool PrearmOk {
    get => GetValue(PrearmOkProperty);
    set => SetValue(PrearmOkProperty, value);
  }
  public int GpsFixType {
    get => GetValue(GpsFixTypeProperty);
    set => SetValue(GpsFixTypeProperty, value);
  }
  public double WpDist {
    get => GetValue(WpDistProperty);
    set => SetValue(WpDistProperty, value);
  }
  public int WpNo {
    get => GetValue(WpNoProperty);
    set => SetValue(WpNoProperty, value);
  }

  private static readonly IBrush _skyBrush = new LinearGradientBrush {
    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
    GradientStops =
      {
            new GradientStop(Color.Parse("#3A6FB0"), 0),
            new GradientStop(Color.Parse("#7FB3E0"), 1),
        },
  };
  private static readonly IBrush _groundBrush = new LinearGradientBrush {
    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
    GradientStops =
      {
            new GradientStop(Color.Parse("#9BB824"), 0),
            new GradientStop(Color.Parse("#414F07"), 1),
        },
  };

  private static readonly IBrush _groundBrownBrush = new LinearGradientBrush {
    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
    GradientStops =
      {
            new GradientStop(Color.Parse("#934E01"), 0),
            new GradientStop(Color.Parse("#3C2104"), 1),
        },
  };
  private static readonly IBrush _tape = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
  private static readonly IBrush _textBrush = Brushes.White;
  private static readonly Pen _whitePen = new(Brushes.White, 1.5);
  private static readonly Pen _thinPen = new(Brushes.White, 1);

  private readonly DispatcherTimer _ease;
  private double _eRoll, _ePitch, _eYaw, _eAlt, _eAs, _eGs, _eVs;
  private bool _easeInit;

  public HudControl() {
    _ease = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
    _ease.Tick += (_, _) => StepEase();
  }

  protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
    base.OnAttachedToVisualTree(e);
    _ease.Start();
  }

  protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
    base.OnDetachedFromVisualTree(e);
    _ease.Stop();
  }

  private void StepEase() {
    if (!_easeInit) {
      _eRoll = Roll;
      _ePitch = Pitch;
      _eYaw = Yaw;
      _eAlt = Alt;
      _eAs = AirSpeed;
      _eGs = GroundSpeed;
      _eVs = VerticalSpeed;
      _easeInit = true;
      return;
    }
    const double k = 0.4;
    bool moving = false;
    moving |= Approach(ref _eRoll, Roll, k);
    moving |= Approach(ref _ePitch, Pitch, k);
    moving |= ApproachAngle(ref _eYaw, Yaw, k);
    moving |= Approach(ref _eAlt, Alt, k);
    moving |= Approach(ref _eAs, AirSpeed, k);
    moving |= Approach(ref _eGs, GroundSpeed, k);
    moving |= Approach(ref _eVs, VerticalSpeed, k);
    if (moving) {
      InvalidateVisual();
    }
  }

  private static bool Approach(ref double v, double target, double k) {
    double d = target - v;
    if (Math.Abs(d) < 1e-3) {
      v = target;
      return false;
    }
    v += d * k;
    return true;
  }

  private static bool ApproachAngle(ref double v, double target, double k) {
    double d = ((target - v + 540) % 360) - 180;
    if (Math.Abs(d) < 1e-3) {
      v = target;
      return false;
    }
    v = (v + d * k + 360) % 360;
    return true;
  }

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
        PrearmOkProperty,
        GpsFixTypeProperty,
        ModeProperty,
        BatteryVoltageProperty,
        BatteryRemainingProperty,
        ShowIconsProperty,
        RussianProperty,
        BatteryCellsProperty,
        GroundBrownProperty,
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
        DisplayConnectionProperty,
        WpDistProperty,
        WpNoProperty
    );
  }

  private DateTime _modeChanged = DateTime.MinValue;

  protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
    base.OnPropertyChanged(change);
    if (change.Property == ModeProperty) {
      _modeChanged = DateTime.Now;
    }
  }

  private IBrush GroundFill() => GroundBrown ? _groundBrownBrush : _groundBrush;

  public override void Render(DrawingContext context) {
    var b = Bounds;
    double w = b.Width,
        h = b.Height;
    if (w <= 0 || h <= 0) {
      return;
    }

    context.FillRectangle(Brushes.Black, new Rect(0, 0, w, h));

    double unit = Math.Min(w, h);
    double fontsize = Math.Clamp(unit / 28.0, 9, 30);
    double cx = w / 2,
        cy = h / 2;
    double perDeg = h / 65.0;
    double headH = Math.Clamp(h / 14.0, 16, 48);
    var ground = GroundFill();

    double rollRad = (Russian ? 0 : _eRoll) * Math.PI / 180.0;
    using (context.PushClip(new Rect(0, headH, w, h - headH)))
    using (context.PushTransform(Matrix.CreateTranslation(cx, cy)))
    using (context.PushTransform(Matrix.CreateRotation(-rollRad))) {
      double big = Math.Max(w, h) * 2;
      double pitchoffset = _ePitch * perDeg;
      context.FillRectangle(_skyBrush, new Rect(-big, -big, big * 2, big + pitchoffset));
      context.FillRectangle(ground, new Rect(-big, pitchoffset, big * 2, big * 2 - pitchoffset));
      context.DrawLine(_whitePen, new Point(-big, pitchoffset), new Point(big, pitchoffset));

      if (DisplayRollPitch) {
        for (int a = -90; a <= 90; a += 5) {
          if (a == 0) {
            continue;
          }

          double y = (_ePitch - a) * perDeg;
          if (Math.Abs(y) > h * 0.38) {
            continue;
          }

          bool major = a % 10 == 0;
          double len = major ? unit * 0.11 : unit * 0.075;
          context.DrawLine(_thinPen, new Point(-len / 2, y), new Point(len / 2, y));
          if (major) {
            var br = _textBrush;
            DrawText(context, Math.Abs(a).ToString(), new Point(len / 2 + 3, y - fontsize), fontsize,
                br);
            DrawText(context, Math.Abs(a).ToString(), new Point(-len / 2 - fontsize * 1.6,
                y - fontsize), fontsize, br);
          }
        }
      }
    }

    if (DisplayRollPitch) {
      DrawRollArc(context, cx, cy, Math.Max(0, Math.Min(unit * 0.46, cy - headH - unit * 0.05)), unit);
    }

    double wing = unit * 0.11;
    double rgap = unit * 0.035;
    double drop = unit * 0.05;
    double caretHalf = unit * 0.04;
    var reticle = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 0, 0)),
        Math.Max(2.5, unit * 0.009), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
    using (context.PushTransform(Russian
        ? Matrix.CreateRotation(-_eRoll * Math.PI / 180.0, new Point(cx, cy))
        : Matrix.Identity)) {
      context.DrawLine(reticle, new Point(cx - rgap - wing, cy), new Point(cx - rgap, cy));
      context.DrawLine(reticle, new Point(cx + rgap, cy), new Point(cx + rgap + wing, cy));
      context.DrawLine(reticle, new Point(cx - caretHalf, cy), new Point(cx, cy + drop));
      context.DrawLine(reticle, new Point(cx, cy + drop), new Point(cx + caretHalf, cy));
    }

    if (DisplayHeading) {
      DrawHeadingTape(context, w, headH, fontsize, _eYaw, NavBearing);
    }

    if (DisplayConnection) {
      var lc = LinkQuality <= 0 ? Brushes.Red : LinkQuality < 50 ? Brushes.Orange : _textBrush;
      DrawTextRight(context, $"{LinkQuality:0}%", w - 8, headH + 6, fontsize + 6, lc, outline: true);
    }

    if (DisplayXTrack) {
      DrawXTrack(context, w, h, headH);
    }

    if (DisplayAoa) {
      DrawAoaSsa(context, w, h);
    }

    double tapeW = Math.Max(20, Math.Min(Math.Max(unit * 0.12, 42), w * 0.16));
    var speedRect = new Rect(0, cy - h / 4.0, tapeW, h / 2.0);
    var altRect = new Rect(w - tapeW, cy - h / 4.0, tapeW, h / 2.0);
    double spd = AirSpeed > 0 ? _eAs : _eGs;
    if (DisplaySpeed) {
      DrawScrollTape(context, speedRect, spd, TargetSpeed, false, fontsize);
      DrawText(context, $"AS {AirSpeed:0.0}", new Point(2, speedRect.Bottom + 4), fontsize);
      DrawText(context, $"GS {GroundSpeed:0.0}", new Point(2, speedRect.Bottom + fontsize + 8),
          fontsize);

      DrawText(context, $"Thr {ThrottlePercent:0}%",
          new Point(2, speedRect.Bottom + fontsize * 2 + 12), fontsize, Brushes.White);
    }
    if (DisplayAlt) {
      DrawScrollTape(context, altRect, _eAlt, TargetAlt, true, fontsize);
      DrawVsi(context, altRect, _eVs);

      var modeBrush = _modeChanged.AddSeconds(2) > DateTime.Now ? Brushes.Red : _textBrush;
      DrawText(context, Mode, new Point(altRect.Left - 30, altRect.Bottom + 5), fontsize, modeBrush);
      double newdist = WpDist;
      string newdistunit = "m";
      if (newdist >= 1000) {
        newdistunit = "k";
        newdist = Math.Round(newdist / 1000.0, 1);
      } else {
        newdist = (int)newdist;
      }
      DrawText(context, $"{newdist}{newdistunit}>{WpNo}",
          new Point(altRect.Left - 30, altRect.Bottom + fontsize + 2 + 10), fontsize, _textBrush);
    }

    if (DisplayBattery) {
      var bb = BatteryRemaining > 0 && BatteryRemaining < 20 ? Brushes.Red
          : BatteryRemaining > 0 && BatteryRemaining < 30 ? Brushes.Orange
          : _textBrush;
      double batY = h - fontsize - 4;

      if (DisplayBattery2 && BatteryVoltage2 > 0) {
        string batt2 =
            $"Bat2 {BatteryVoltage2:0.00}v {CurrentAmps2:0.0} A {BatteryRemaining2}%";
        DrawText(context, batt2, new Point(2, batY - fontsize - 4), fontsize, _textBrush);
      } else if (BatteryCells > 0) {
        DrawText(context, $"Cell {BatteryVoltage / BatteryCells:0.00}v",
            new Point(2, batY - fontsize - 4), fontsize, bb);
      }
      string batt = $"Bat1 {BatteryVoltage:0.00}v {CurrentAmps:0.0} A {BatteryRemaining}%";
      DrawText(context, batt, new Point(2, batY), fontsize, bb);
    }

    double byline = h - fontsize - 4;
    if (DisplayGps) {

      string g = GpsFixText(GpsFixType, (int)SatCount);
      var gpsBrush = GpsFixType >= 3 ? _gpsGoodBrush : GpsFixType >= 2 ? Brushes.Orange : Brushes.Red;
      DrawTextRight(context, g, w - 6, byline, fontsize, gpsBrush, outline: true);
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
      string ptext = PrearmOk ? "Ready to Arm" : "Not Ready to Arm";
      var pbrush = PrearmOk ? Brushes.LimeGreen : Brushes.Red;
      DrawTextCenter(context, ptext, cx, py, fontsize, pbrush);
      double pw = MakeText(ptext, fontsize, null).Width;
      _prearmRect = new Rect(cx - pw / 2, py, pw, fontsize + 4);
    }

    if (!string.IsNullOrEmpty(CustomItemsText)) {
      double yy = headH + 4;
      foreach (var line in CustomItemsText.Split('\n')) {
        DrawText(context, line, new Point(4, yy), fontsize, Brushes.White);
        yy += fontsize + 3;
      }
    }

    DrawTextCenter(context, Armed ? "ARMED" : "DISARMED", cx, h / 3.0, fontsize + 10, Brushes.Red);

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
    ctx.FillRectangle(_tape, bg);
    ctx.DrawRectangle(null, new Pen(Brushes.Black, 2), bg);
    double space = (w - 10) / 120.0;
    int yawi = (int)Math.Round(yaw);
    for (int d = -60; d <= 60; d++) {
      int hdg = ((yawi + d) % 360 + 360) % 360;
      double x = w / 2 + d * space;
      bool major = hdg % 15 == 0;
      if (hdg % 5 == 0) {
        ctx.DrawLine(_whitePen, new Point(x, headH - 5), new Point(x, headH - 10));
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

    if (navBearing != 0) {
      double delta = ((navBearing - yaw + 540) % 360) - 180;
      if (Math.Abs(delta) >= 4 && Math.Abs(delta) <= 60) {
        double nx = w / 2 + delta * space;
        ctx.DrawLine(new Pen(Brushes.Green, 6), new Point(nx, 0), new Point(nx, headH));
      }
    }

    double bw = fontsize * 2.6;
    var box = new Rect(w / 2 - bw / 2, 0, bw, headH);
    ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), box);
    DrawText(ctx, yawi.ToString("000"), new Point(w / 2 - fontsize, headH / 2 - fontsize / 2),
        fontsize, Brushes.Black);
  }

  private void DrawXTrack(DrawingContext ctx, double w, double h, double headH) {
    double xtspace = w / 10.0 / 3.0;
    const double pad = 10;
    double xtrack = Math.Clamp(XTrackError, -40, 40);
    double loc = xtrack / 20.0 * xtspace;

    var green = new Pen(Brushes.Green, 2);
    var greenFaint = new Pen(new SolidColorBrush(Color.FromArgb(128, 0, 128, 0)), 2);
    var white = new Pen(Brushes.White, 2);
    double top = headH + 5;
    double bot = headH + h / 10;

    ctx.DrawLine(Math.Abs(xtrack) == 40 ? greenFaint : green,
        new Point(w / 10 + loc, top), new Point(w / 10 + loc, bot));

    ctx.DrawLine(white, new Point(w / 10, top), new Point(w / 10, bot));
    ctx.DrawLine(white, new Point(w / 10 - xtspace, top + pad), new Point(w / 10 - xtspace, bot - pad));
    ctx.DrawLine(white, new Point(w / 10 - xtspace * 2, top + pad),
        new Point(w / 10 - xtspace * 2, bot - pad));
    ctx.DrawLine(white, new Point(w / 10 + xtspace, top + pad), new Point(w / 10 + xtspace, bot - pad));
    ctx.DrawLine(white, new Point(w / 10 + xtspace * 2, top + pad),
        new Point(w / 10 + xtspace * 2, bot - pad));

    var white4 = new Pen(Brushes.White, 4);
    double trY = headH + h / 10 + 10;
    ctx.DrawLine(white4, new Point(w / 10 - xtspace * 2 - xtspace / 2, trY),
        new Point(w / 10 - xtspace * 2 - xtspace / 2 + xtspace, trY));
    ctx.DrawLine(white4, new Point(w / 10 - xtspace / 2, trY),
        new Point(w / 10 - xtspace / 2 + xtspace, trY));
    ctx.DrawLine(white4, new Point(w / 10 + xtspace * 2 - xtspace / 2, trY),
        new Point(w / 10 + xtspace * 2 - xtspace / 2 + xtspace, trY));

    const double range = 12;
    double turnrate = Math.Clamp(TurnRate, -range / 2, range / 2);
    double trwidth = (w / 10 + xtspace * 2 - xtspace / 2) - (w / 10 - xtspace * 2 - xtspace / 2);
    double trloc = turnrate / range * trwidth;
    var needle = Math.Abs(turnrate) == range / 2
        ? new Pen(new SolidColorBrush(Color.FromArgb(128, 0, 128, 0)), 4)
        : new Pen(Brushes.Green, 4);
    ctx.DrawLine(needle, new Point(w / 10 + trloc - xtspace / 2, trY + 3),
        new Point(w / 10 + trloc + xtspace / 2, trY + 3));
    ctx.DrawLine(needle, new Point(w / 10 + trloc, trY + 3), new Point(w / 10 + trloc, trY + 3 + 10));
  }

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
    ctx.DrawRectangle(null, _whitePen, new Rect(left, top, bw, bh));

    double ind = bh * (100 - greenSSAp) / 100 - (Aoa / critAOA) * (bh * (redSSAp - greenSSAp) / 100);
    ind = Math.Clamp(ind, 0, bh);
    var arrow = new StreamGeometry();
    using (var g = arrow.Open()) {
      g.BeginFigure(new Point(left + bw / 5, top + ind), true);
      g.LineTo(new Point(left - bw / 2 + bw / 5, top + bw / 2 + ind));
      g.LineTo(new Point(left - bw / 2 + bw / 5, top - bw / 2 + ind));
      g.EndFigure(true);
    }
    ctx.DrawGeometry(Brushes.Black, _whitePen, arrow);
  }

  private void DrawRollArc(DrawingContext ctx, double cx, double cy, double r, double unit) {
    var c = new Point(cx, cy);
    using (ctx.PushTransform(Matrix.CreateRotation(-_eRoll * Math.PI / 180.0, c))) {
      foreach (int a in new[] { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 }) {
        double rad = (a - 90) * Math.PI / 180.0;
        double len = a == 0 ? unit * 0.028 : unit * 0.016;
        ctx.DrawLine(_whitePen,
            new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad)),
            new Point(cx + (r - len) * Math.Cos(rad), cy + (r - len) * Math.Sin(rad)));
      }

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
      ctx.DrawGeometry(null, _whitePen, geo);
    }
    double ph = unit * 0.026;
    var pen = new Pen(Brushes.Red, Math.Max(2, unit * (Math.Abs(_eRoll) > 45 ? 0.008 : 0.005)),
        lineCap: PenLineCap.Round);
    ctx.DrawLine(pen, new Point(cx, cy - r), new Point(cx - ph * 0.7, cy - r + ph));
    ctx.DrawLine(pen, new Point(cx, cy - r), new Point(cx + ph * 0.7, cy - r + ph));
  }

  private void DrawScrollTape(DrawingContext ctx, Rect rect, double value, double target,
      bool leftSide, double fontsize) {
    ctx.FillRectangle(_tape, rect);
    ctx.DrawRectangle(null, _whitePen, rect);
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
            ctx.DrawLine(_whitePen, new Point(rect.Left, y), new Point(rect.Left + 8, y));
            DrawText(ctx, iv.ToString(), new Point(rect.Left + 10, y - fontsize / 2), fontsize - 1,
                Brushes.White);
          } else {
            ctx.DrawLine(_whitePen, new Point(rect.Right - 8, y), new Point(rect.Right, y));
            DrawText(ctx, iv.ToString(), new Point(rect.Left + 2, y - fontsize / 2), fontsize - 1,
                Brushes.White);
          }
        }
      }

      if (target != 0 && Math.Abs(target - value) < viewrange / 2) {
        double ty = midY - (target - value) * space;
        ctx.DrawLine(new Pen(Brushes.Green, 4), new Point(rect.Left, ty), new Point(rect.Right, ty));
      }
    }

    double bh = fontsize + 6;
    var box = new Rect(rect.Left, midY - bh / 2, rect.Width, bh);
    ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(210, 0, 0, 0)), box);
    ctx.DrawRectangle(null, _whitePen, box);
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
    ctx.DrawGeometry(null, _whitePen, box);
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

  private void DrawText(DrawingContext ctx, string text, Point at, double size, IBrush? brush = null,
      bool outline = true) {
    if (outline) {
      DrawHalo(ctx, text, at, size);
    }
    ctx.DrawText(MakeText(text, size, brush), at);
  }

  private double DrawTextRight(DrawingContext ctx, string text, double rightX, double y, double size,
      IBrush? brush = null, bool outline = true) {
    var ft = MakeText(text, size, brush);
    var at = new Point(rightX - ft.Width, y);
    if (outline) {
      DrawHalo(ctx, text, at, size);
    }
    ctx.DrawText(ft, at);
    return ft.Width;
  }

  private static readonly IBrush _gpsGoodBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xFF, 0x4C));

  private static string GpsFixText(int fix, int sats) => fix switch {
    <= 1 => "GPS: No Fix",
    2 => $"GPS: 2D Fix ({sats})",
    3 => $"GPS: 3D Fix ({sats})",
    4 => $"GPS: 3D DGPS ({sats})",
    5 => $"GPS: RTK Float ({sats})",
    _ => $"GPS: RTK Fixed ({sats})",
  };

  private void DrawHalo(DrawingContext ctx, string text, Point at, double size) {
    var dark = MakeText(text, size, Brushes.Black);
    ctx.DrawText(dark, new Point(at.X - 1, at.Y));
    ctx.DrawText(dark, new Point(at.X + 1, at.Y));
    ctx.DrawText(dark, new Point(at.X, at.Y - 1));
    ctx.DrawText(dark, new Point(at.X, at.Y + 1));
  }

  private void DrawTextCenter(DrawingContext ctx, string text, double centerX, double y, double size,
      IBrush? brush = null) {
    var ft = MakeText(text, size, brush);
    var at = new Point(centerX - ft.Width / 2, y);
    DrawHalo(ctx, text, at, size);
    ctx.DrawText(ft, at);
  }

  private static FormattedText MakeText(string text, double size, IBrush? brush) {
    return new FormattedText(
        text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        Typeface.Default,
        size,
        brush ?? _textBrush
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
