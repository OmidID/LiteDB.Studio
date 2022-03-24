using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;

namespace LiteDB.Studio.ViewServices;

public class DialogService
{
	public static DialogService Instance { get; } = new DialogService();

	public async Task ShowErrorAsync(string message)
	{
		var messageBoxCustomWindow = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxCustomWindow(
			new MessageBoxCustomParams
			{
				ContentTitle = "Error!",
				ContentMessage = message,
				Icon = Icon.Error,
				WindowIcon = null,
				ButtonDefinitions = new[]
					{ new ButtonDefinition { Name = "Close", IsDefault = true }, },
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
			});
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } } classic)
			await messageBoxCustomWindow.ShowDialog(classic.MainWindow);
		else
			await messageBoxCustomWindow.Show();
	}
}
