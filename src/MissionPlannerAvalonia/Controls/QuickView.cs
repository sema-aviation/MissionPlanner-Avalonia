using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MissionPlannerAvalonia.Controls;

// A single Quick-tab cell — port of MissionPlanner Controls/QuickView.cs: a small label on
// top and a big auto-sized number filling the rest. Double-click raises DoubleClicked so the
// host can pop the cs-field picker (mirrors quickView_DoubleClick).
public class QuickView : Control {
  public static readonly StyledProperty<double> NumberProperty =
      AvaloniaProperty.Register<QuickView, double>(nameof(Number));
  public static readonly StyledProperty<string> DescProperty =
      AvaloniaProperty.Register<QuickView, string>(nameof(Desc), "");
  public static readonly StyledProperty<string> NumberFormatProperty =
      AvaloniaProperty.Register<QuickView, string>(nameof(NumberFormat), "0.00");
  public static readonly StyledProperty<IBrush> NumberColorProperty =
      AvaloniaProperty.Register<QuickView, IBrush>(nameof(NumberColor), Brushes.White);

  public event Action<QuickView>? DoubleClicked;

  static QuickView() {
    AffectsRender<QuickView>(NumberProperty, DescProperty, NumberFormatProperty, NumberColorProperty);
  }

  public double Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }
  public string Desc { get => GetValue(DescProperty); set => SetValue(DescProperty, value); }
  public string NumberFormat {
    get => GetValue(NumberFormatProperty);
    set => SetValue(NumberFormatProperty, value);
  }
  public IBrush NumberColor {
    get => GetValue(NumberColorProperty);
    set => SetValue(NumberColorProperty, value);
  }

  public override void Render(DrawingContext ctx) {
    double w = Bounds.Width, h = Bounds.Height;
    if (w <= 0 || h <= 0) {
      return;
    }

    double descSize = Math.Max(9, h * 0.16);
    var descFt = new FormattedText(Desc, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        Typeface.Default, descSize, Brushes.White);
    ctx.DrawText(descFt, new Point(w / 2 - descFt.Width / 2, 4));
    double y = descFt.Height + 6;

    // auto-size the number to fill the remaining cell, like upstream's ratio fit.
    string numb = Number.ToString(NumberFormat, CultureInfo.InvariantCulture);
    double numSize = Math.Max(10, (h - y) * 0.9);
    var probe = new FormattedText(numb, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        Typeface.Default, numSize, NumberColor);
    if (probe.Width > w) {
      numSize *= w / probe.Width;
    }
    var numFt = new FormattedText(numb, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        Typeface.Default, Math.Max(8, numSize), NumberColor);
    ctx.DrawText(numFt,
        new Point(w / 2 - numFt.Width / 2, y + (h - y) / 2 - numFt.Height / 2));
  }

  protected override void OnPointerPressed(PointerPressedEventArgs e) {
    base.OnPointerPressed(e);
    if (e.ClickCount == 2) {
      DoubleClicked?.Invoke(this);
      e.Handled = true;
    }
  }
}
