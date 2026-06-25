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
