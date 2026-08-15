using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using IPCE.Desktop.Services;

namespace IPCE.Desktop.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    private readonly SynchronizationContext? _uiContext;

    protected ViewModelBase(
        SynchronizationContext? synchronizationContext = null)
    {
        _uiContext =
            synchronizationContext ?? SynchronizationContext.Current;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));

    protected Task RunOnUiContextAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_uiContext is null ||
            ReferenceEquals(SynchronizationContext.Current, _uiContext))
        {
            action();
            return Task.CompletedTask;
        }

        var completion =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(
            _ =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
            },
            null);
        return completion.Task;
    }
}

public interface IAsyncCommand : ICommand
{
    Task ExecuteAsync(object? parameter);
}

public sealed class RelayCommand(
    Action<object?> execute,
    Predicate<object?>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => execute(parameter);

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncRelayCommand(
    Func<object?, Task> execute,
    Predicate<object?>? canExecute = null) : IAsyncCommand
{
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isExecuting && (canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter) =>
        await ExecuteAsync(parameter);

    public async Task ExecuteAsync(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await execute(parameter);
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class SafeRelayCommand : ICommand
{
    private readonly IUserOperationRunner _operations;
    private readonly Func<string> _title;
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public SafeRelayCommand(
        IUserOperationRunner operations,
        string title,
        Action<object?> execute,
        Predicate<object?>? canExecute = null)
        : this(operations, () => title, execute, canExecute)
    {
    }

    public SafeRelayCommand(
        IUserOperationRunner operations,
        Func<string> title,
        Action<object?> execute,
        Predicate<object?>? canExecute = null)
    {
        _operations = operations ??
            throw new ArgumentNullException(nameof(operations));
        _title = title ??
            throw new ArgumentNullException(nameof(title));
        _execute = execute ??
            throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) =>
        _operations.Run(
            _title(),
            () => _execute(parameter));

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class SafeAsyncRelayCommand : IAsyncCommand
{
    private readonly IUserOperationRunner _operations;
    private readonly Func<string> _title;
    private readonly Func<object?, Task> _execute;
    private readonly Predicate<object?>? _canExecute;
    private bool _isExecuting;

    public SafeAsyncRelayCommand(
        IUserOperationRunner operations,
        string title,
        Func<object?, Task> execute,
        Predicate<object?>? canExecute = null)
        : this(operations, () => title, execute, canExecute)
    {
    }

    public SafeAsyncRelayCommand(
        IUserOperationRunner operations,
        Func<string> title,
        Func<object?, Task> execute,
        Predicate<object?>? canExecute = null)
    {
        _operations = operations ??
            throw new ArgumentNullException(nameof(operations));
        _title = title ??
            throw new ArgumentNullException(nameof(title));
        _execute = execute ??
            throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isExecuting &&
        (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter) =>
        await ExecuteAsync(parameter);

    public async Task ExecuteAsync(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _operations.RunAsync(
                _title(),
                () => _execute(parameter));
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
