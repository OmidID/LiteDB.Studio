using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LiteDB.Studio.Converters;

public class BsonValueConverter : IValueConverter
{
	public string Key { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is not BsonDocument doc) return null;

		var rawValue = doc[Key]?.RawValue;
		return rawValue?.GetType() == targetType ? rawValue : rawValue?.ToString();
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (parameter is not BsonDocument bson)
			throw new InvalidCastException("Parameter is null or not a BsonDocument");

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
}
