using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LiteDB.Studio.ViewModels;

public sealed class Command<T> : Command
{
	private readonly Action<T> _cb;
	private static object _lock = new object();
	private static bool _busy;

	public Func<bool> IsBusy { get; set; }

	private static event EventHandler _canExecuteChanged;

	public Command(Action<T> cb)
	{
		_cb = cb;
	}

	private bool Busy
	{
		get => IsBusy?.Invoke() ?? _busy;
		set
		{
			_busy = value;
			_canExecuteChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public override event EventHandler CanExecuteChanged
	{
		add => _canExecuteChanged += value;
		remove => _canExecuteChanged -= value;
	}

	public override bool CanExecute(object parameter) => !_busy;

	public override void Execute(object parameter)
	{
		lock (_lock)
		{
			if (Busy)
				return;
			try
			{
				Busy = true;
				_cb((T)parameter);
			}
			finally
			{
				Busy = false;
			}
		}
	}
}

public abstract class Command : ICommand
{
	public static Command Create(Action cb) => new Command<object>(_ => cb());
	public static Command Create(Action cb, Func<bool> isBusy) => new Command<object>(_ => cb()) { IsBusy = isBusy };
	public static Command Create<TArg>(Action<TArg> cb) => new Command<TArg>(cb);
	public static Command Create<TArg>(Action<TArg> cb, Func<bool> isBusy) => new Command<TArg>(cb) { IsBusy = isBusy };

	public abstract bool CanExecute(object parameter);
	public abstract void Execute(object parameter);
	public abstract event EventHandler CanExecuteChanged;
}
