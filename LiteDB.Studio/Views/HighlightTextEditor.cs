using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace LiteDB.Studio.Views;

public class HighlightTextEditor : TextEditor
{
	private RegistryOptions _registryOptions;
	private TextMate.Installation _textMateInstallation;

	public void SetHighlight(string fileExtension)
	{
		_registryOptions ??= new RegistryOptions(ThemeName.Dark);
		_textMateInstallation ??= this.InstallTextMate(_registryOptions);

		_textMateInstallation.SetGrammar(
			_registryOptions.GetScopeByLanguageId(_registryOptions.GetLanguageByExtension(fileExtension).Id));
	}
}
