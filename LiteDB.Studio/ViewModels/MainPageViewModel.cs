using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LiteDB.Studio.ViewModels;

public class MainPageViewModel : ViewModelBase
{
	private readonly SynchronizationContext? _synchronizationContext;

	private ConnectionString _connectionString = null;
	private LiteDatabase _db;
	private DatabaseDebugger _debugger = null;
	private TaskData _activeTask;
	private bool _isActive;

	public ICommand InverseResultCommand { get; set; }
	public ICommand RunCommand { get; }
	public ICommand UpdateTextPositionCommand { get; }
	public ICommand CodeSnippedCommand { get; }
	public ObservableCollection<TaskData> Tasks { get; } = new();
	public ObservableCollection<TreeNodeViewModel> TreeViewNodes { get; } = new();

	public TaskData ActiveTask
	{
		get => _activeTask;
		set
		{
			if (Set(ref _activeTask, value))
				InvokeSelectedTaskChanged(value);
		}
	}

	public bool IsActive
	{
		get => _isActive;
		private set => Set(ref _isActive, value);
	}

	public bool Loading => ActiveTask?.Executing ?? false;

	public string TextPosition => $"Line: {ActiveTask?.Position.Item1} - Column: {ActiveTask?.Position.Item2}";

	public MainPageViewModel()
	{
		_synchronizationContext = SynchronizationContext.Current;
		RunCommand = Command.Create(InvokeRun);
		UpdateTextPositionCommand = Command.Create<(int, int)>(InvokeUpdatePosition);
		CodeSnippedCommand = Command.Create<TreeNodeViewModel>(InvokeCodeSnipped);
	}

	public async Task InitialiseAsync(string filename)
	{
		if (string.IsNullOrWhiteSpace(filename))
		{
			Disconnect();
		}
		else
		{
			await InternalInitialiseAsync(new ConnectionString(filename));
			AddNewTask("");
			Tasks.Add(new TaskData());
			IsActive = true;
		}
	}

	private void Disconnect()
	{
		foreach (var task in Tasks)
		{
			task.ThreadRunning = false;
			task.WaitHandle.Set();
		}

		Tasks.Clear();

		try
		{
			_debugger?.Dispose();
			_debugger = null;

			_db?.Dispose();
			_db = null;
		}
		catch (Exception ex)
		{
			//MessageBox.Show(ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Stop);
		}
	}

	private async Task InternalInitialiseAsync(ConnectionString connectionString)
	{
		try
		{
			_db = await AsyncConnect(connectionString);

			// force open database
			var uv = _db.UserVersion;

			_connectionString = connectionString;

			LoadTreeView();
		}
		catch (Exception ex)
		{
			_db?.Dispose();
			_db = null;

			//MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

			return;
		}
	}

	private async Task<LiteDatabase> AsyncConnect(ConnectionString connectionString) =>
		await Task.Run(() => new LiteDatabase(connectionString));

	private void LoadTreeView()
	{
		TreeViewNodes.Clear();

		var rootNode = new TreeNodeViewModel(Path.GetFileName(_connectionString.Filename), TreeNodeType.Root)
		{
			Nodes = new List<TreeNodeViewModel>()
		};

		var systemNode =  new TreeNodeViewModel("System", TreeNodeType.Dictionary)
		{
			Nodes = new List<TreeNodeViewModel>()
		};

		rootNode.Nodes.Add(systemNode);

		var sc = _db.GetCollection("$cols")
			.Query()
			.Where("type = 'system'")
			.OrderBy("name")
			.ToDocuments();

		foreach (var doc in sc)
		{
			systemNode.Nodes.Add(new TreeNodeViewModel(doc["name"].AsString, TreeNodeType.SystemTable)
			{
				Query = $"SELECT $ FROM {doc["name"].AsString}"
			});
		}

		foreach (var key in _db.GetCollectionNames().OrderBy(x => x))
		{
			rootNode.Nodes.Add(new TreeNodeViewModel(key, TreeNodeType.Table)
			{
				Query = $"SELECT $ FROM {key};"
			});
		}

		TreeViewNodes.Add(rootNode);
	}

