using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LiteDB.Studio.ViewModels;
using Markdown.Avalonia.Utils;

namespace LiteDB.Studio.Views;

public partial class ConnectionPage : Window
{
	public ConnectionPageViewModel ViewModel { get; set; }

	public ConnectionPage()
	{
		InitializeComponent();
		DataContext = ViewModel = new ConnectionPageViewModel();
		ViewModel.InvertCloseCommand = Command.Create<ConnectionString>(Close, () => false);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private async void Button_OnClick(object? sender, RoutedEventArgs e)
	{
		var openFileDialog = new OpenFileDialog()
		{
			Filters = new List<FileDialogFilter>
			{
				new()
				{
					Name = "Database file",
					Extensions = new List<string> { "db", "ldb", "litedb" }
				}
			},
			AllowMultiple = false,
			Title = "Select database file"
		};

		var result = await openFileDialog.ShowAsync(this);
		if (result?.Length > 0)
		{
			this.FindControl<TextBox>("FileTextBox").Text = result.FirstOrDefault();
		}
	}
}

