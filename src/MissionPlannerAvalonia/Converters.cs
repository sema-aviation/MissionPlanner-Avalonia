using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlannerAvalonia;

public class StringEqualsConverter : IValueConverter {
  public static readonly StringEqualsConverter Instance = new();

  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}

// Passes through only string content; returns null for anything else. Used by the Button ToolTip.Tip
// style so a button with a control (e.g. StackPanel) content does NOT alias that live control into a
// ToolTip (which crashes with "already has a visual parent"); only ellipsized text buttons get a tip.
public class StringOnlyConverter : IValueConverter {
  public static readonly StringOnlyConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is string s ? s : null;

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