	private void InvokeRun()
	{
		if (ActiveTask?.Executing == false)
		{
			ActiveTask.Sql = ActiveTask.EditorContent;
			ActiveTask.WaitHandle.Set();
			RaisePropertyChanged(nameof(Loading));
		}
	}

	private void InvokeUpdatePosition((int, int) positions)
	{
		if (!_isActive)
			return;

		if (ActiveTask == null)
			return;

		ActiveTask.Position = positions;
		RaisePropertyChanged(nameof(TextPosition));
	}

	private void InvokeCodeSnipped(TreeNodeViewModel node)
	{
		if (string.IsNullOrWhiteSpace(node.Query))
			return;

		if (string.IsNullOrWhiteSpace(ActiveTask.EditorContent))
		{
			ActiveTask.EditorContent = node.Query;
			RaisePropertyChanged(nameof(ActiveTask));
		}
		else
		{
			try
			{
				IsActive = false;
				AddNewTask(node.Query);
			}
			finally
			{
				IsActive = true;
			}
		}
	}

	private void InvokeSelectedTaskChanged(TaskData value)
	{
		if (!_isActive) return;

		try
		{
			IsActive = false;
			if (value.Id < 1)
			{
				AddNewTask("");
				return;
			}

			ActiveTask.IsGridLoaded = ActiveTask.IsTextLoaded = ActiveTask.IsParametersLoaded = false;
			LoadResult(ActiveTask);
		}
		finally
		{
			IsActive = true;
		}

		RaisePropertyChanged(nameof(TextPosition));
	}


	private void AddNewTask(string content)
	{
		// find + tab
		//var tab = tabSql.TabPages.Cast<TabPage>().Where(x => x.Text == "+").Single();

		var task = new TaskData();

		task.EditorContent = content;
		task.Thread = new Thread(() => CreateThread(task));
		task.Thread.Start();

		task.Id = task.Thread.ManagedThreadId;

		Tasks.Insert(Tasks.Count == 0 ? 0 : Tasks.Count - 1, task);
		ActiveTask = task;
		//tab.Text = tab.Name = task.Id.ToString();
		// tab.Tag = task;
		//
		// if (tabSql.SelectedTab != tab)
		// {
		// 	tabSql.SelectTab(tab);
		// }
		//
		// // adding new + tab at end
		// tabSql.TabPages.Add("+", "+");
		//
		// tabResult.SelectTab("tabGrid");
	}

	private void CreateThread(TaskData task)
	{
		while (true)
		{
			task.WaitHandle.Wait();

			if (task.ThreadRunning == false) break;

			if (task.Sql.Trim() == "")
			{
				task.WaitHandle.Reset();
				continue;
			}

			var sw = new Stopwatch();
			sw.Start();

			try
			{
				task.Executing = true;
				task.IsGridLoaded = task.IsTextLoaded = task.IsParametersLoaded = false;

				_synchronizationContext?.Post(_ => { LoadResult(task); }, task);

				task.Parameters = new BsonDocument();

				var sql = new StringReader(task.Sql.Trim());

				while (sql.Peek() >= 0 && _db != null)
				{
					using (var reader = _db.Execute(sql, task.Parameters))
					{
						task.ReadResult(reader);
					}
				}

				task.Elapsed = sw.Elapsed;
				task.Exception = null;
				task.Executing = false;

				// update form button selected
				_synchronizationContext?.Post(o =>
				{
					var t = o as TaskData;

					if (ActiveTask?.Id == t.Id)
					{
						LoadResult(o as TaskData);
					}
				}, task);
			}
			catch (Exception ex)
			{
				task.Executing = false;
				task.Result = null;
				task.Elapsed = sw.Elapsed;
				task.Exception = ex;

				_synchronizationContext.Post(o =>
				{
					var t = o as TaskData;

					if (ActiveTask?.Id == t.Id)
					{
						LoadResult(o as TaskData);
					}
				}, task);
			}

			// put thread in wait mode
			task.WaitHandle.Reset();
		}

		task.WaitHandle.Dispose();
	}

	private void LoadResult(TaskData data)
	{
		_synchronizationContext?.Post(_ =>
				InverseResultCommand?.Execute(data),
			data);

		RaisePropertyChanged(nameof(Loading));
	}
}
