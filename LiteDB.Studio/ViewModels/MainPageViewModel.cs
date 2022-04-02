using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LiteDB.Studio.ViewServices;

namespace LiteDB.Studio.ViewModels;

public class MainPageViewModel : ViewModelBase
{
	private readonly SynchronizationContext? _synchronizationContext;

	private ConnectionString _connectionString = null;
	private LiteDatabase _db;
	private DatabaseDebugger _debugger = null;
	private TaskData _activeTask;
	private bool _isActive;
	private bool _connected;

	public ICommand InverseResultCommand { get; set; }
	public ICommand InvertShowOpenDialogCommand { get; set; }

	public ICommand TabCloseCommand { get; }
	public ICommand TabCloseAllCommand { get; }
	public ICommand TabCloseOthersCommand { get; }
	public ICommand TabCloseToRightCommand { get; }
	public ICommand TabCloseToLeftCommand { get; }

	public ICommand OpenCommand { get; }
	public ICommand DisconnectCommand { get; }
	public ICommand RefreshCommand { get; }
	public ICommand RunCommand { get; }
	public ICommand BeginCommand { get; }
	public ICommand CommitCommand { get; }
	public ICommand RollbackCommand { get; }
	public ICommand CheckpointCommand { get; }
	public ICommand UpdateTextPositionCommand { get; }
	public ICommand CodeSnippedCommand { get; }

	public ICommand CellUpdateCommand { get; }
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

	public bool Connected
	{
		get => _connected;
		private set => Set(ref _connected, value);
	}

	public bool Loading => ActiveTask?.Executing ?? false;

	public string TextPosition => $"Line: {ActiveTask?.Position.Item1} - Column: {ActiveTask?.Position.Item2}";
	public string DocumentsStatus => ActiveTask?.Result?.Count > 0
		? $"{ActiveTask.Result.Count} document{(ActiveTask.Result.Count > 1 ? "s" : "")}"
		: "no document";
	public string ElapsedStatus => ActiveTask?.Executing ?? false
		? "Executing"
		: ActiveTask?.Elapsed.ToString();

	public MainPageViewModel()
	{
		_synchronizationContext = SynchronizationContext.Current;

		TabCloseCommand = Command.Create<TaskData>(InvokeTabClose);
		TabCloseAllCommand = Command.Create<TaskData>(InvokeTabCloseAll);
		TabCloseOthersCommand = Command.Create<TaskData>(InvokeTabCloseOthers);
		TabCloseToRightCommand = Command.Create<TaskData>(InvokeTabCloseToRight);
		TabCloseToLeftCommand = Command.Create<TaskData>(InvokeTabCloseToLeft);

		OpenCommand = Command.Create(InvokeOpen);
		DisconnectCommand = Command.Create(Disconnect);
		RefreshCommand = Command.Create(CompareTablesWithTree);
		RunCommand = Command.Create(InvokeRun);
		BeginCommand = Command.Create(InvokeBegin);
		CommitCommand = Command.Create(InvokeCommit);
		RollbackCommand = Command.Create(InvokeRollback);
		CheckpointCommand = Command.Create(InvokeCheckpoint);
		UpdateTextPositionCommand = Command.Create<(int, int)>(InvokeUpdatePosition);
		CodeSnippedCommand = Command.Create<TreeNodeViewModel>(InvokeCodeSnipped);
		CellUpdateCommand = Command.Create<(BsonValue, string)>(InvokeCellUpdate);

		BroadcastService.Instance.Register<ConnectionString>(ConnectionStringChanged);
	}

	#region Initialise and Connection

	private void InvokeOpen()
	{
		InvertShowOpenDialogCommand.Execute(null);
	}

	private void ConnectionStringChanged(ConnectionString connectionString)
	{
		if (Connected)
			Disconnect();

		_ = InitialiseAsync(connectionString.Filename);
	}

