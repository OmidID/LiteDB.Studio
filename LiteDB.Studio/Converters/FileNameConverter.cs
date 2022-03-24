using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace LiteDB.Studio.Converters;

public class FileNameConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string path)
			return Path.GetFileName(path);

		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
