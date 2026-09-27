using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace VSerial.UI.Common;

/// <summary>
/// 通用异步命令实现，内建防重入保护
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _executeAsync;
    private readonly Predicate<object?>? _canExecute;
    private readonly Action<Exception>? _onException;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool IsExecuting => _isExecuting;

    public AsyncRelayCommand(
        Func<object?, Task> executeAsync,
        Predicate<object?>? canExecute = null,
        Action<Exception>? onException = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _onException = onException;
    }

    public AsyncRelayCommand(
        Func<Task> executeAsync,
        Func<bool>? canExecute = null,
        Action<Exception>? onException = null)
        : this(_ => executeAsync(), canExecute != null ? _ => canExecute() : null, onException)
    {
    }

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting)
        {
            return false;
        }

        return _canExecute == null || _canExecute(parameter);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _executeAsync(parameter);
        }
        catch (Exception ex)
        {
            if (_onException != null)
            {
                _onException(ex);
            }
            else
            {
                // 默认向上抛出
                throw;
            }
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
