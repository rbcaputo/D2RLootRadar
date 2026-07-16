using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace D2RLootRadar.Desktop.Converters;

/// <summary>
/// The negated counterpart to WPF's built-in <see cref="BooleanToVisibilityConverter"/>:
/// true → <see cref="Visibility.Collapsed"/>, false → <see cref="Visibility.Visible"/>.
/// 
/// Used for placeholder content that should only show up when a flag is <c>false</c>
/// (e.g. the rarity popup's "None" placeholder, shown only when nothing is selected)
/// rather than layering a separate bool property just to flip the sense of an existing one.
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
  public object Convert(
    object? value,
    Type targetType,
    object? parameter,
    CultureInfo culture
  ) => value is true ? Visibility.Collapsed : Visibility.Visible;

  public object ConvertBack(
    object? value,
    Type targetType,
    object? parameter,
    CultureInfo culture
  ) => value is Visibility.Visible;
}
