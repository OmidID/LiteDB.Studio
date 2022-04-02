using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Markup.Xaml;
using LiteDB.Studio.Views;

namespace LiteDB.Studio
{
	public class App : Application
	{
		internal static IClassicDesktopStyleApplicationLifetime Lifetime => Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

		public override void Initialize()
		{
			ExpressionObserver.PropertyAccessors.Add(new AvaloniaBsonValuePropertyAccessorPlugin());
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
			{
				Lifetime.ShutdownMode = ShutdownMode.OnMainWindowClose;
				Lifetime.MainWindow = new MainPage();
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}
