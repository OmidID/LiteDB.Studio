using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using LiteDB.Studio.ViewModels;
using TextMateSharp.Grammars;

#if TREE_DATA_GRID
using Avalonia.Controls.Models.TreeDataGrid;
using AppDataGrid = Avalonia.Controls.TreeDataGrid;
#else
using AppDataGrid = Avalonia.Controls.DataGrid;
#endif

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

			SetupUi();
			SetupViewModel();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		private void SetupUi()
		{
			SetupEditor();
			SetupResultEditor();
			SetupParameterEditor();
		}

		private void SetupViewModel()
		{
			DataContext = ViewModel = new MainPageViewModel();

			ViewModel.InverseResultCommand = Command.Create<TaskData>(RenderResult, () => false);
			ViewModel.InvertShowOpenDialogCommand = Command.Create(InvokeShowOpenDialog, () => false);
			ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
			_ = ViewModel.CheckLastRecentConnectionAsync();
		}

		private void ViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(ViewModel.ActiveTask):
					UpdateEditors();
					break;
				case nameof(ViewModel.Connected):
					ConnectionStatusChanged();
					break;
			}
		}

		private void ConnectionStatusChanged()
		{
			if (!ViewModel.Connected)
			{
				this.FindControl<AppDataGrid>("DataGrid").Clear();
				_textEditor.Text = "";
				_textResultEditor.Text = "";
				_textResultEditor.Text = "";
			}
		}

		private void UpdateEditors()
		{
			_textEditor.Text = ViewModel.ActiveTask?.EditorContent;
		}


		private async void InvokeShowOpenDialog()
		{
			var connectionPage = new ConnectionPage();
			_ = await connectionPage.ShowDialog<ConnectionString>(this);
		}

		private void RenderResult(TaskData data)
		{
			 if (data == null) return;

			 this.FindControl<AppDataGrid>("DataGrid").Clear();
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
				 this.FindControl<AppDataGrid>("DataGrid").BindErrorMessage(data.Sql, data.Exception);
				 return;
			 }

			 if (data.Result == null)
				 return;

			 this.FindControl<AppDataGrid>("DataGrid").BindBsonData(data);
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

		private void OnCellEditEnded(object sender, DataGridCellEditEndedEventArgs e)
		{
			if (e.EditAction != DataGridEditAction.Commit) return;

			var bson = e.Row.DataContext as BsonValue;
			if (bson == null) return;

			var key = e.Column.Header?.ToString();
			if (string.IsNullOrEmpty(key)) return;

			ViewModel.CellUpdateCommand?.Execute((bson, key));
		}

		private void TreeViewItem_OnDoubleTapped(object sender, RoutedEventArgs e)
		{
			if (e.Source is StyledElement { DataContext: TreeNodeViewModel nodeViewModel })
			{
				ViewModel.CodeSnippedCommand.Execute(nodeViewModel);
			}
		}

		private void TabItemOnPointerReleased(object sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton == MouseButton.Middle && (sender as Control)?.Tag is TaskData task)
			{
				ViewModel.TabCloseCommand.Execute(task);
			}
		}
	}
}
