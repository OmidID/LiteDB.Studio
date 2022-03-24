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
		if (parameter is BsonDocument bson)
		{
			if (value == null)
			{
				bson.Remove(Key);
			}
			else
			{
				bson[Key] = new BsonValue(System.Convert.ChangeType(value, targetType));
			}

			return bson;
		}

		throw new InvalidCastException("Parameter is null or not a BsonDocument");
	}
}
