using System;
using System.Collections.Generic;

namespace LiteDB.Studio;

[Serializable]
public class ApplicationSettings
{
	public ConnectionString LastConnectionStrings { get; set; }
	public List<ConnectionString> RecentConnectionStrings { get; set; }

	public int MaxRecentListItems { get; set; } = 10;
	public bool LoadLastDbOnStartup { get; set; }

	public ApplicationSettings()
	{
		RecentConnectionStrings = new List<ConnectionString>();
	}
}
