using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace Melodium.Services;

public static class MainThread
{
    private static DispatcherQueue? _dispatcherQueue;

    public static void Initialize(DispatcherQueue queue)
    {
        _dispatcherQueue = queue;
    }

    public static bool IsMainThread => _dispatcherQueue == null || _dispatcherQueue.HasThreadAccess;

    public static void BeginInvokeOnMainThread(Action action)
    {
        if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                try { action(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainThread] Exception: {ex}");
                }
            });
        }
        else
        {
            try { action(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainThread] Exception: {ex}");
            }
        }
    }

    public static Task InvokeOnMainThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource();
        BeginInvokeOnMainThread(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
