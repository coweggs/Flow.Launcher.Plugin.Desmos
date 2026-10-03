using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

#nullable enable

namespace Flow.Launcher.Plugin.Desmos;

// Query:  des y=x^2 ; y=2x+1        -> 2D
//         des 3d z=sin(x)*cos(y)    -> 3D
public sealed class Main : IAsyncPlugin, ISettingProvider, IDisposable
{
    private const int HistoryDebounceMilliseconds = 800;
    private PluginInitContext _ctx = null!;
    private DesmosSettings _settings = new();
    private GraphPanel? _panel; // ONE shared WebView2, reused by every result
    private readonly object _historyLock = new();
    private readonly Timer _historyTimer;
    private string? _pendingHistoryExpression;

    public Main()
    {
        _historyTimer = new Timer(_ => CommitPendingHistory(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public Task InitAsync(PluginInitContext context)
    {
        _ctx = context;
        _settings = context.API.LoadSettingJsonStorage<DesmosSettings>();
        lock (_historyLock)
        {
            TrimHistoryToLimit();
        }
        return Task.CompletedTask;
    }

    public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
    {
        var search = query.Search.Trim();
        var is3d = false;

        // optional leading "3d" switches the mode
        if (search.StartsWith("3d", StringComparison.OrdinalIgnoreCase) &&
            (search.Length == 2 || char.IsWhiteSpace(search[2])))
        {
            is3d = true;
            search = search[2..].Trim();
        }

        if (IsClearHistoryQuery(search))
        {
            return Task.FromResult(new List<Result> { CreateClearHistoryResult() });
        }

        if (search.Length == 0)
        {
            var results = new List<Result>
            {
                new()
                {
                    Title = "Type an expression",
                    SubTitle = "des <expr>, des 3d <expr>",
                    IcoPath = "desmos.png"
                }
            };

            List<string> history;
            lock (_historyLock)
            {
                TrimHistoryToLimit();
                history = new List<string>(_settings.History);
            }

            results.AddRange(history.ConvertAll(stored =>
            {
                var (expression, is3d) = DecodeHistory(stored);
                return CreateGraphResult(expression, is3d, true);
            }));

            if (history.Count > 0)
                results.Add(CreateClearHistoryResult());

            return Task.FromResult(results);
        }

        if (LooksGraphable(search))
            ScheduleHistory(EncodeHistory(search, is3d));
        return Task.FromResult(new List<Result>
        {
            CreateGraphResult(search, is3d, false)
        });
    }

    private Result CreateGraphResult(string expr, bool is3d, bool fromHistory)
    {
        var result = new Result
        {
            Title = expr,
            SubTitle = fromHistory
                ? "Recent graph. Enter to copy"
                : is3d ? "3D graph. F1 shows the preview" : "2D graph. F1 shows the preview",
            IcoPath = "desmos.png",
            PreviewPanel = new Lazy<UserControl>(() =>
            {
                _panel ??= new GraphPanel();
                _panel.Show(is3d, expr, _settings.SyncTheme && IsApplicationDarkTheme());
                return _panel;
            }),
            Action = _ =>
            {
                AddToHistory(expr, is3d);
                _ctx.API.CopyToClipboard(expr);
                return true;
            }
        };

        SetPreviewVisibilityAlways(result);

        return result;
    }

    private void AddToHistory(string expression, bool is3d = false)
    {
        if (!LooksGraphable(expression)) return;

        var storedExpression = EncodeHistory(expression, is3d);

        lock (_historyLock)
        {
            _settings.History.RemoveAll(value => string.Equals(value, storedExpression, StringComparison.OrdinalIgnoreCase));
            _settings.History.Insert(0, storedExpression);
            TrimHistoryToLimit();
        }
    }

    private void ScheduleHistory(string expression)
    {
        lock (_historyLock)
        {
            _pendingHistoryExpression = expression;
            _historyTimer.Change(HistoryDebounceMilliseconds, Timeout.Infinite);
        }
    }

    private static bool LooksGraphable(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return false;

        return expression.Contains('=') ||
               Regex.IsMatch(expression, @"^\s*\([^,]+,[^)]+\)\s*$") ||
               Regex.IsMatch(expression, @"\([^,]+,[^)]+\)");
    }

    private static bool IsClearHistoryQuery(string search)
        => string.Equals(search, "clear", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(search, "clear history", StringComparison.OrdinalIgnoreCase);

    private static string EncodeHistory(string expression, bool is3d)
        => is3d ? $"3d {expression}" : expression;

    private static (string Expression, bool Is3d) DecodeHistory(string stored)
    {
        if (stored.StartsWith("3d ", StringComparison.OrdinalIgnoreCase))
            return (stored[3..], true);

        return (stored, false);
    }

    private void CommitPendingHistory()
    {
        lock (_historyLock)
        {
            var expression = _pendingHistoryExpression;
            _pendingHistoryExpression = null;
            if (!string.IsNullOrWhiteSpace(expression))
            {
                var (decodedExpression, is3d) = DecodeHistory(expression);
                AddToHistory(decodedExpression, is3d);
            }
        }
    }

    private bool IsApplicationDarkTheme()
    {
        var method = _ctx.API.GetType().GetMethod("IsApplicationDarkTheme", BindingFlags.Public | BindingFlags.Instance);
        return method?.Invoke(_ctx.API, null) as bool? ?? false;
    }

    private Result CreateClearHistoryResult()
    {
        return new Result
        {
            Title = "Clear Desmos history",
            SubTitle = "Remove all recent expressions",
            IcoPath = "desmos.png",
            Action = _ =>
            {
                lock (_historyLock)
                {
                    _settings.History.Clear();
                    _pendingHistoryExpression = null;
                    _historyTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }

                SaveSettings();
                return true;
            }
        };
    }

    private static void SetPreviewVisibilityAlways(Result result)
    {
        var property = result.GetType().GetProperty("PreviewVisibility");
        if (property is null || !property.CanWrite || !property.PropertyType.IsEnum) return;

        try
        {
            var always = Enum.Parse(property.PropertyType, "Always", ignoreCase: true);
            property.SetValue(result, always);
        }
        catch (ArgumentException)
        {
            // The host exposes an incompatible preview visibility enum.
        }
    }

    public Control CreateSettingPanel() => new SettingsControl(_settings, SaveSettings);

    private void SaveSettings()
    {
        lock (_historyLock)
        {
            TrimHistoryToLimit();
        }

        var method = _ctx.API.GetType().GetMethod("SaveSettingJsonStorage", BindingFlags.Public | BindingFlags.Instance);
        method?.MakeGenericMethod(typeof(DesmosSettings)).Invoke(_ctx.API, null);
    }

    private void TrimHistoryToLimit()
    {
        var historyLimit = Math.Clamp(_settings.HistoryLimit, 1, 100);
        _settings.HistoryLimit = historyLimit;
        if (_settings.History.Count > historyLimit)
            _settings.History.RemoveRange(historyLimit, _settings.History.Count - historyLimit);
    }

    public void Dispose()
    {
        CommitPendingHistory();
        _historyTimer.Dispose();
        SaveSettings();
        _panel?.Dispose();
        _panel = null;
    }
}

public sealed class DesmosSettings
{
    public bool SyncTheme { get; set; } = true;
    public int HistoryLimit { get; set; } = 20;
    public List<string> History { get; set; } = new();
}