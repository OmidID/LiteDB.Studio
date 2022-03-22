using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LiteDB.Studio.Converters;

public class BsonValueConverter : IValueConverter
{
	public string Key { get; set; }

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is BsonDocument doc)
		{
			return doc[Key].RawValue?.ToString();
		}

		return null;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value;
	}
}
