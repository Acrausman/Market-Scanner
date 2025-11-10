using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MarketScanner.Core.Abstractions;

namespace MarketScanner.Data.Services.Alerts;

public class AlertManager : IAlertManager
{
    private readonly IAppLogger _logger;
    private readonly ConcurrentQueue<ScannerAlert> _pendingAlerts = new();
    private readonly SynchronizationContext? _synchronizationContext;
    private IAlertSink? _alertSink;

    public AlertManager(IAppLogger logger, SynchronizationContext? synchronizationContext = null)
    {
        _logger = logger;
        _synchronizationContext = synchronizationContext ?? SynchronizationContext.Current;
        OverboughtSymbols = new ObservableCollection<string>();
        OversoldSymbols = new ObservableCollection<string>();
    }

    public ObservableCollection<string> OverboughtSymbols { get; }
    public ObservableCollection<string> OversoldSymbols { get; }

    public int OverboughtCount => OverboughtSymbols.Count;
    public int OversoldCount => OversoldSymbols.Count;

    public void SetSink(IAlertSink? sink)
    {
        _alertSink = sink;
    }

    public void Enqueue(string symbol, string triggerName, double value)
    {
        var alert = new ScannerAlert(symbol, triggerName, value);
        _pendingAlerts.Enqueue(alert);

        var formatted = $"{symbol} {triggerName} ({value:F2})";
        //_logger.Log(LogSeverity.Information, $"[AlertManager] Queued alert: {formatted}");
        //_logger.Log(LogSeverity.Information, $"[AlertManager] Total alerts are now {_pendingAlerts.Count}");

        _alertSink?.AddAlert(formatted);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_pendingAlerts.IsEmpty)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var drained = new List<ScannerAlert>();
        while (_pendingAlerts.TryDequeue(out var alert))
        {
            drained.Add(alert);
        }

        if (drained.Count == 0)
        {
            return;
        }

        await InvokeOnContextAsync(() =>
        {
            foreach (var alert in drained)
            {
                if (IsOverbought(alert.TriggerName))
                {
                    if (!OverboughtSymbols.Contains(alert.Symbol))
                    {
                        OverboughtSymbols.Add(alert.Symbol);
                    }
                }
                else if (IsOversold(alert.TriggerName))
                {
                    if (!OversoldSymbols.Contains(alert.Symbol))
                    {
                        OversoldSymbols.Add(alert.Symbol);
                    }
                }
            }
        }).ConfigureAwait(false);
    }

    public async Task ResetAsync()
    {
        while (_pendingAlerts.TryDequeue(out _))
        {
            // drain any remaining alerts
        }

        await InvokeOnContextAsync(() =>
        {
            OverboughtSymbols.Clear();
            OversoldSymbols.Clear();
        }).ConfigureAwait(false);
    }

    private static bool IsOverbought(string triggerName)
    {
        return triggerName.IndexOf("overbought", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsOversold(string triggerName)
    {
        return triggerName.IndexOf("oversold", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Task InvokeOnContextAsync(Action action)
    {
        if (_synchronizationContext is null)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource<object?>();
        _synchronizationContext.Post(_ =>
        {
            try
            {
                action();
                completion.SetResult(null);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }, null);

        return completion.Task;
    }

    private readonly record struct ScannerAlert(string Symbol, string TriggerName, double Value);
}
