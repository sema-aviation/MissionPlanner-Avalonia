using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MissionPlannerAvalonia.Controls;

public class FormField : ContentControl {
  public static readonly StyledProperty<string?> LabelProperty =
      AvaloniaProperty.Register<FormField, string?>(nameof(Label));

  public static readonly StyledProperty<double> LabelWidthProperty =
      AvaloniaProperty.Register<FormField, double>(nameof(LabelWidth), 140);

  public static readonly StyledProperty<Orientation> OrientationProperty =
      AvaloniaProperty.Register<FormField, Orientation>(nameof(Orientation), Orientation.Horizontal);

  public string? Label {
    get => GetValue(LabelProperty);
    set => SetValue(LabelProperty, value);
  }

  public double LabelWidth {
    get => GetValue(LabelWidthProperty);
    set => SetValue(LabelWidthProperty, value);
  }

  public Orientation Orientation {
    get => GetValue(OrientationProperty);
    set => SetValue(OrientationProperty, value);
  }
}