	public Task CheckLastRecentConnectionAsync()
	{
		return AppSettingsManager.ApplicationSettings.LastConnectionStrings != null
			? InitialiseAsync(AppSettingsManager.ApplicationSettings.LastConnectionStrings.Filename)
			: Task.CompletedTask;
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
			if (task.Id > 0)
			{
				task.ThreadRunning = false;
				task.WaitHandle.Set();
			}
		}

		Connected = false;
		Tasks.Clear();

		try
		{
			_debugger?.Dispose();
			_debugger = null;

			_db?.Dispose();
			_db = null;

			LoadTreeView();
		}
		catch (Exception ex)
		{
			_ = DialogService.Instance.ShowErrorAsync(ex.Message);
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
			Connected = true;
			LoadTreeView();
		}
		catch (Exception ex)
		{
			_db?.Dispose();
			_db = null;

			_ = DialogService.Instance.ShowErrorAsync(ex.Message);

			return;
		}
	}

	private async Task<LiteDatabase> AsyncConnect(ConnectionString connectionString) =>
		await Task.Run(() => new LiteDatabase(connectionString));

	#endregion

	#region Tree and Complation

	private void LoadTreeView()
	{
		TreeViewNodes.Clear();

		if (!Connected)
			return;

		var rootNode = new TreeNodeViewModel(Path.GetFileName(_connectionString.Filename), TreeNodeType.Root)
		{
			Nodes = new ObservableCollection<TreeNodeViewModel>()
		};

		var systemNode =  new TreeNodeViewModel("System", TreeNodeType.Dictionary)
		{
			Nodes = new ObservableCollection<TreeNodeViewModel>()
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

	private void CompareTablesWithTree()
	{
		var rootNode = TreeViewNodes.FirstOrDefault();
		if (rootNode == null)
			return;

		var tables = _db.GetCollectionNames().ToList();
		var notExists = tables.Except(rootNode.Nodes.Select(s => s.Name));
		var toRemove = rootNode.Nodes
			.Skip(1)
			.ExceptBy(tables, s => s.Name);

		foreach (var key in notExists)
		{
			rootNode.Nodes.Add(new TreeNodeViewModel(key, TreeNodeType.Table)
			{
				Query = $"SELECT $ FROM {key};"
			});
		}

		foreach (var node in toRemove)
			rootNode.Nodes.Remove(node);
	}

	private void InvokeCodeSnipped(TreeNodeViewModel node)
	{
		if (string.IsNullOrWhiteSpace(node.Query))
			return;

		if (!Connected || ActiveTask == null)
			return;

		if (string.IsNullOrWhiteSpace(ActiveTask.EditorContent))
		{
			ActiveTask.EditorContent = node.Query;
			InvokeStatusUpdate();
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
				InvokeStatusUpdate();
			}
		}
	}

	#endregion

	#region Run and transaction

	private void ExecuteSql(string sql)
	{
		if (!Connected) return;
		if (ActiveTask?.Executing != false) return;

		ActiveTask.Sql = sql;
		ActiveTask.WaitHandle.Set();
		RaisePropertyChanged(nameof(Loading));
	}

	private void InvokeRun() => ExecuteSql(ActiveTask.EditorContent);
	private void InvokeBegin() => ExecuteSql("BEGIN");
	private void InvokeCommit() => ExecuteSql("COMMIT");
	private void InvokeRollback() => ExecuteSql("ROLLBACK");
	private void InvokeCheckpoint() => ExecuteSql("CHECKPOINT");

	private void InvokeCellUpdate((BsonValue bson, string key) parameter)
	{
		try
		{
			var r = _db.Execute($"UPDATE {this.ActiveTask.Collection} SET {parameter.key} = @0 WHERE _id = @1",
				new BsonDocument
				{
					["0"] = parameter.bson[parameter.key],
					["1"] = parameter.bson["_id"]
				});

			if (r.Current == 1) return;

			throw new Exception("Current document was not found. Try run your query again");
		}
		catch (Exception ex)
		{
			_ = DialogService.Instance.ShowErrorAsync(ex.Message);
		}
	}

	#endregion

	#region Status

	private void InvokeStatusUpdate()
	{
		RaisePropertyChanged(nameof(TextPosition));
		RaisePropertyChanged(nameof(DocumentsStatus));
		RaisePropertyChanged(nameof(ElapsedStatus));
		RaisePropertyChanged(nameof(Loading));
		RaisePropertyChanged(nameof(IsActive));
		RaisePropertyChanged(nameof(ActiveTask));
	}

	private void InvokeUpdatePosition((int, int) positions)
	{
		if (!_isActive || ActiveTask == null || !Connected)
			return;

		ActiveTask.Position = positions;
		RaisePropertyChanged(nameof(TextPosition));
	}

	#endregion

	#region Tab and task manager

	private void InvokeSelectedTaskChanged(TaskData value)
	{
		if (!_isActive) return;

		try
		{
			IsActive = false;

			if (value == null)
				return;

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
			InvokeStatusUpdate();
		}
	}

	private void AddNewTask(string content)
	{
		var task = new TaskData();

		task.EditorContent = content;
		task.Thread = new Thread(() => CreateThread(task));
		task.Thread.Start();

		task.Id = task.Thread.ManagedThreadId;

		Tasks.Insert(Tasks.Count == 0 ? 0 : Tasks.Count - 1, task);
		ActiveTask = task;
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
		{
			InverseResultCommand?.Execute(data);

			InvokeStatusUpdate();

			if (!data.Executing)
			{
				CompareTablesWithTree();
			}
		}, data);
	}


	private void InvokeTabClose(TaskData task)
	{
		task.ThreadRunning = false;
		task.WaitHandle.Set();

		try
		{
			IsActive = false;
			Tasks.Remove(task);
		}
		finally
		{
			IsActive = true;
		}

		if (Tasks.Count == 1)
			AddNewTask("");
	}

	private void InvokeTabCloseAll(TaskData task)
	{
		try
		{
			IsActive = false;
			foreach (var item in Tasks.ToList())
			{
				if (item.Id > 0)
				{
					item.ThreadRunning = false;
					item.WaitHandle.Set();
					Tasks.Remove(item);
				}
			}
		}
		finally
		{
			IsActive = true;
		}

		AddNewTask("");
	}

	private void InvokeTabCloseOthers(TaskData task)
	{
		try
		{
			IsActive = false;
			foreach (var item in Tasks.ToList())
			{
				if (item == task) continue;
				if (item.Id > 0)
				{
					item.ThreadRunning = false;
					item.WaitHandle.Set();
					Tasks.Remove(item);
				}
			}
		}
		finally
		{
			IsActive = true;
		}

		if (Tasks.Count == 1)
			AddNewTask("");
	}

	private void InvokeTabCloseToRight(TaskData task)
	{
		try
		{
			IsActive = false;
			foreach (var item in Tasks.Skip(Tasks.IndexOf(task) + 1).ToList())
			{
				if (item.Id > 0)
				{
					item.ThreadRunning = false;
					item.WaitHandle.Set();
					Tasks.Remove(item);
				}
			}
		}
		finally
		{
			IsActive = true;
		}

		if (Tasks.Count == 1)
			AddNewTask("");
	}

	private void InvokeTabCloseToLeft(TaskData task)
	{
		try
		{
			IsActive = false;
			foreach (var item in Tasks.Take(Tasks.IndexOf(task)).ToList())
			{
				if (item.Id > 0)
				{
					item.ThreadRunning = false;
					item.WaitHandle.Set();
					Tasks.Remove(item);
				}
			}
		}
		finally
		{
			IsActive = true;
		}

		if (Tasks.Count == 1)
			AddNewTask("");
	}

	#endregion
}
