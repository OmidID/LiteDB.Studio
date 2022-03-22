using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;

namespace LiteDB.Studio
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
	        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        static AppBuilder BuildAvaloniaApp()
        {
	        return AppBuilder.Configure<App>()
		        .UsePlatformDetect()
		        .WithIcons(container => container
			        .Register<FontAwesomeIconProvider>());
        }
    }
}
