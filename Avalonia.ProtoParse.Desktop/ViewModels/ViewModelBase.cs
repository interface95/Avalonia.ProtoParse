using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.ProtoParse.Desktop.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avalonia.ProtoParse.Desktop.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    private readonly Lock _sync = new();
    protected static IClipboard Clipboard { get; private set; } = null!;
    protected static IStorageProvider Provider { get; private set; } = null!;

    public static void SetTopLevel(TopLevel topLevel)
    {
        Clipboard = topLevel.Clipboard!;
        Provider = topLevel!.StorageProvider;
    }

    /// <summary>
    /// Runs a command if the updating flag is not set.
    /// If the flag is true (indicating the function is already running) then the action is not run.
    /// If the flag is false (indicating no running function) then the action is run.
    /// Once the action is finished if it was run, then the flag is reset to false
    /// </summary>
    /// <param name="updatingFlag">The boolean property flag defining if the command is already running</param>
    /// <param name="action">The action to run if the command is not already running</param>
    /// <param name="onError">The action to run if the command is not already running</param>
    /// <returns></returns>
    protected async System.Threading.Tasks.Task RunCommandAsync(Expression<Func<bool>> updatingFlag, Func<System.Threading.Tasks.Task> action,
        Func<Exception, System.Threading.Tasks.Task>? onError = null)
    {
        // Lock to ensure single access to check
        _sync.Enter();

        try
        {
            // Check if the flag property is true (meaning the function is already running)
            if (updatingFlag.GetPropertyValue())
                return;

            // Set the property flag to true to indicate we are running
            updatingFlag.SetPropertyValue(true);
        }
        finally
        {
            _sync.Exit();
        }

        try
        {
            // Run the passed in action
            await action();
        }
        catch (Exception e)
        {
            if (onError != null)
                await onError(e);
        }
        finally
        {
            // Set the property flag back to false now it's finished
            updatingFlag.SetPropertyValue(false);
        }
    }

    /// <summary>
    ///     /// Runs a command if the updating flag is not set.
    /// If the flag is true (indicating the function is already running) then the action is not run.
    /// If the flag is false (indicating no running function) then the action is run.
    /// Once the action is finished if it was run, then the flag is reset to false
    /// </summary>
    /// <param name="updatingFlag"></param>
    /// <param name="action"></param>
    /// <param name="defaultValue"></param>
    /// <param name="onError"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected async Task<T?> RunCommandAsync<T>(Expression<Func<bool>> updatingFlag,
        Func<Task<T>> action,
        T? defaultValue = default,
        Func<Exception, System.Threading.Tasks.Task>? onError = null)
    {
        // Lock to ensure single access to check
        _sync.Enter();

        try
        {
            // Check if the flag property is true (meaning the function is already running)
            if (updatingFlag.GetPropertyValue())
                return defaultValue;

            // Set the property flag to true to indicate we are running
            updatingFlag.SetPropertyValue(true);
        }
        finally
        {
            _sync.Exit();
        }

        try
        {
            // Run the passed in action
            return await action();
        }
        catch (Exception e)
        {
            if (onError != null)
                await onError(e);
            return defaultValue;
        }
        finally
        {
            // Set the property flag back to false now it's finished
            updatingFlag.SetPropertyValue(false);
        }
    }
}