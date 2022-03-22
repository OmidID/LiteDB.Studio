using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiteDB.Studio.Views;

namespace LiteDB.Studio
{
	public class App : Application
	{
		internal static IClassicDesktopStyleApplicationLifetime Lifetime => Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
			{
				Lifetime.MainWindow = new MainPage();
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
