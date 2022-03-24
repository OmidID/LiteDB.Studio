using System.Configuration;

namespace LiteDB.Studio.Properties;

public class Settings : ApplicationSettingsBase
{
	public static Settings Default { get; } = (Settings)Synchronized(new Settings());

	[UserScopedSettingAttribute()]
	public string ApplicationSettings
	{
		get => (string)this[nameof(ApplicationSettings)];
		set => this[nameof(ApplicationSettings)] = value;
	}
}
