using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using LiteDB.Studio.ViewModels;
using TextMateSharp.Grammars;

namespace LiteDB.Studio.Views
{
	public class MainPage : Window
	{
		private TextEditor _textEditor;
		private RegistryOptions _registryOptions;
		private TextMate.Installation _textMateInstallation;

		private TextEditor _textResultEditor;
		private RegistryOptions _textResultRegistryOptions;
		private TextMate.Installation _resultTextMateInstallation;

		private TextEditor _textParameterEditor;
		private RegistryOptions _textParameterRegistryOptions;
		private TextMate.Installation _parameterTextMateInstallation;
		public MainPageViewModel ViewModel { get; set; }

		public MainPage()
		{
			InitializeComponent();
#if DEBUG
			//this.AttachDevTools();
#endif

			SetupEditor();
			SetupResultEditor();
			SetupParameterEditor();

			DataContext = ViewModel = new MainPageViewModel();
			SetupViewModel();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void SetupViewModel()
		{
			ViewModel.InverseResultCommand = Command.Create<TaskData>(RenderResult);
			ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
			_ = ViewModel.InitialiseAsync("test.db");
		}

		private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ViewModel.ActiveTask))
			{
				UpdateEditors();
			}
		}

		private void UpdateEditors()
		{
			_textEditor.Text = ViewModel.ActiveTask?.EditorContent;
		}

		private void RenderResult(TaskData data)
		{
			 if (data == null) return;

			 this.FindControl<DataGrid>("DataGrid").Clear();
			 _textResultEditor.Text = "";
			 _textResultEditor.Text = "";

			 if (data.Executing || data != ViewModel.ActiveTask)
			 {
				 return;
			 }

			 if (data.Exception != null)
			 {
				 _textResultEditor.BindErrorMessage(_resultTextMateInstallation, _textResultRegistryOptions, data.Sql, data.Exception);
				 _textParameterEditor.BindErrorMessage(_parameterTextMateInstallation, _textParameterRegistryOptions, data.Sql, data.Exception);
				 this.FindControl<DataGrid>("DataGrid").BindErrorMessage(data.Sql, data.Exception);
				 return;
			 }

			 if (data.Result == null)
				 return;

			 this.FindControl<DataGrid>("DataGrid")
				 .BindBsonData(data);
			 _textResultEditor.BindBsonData(_resultTextMateInstallation, _textResultRegistryOptions, data);
			 _textParameterEditor.BindParameter(_parameterTextMateInstallation, _textParameterRegistryOptions, data);
		}

		private void SetupEditor()
		{
			_textEditor ??= this.FindControl<TextEditor>("Editor");

			_textEditor.TextArea.Caret.PositionChanged += CaretOnPositionChanged;
			//_textEditor.SetHighlight(".sql");

			_registryOptions ??= new RegistryOptions(ThemeName.Dark);
			_textMateInstallation ??= _textEditor.InstallTextMate(_registryOptions);

			_textMateInstallation.SetGrammar(
				_registryOptions.GetScopeByLanguageId(_registryOptions.GetLanguageByExtension(".sql").Id));
		}

		private void SetupResultEditor()
		{
			_textResultEditor ??= this.FindControl<TextEditor>("ResultEditor");

			_textResultRegistryOptions ??= new RegistryOptions(ThemeName.Dark);
			_resultTextMateInstallation ??= _textResultEditor.InstallTextMate(_textResultRegistryOptions);

			_resultTextMateInstallation.SetGrammar(
				_textResultRegistryOptions.GetScopeByLanguageId(_textResultRegistryOptions.GetLanguageByExtension(".json").Id));
		}

		private void SetupParameterEditor()
		{
			_textParameterEditor ??= this.FindControl<TextEditor>("ParameterEditor");

			_textParameterRegistryOptions ??= new RegistryOptions(ThemeName.Dark);
			_parameterTextMateInstallation ??= _textParameterEditor.InstallTextMate(_textParameterRegistryOptions);

			_parameterTextMateInstallation.SetGrammar(
				_textParameterRegistryOptions.GetScopeByLanguageId(_textParameterRegistryOptions.GetLanguageByExtension(".json").Id));
		}

		private void CaretOnPositionChanged(object sender, EventArgs e)
		{
			if (ViewModel?.ActiveTask == null)
				return;

			ViewModel.UpdateTextPositionCommand.Execute((_textEditor.TextArea.Caret.Line, _textEditor.TextArea.Caret.Column));
		}

		private void Editor_OnTextChanged(object sender, EventArgs e)
		{
			if (ViewModel?.ActiveTask == null)
				return;
			ViewModel.ActiveTask.EditorContent = _textEditor.Text;
		}

		private void TreeViewItem_OnDoubleTapped(object sender, RoutedEventArgs e)
		{
			if (e.Source is StyledElement element && element.DataContext is TreeNodeViewModel nodeViewModel)
			{
				ViewModel.CodeSnippedCommand.Execute(nodeViewModel);
			}
		}
	}
}
