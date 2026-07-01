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

public class StringOnlyConverter : IValueConverter {
  public static readonly StringOnlyConverter Instance = new();

  public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is string s ? s : null;

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
