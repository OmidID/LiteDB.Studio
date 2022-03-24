using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using LiteDB.Studio.ViewServices;

namespace LiteDB.Studio.ViewModels;

public class ConnectionPageViewModel : ViewModelBase
{
	private string _filePath;

	public ICommand InvertCloseCommand { get; set; }

	public ICommand OpenCommand { get; }
	public ICommand CloseCommand { get; }
	public ICommand RecentCommand { get; }

	public ObservableCollection<ConnectionString> ConnectionStrings { get; }

	public string FilePath
	{
		get => _filePath;
		set => Set(ref _filePath, value);
	}

	public ConnectionPageViewModel()
	{
		OpenCommand = Command.Create(InvokeOpen);
		CloseCommand = Command.Create(InvokeClose);
		RecentCommand = Command.Create<ConnectionString>(OpenConnectionStringAndClose);
		ConnectionStrings = new ObservableCollection<ConnectionString>(
			AppSettingsManager.ApplicationSettings.RecentConnectionStrings);
	}

	private void InvokeClose()
	{
		InvertCloseCommand.Execute(null);
	}

	private void InvokeOpen()
	{
		if (!File.Exists(FilePath))
			return;

		var cnn = new ConnectionString(FilePath);

		OpenConnectionStringAndClose(cnn);
	}

	private void OpenConnectionStringAndClose(ConnectionString connectionString)
	{
		AppSettingsManager.ApplicationSettings.LastConnectionStrings = connectionString;
		AppSettingsManager.AddToRecentList(connectionString);
		AppSettingsManager.PersistData();

		BroadcastService.Instance.Broadcast(connectionString);
		InvertCloseCommand.Execute(connectionString);
	}
}
