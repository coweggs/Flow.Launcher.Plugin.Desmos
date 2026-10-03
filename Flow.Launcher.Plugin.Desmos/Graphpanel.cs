using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

#nullable enable

namespace Flow.Launcher.Plugin.Desmos;

// Hosts desmos.com/calculator (2D) or desmos.com/3d in ONE shared WebView2.
// Expressions are pushed into the page's window.Calc (undocumented but widely used by userscripts).
public sealed class GraphPanel : UserControl, IDisposable
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);
    private const string Url2D = "https://www.desmos.com/calculator";
    private const string Url3D = "https://www.desmos.com/3d";

    private WebView2 _web = new();
    private DispatcherTimer? _idleTimer;
    private bool _hasBeenVisible;
    private bool _initializing;
    private bool _coreReady, _pageReady;
    private bool? _loaded3d;
    private bool _want3d;
    private string? _pendingScript;
    private string? _lastScript;
    private bool _disposed;

    public GraphPanel()
    {
        Content = _web;
        IsVisibleChanged += OnIsVisibleChanged;
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        if (_disposed || _initializing) return;

        _initializing = true;
        try
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FlowLauncher", "GraphPreviewWebView2");

            var env = await CoreWebView2Environment.CreateAsync(null, dataDir);
            await _web.EnsureCoreWebView2Async(env);
            if (_disposed) return;

            _web.ZoomFactor = 0.5;
            var core = _web.CoreWebView2;

            // Lock down: no permission prompts, no devtools, no new windows, desmos.com only.
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.ScriptDialogOpening += (_, e) => e.Accept();
            core.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) ||
                    (u.Host != "www.desmos.com" && u.Host != "desmos.com"))
                    e.Cancel = true;
            };
            core.NavigationCompleted += async (_, e) =>
            {
                try
                {
                    _pageReady = e.IsSuccess;
                    if (_pageReady) await FlushAsync();
                }
                catch (Exception)
                {
                    _pageReady = false;
                }
            };

            _coreReady = true;
            NavigateIfNeeded();
        }
        catch (Exception)
        {
            if (_disposed) return;

            Content = new TextBlock
            {
                Text = "WebView2 runtime not found. Install the Microsoft Edge WebView2 Runtime.",
                Margin = new System.Windows.Thickness(12),
                TextWrapping = System.Windows.TextWrapping.Wrap
            };
        }
        finally
        {
            _initializing = false;
        }
    }

    public void Show(bool is3d, string expr, bool isDark = false)
    {
        if (_disposed) return;

        StopIdleTimer();
        _want3d = is3d;
        _lastScript = BuildScript(expr, isDark);
        _pendingScript = _lastScript;
        if (_coreReady) NavigateIfNeeded();
        else if (!_initializing) _ = InitAsync();
    }

    private void OnIsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _hasBeenVisible = true;
            StopIdleTimer();
            if (!_coreReady && !_initializing && !_disposed)
            {
                _pendingScript = _lastScript;
                _ = InitAsync();
            }
        }
        else if (_hasBeenVisible && !_disposed)
        {
            _idleTimer ??= new DispatcherTimer { Interval = IdleTimeout };
            _idleTimer.Tick -= IdleTimerOnTick;
            _idleTimer.Tick += IdleTimerOnTick;
            _idleTimer.Start();
        }
    }

    private void IdleTimerOnTick(object? sender, EventArgs e)
    {
        StopIdleTimer();
        if (!IsVisible) ReleaseWebView();
    }

    private void StopIdleTimer()
    {
        _idleTimer?.Stop();
    }

    private void ReleaseWebView()
    {
        if (_disposed || _initializing) return;

        _coreReady = false;
        _pageReady = false;
        _loaded3d = null;
        _pendingScript = _lastScript;
        _web.Dispose();
        _web = new WebView2();
        Content = _web;
    }

    private void NavigateIfNeeded()
    {
        if (_loaded3d != _want3d)
        {
            _loaded3d = _want3d;
            _pageReady = false;
            try
            {
                _web.CoreWebView2.Navigate(_want3d ? Url3D : Url2D);
            }
            catch (Exception)
            {
                _loaded3d = null;
            }
        }
        else if (_pageReady)
        {
            _ = FlushAsync();
        }
    }

    private async Task FlushAsync()
    {
        if (_disposed || _pendingScript is null || _web.CoreWebView2 is null) return;
        var script = _pendingScript;
        _pendingScript = null;
        try
        {
            await _web.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception)
        {
            // Navigation can dispose the document while a previous script is running.
            _pendingScript ??= script;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        StopIdleTimer();
        IsVisibleChanged -= OnIsVisibleChanged;
        _coreReady = false;
        _pageReady = false;
        _pendingScript = null;
        _lastScript = null;
        _web.Dispose();
    }

    // Wait for Desmos to expose its calculator object, then replace the previous expressions.
    private static string BuildScript(string expr, bool isDark)
    {
        var parts = expr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(ToLatex).ToArray();
         var json = JsonSerializer.Serialize(parts);
         var darkJson = JsonSerializer.Serialize(isDark);
         return "(function(q,dark){function collapse(){var C=window.Calc;" +
             "if(C&&typeof C.updateSettings==='function'){C.updateSettings({expressionsCollapsed:true});" +
             "window.__flCollapsed=true;return true;}" +
             "if(window.__flCollapsed)return true;" +
             "var b=document.querySelector('.dcg-collapse-button,.dcg-collapse-control,[aria-label*=" +
             "\\\"Collapse\\\" i]');" +
             "if(!b)return false;b.click();window.__flCollapsed=true;return true;}" +
               "function go(n){var C=window.Calc;" +
               "if(!C){if(n>0)setTimeout(function(){go(n-1)},200);return;}" +
               "(window.__fl||[]).forEach(function(id){C.removeExpression({id:id});});window.__fl=[];" +
               "if(C&&typeof C.updateSettings==='function')C.updateSettings({invertedColors:dark});" +
                             "q.forEach(function(l,i){var id='fl'+i;C.setExpression({id:id,latex:l});window.__fl.push(id);});" +
             "if(!collapse()&&n>0)setTimeout(function(){collapse()},200);}" +
               "go(75);})(" + json + "," + darkJson + ");";
    }

    private static readonly Regex Funcs = new(
        @"(?<![\\A-Za-z])(arcsin|arccos|arctan|sinh|cosh|tanh|sin|cos|tan|sec|csc|cot|ln|log)(?=\s*\()",
        RegexOptions.Compiled);
    private static readonly Regex Pi = new(@"(?<![\\A-Za-z])pi(?![A-Za-z])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BigExp = new(@"\^(\d{2,})", RegexOptions.Compiled);
    private static readonly Regex ParenExp = new(@"\^\(([^()]*)\)", RegexOptions.Compiled);

    // Convert common plain-text forms while leaving raw LaTeX usable.
    private static string ToLatex(string s)
    {
        s = Regex.Replace(s, @"\bsqrt\(([^()]*)\)", @"\sqrt{$1}", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\babs\(([^()]*)\)", @"\left|$1\right|", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\binf(?:inity)?\b", @"\infty", RegexOptions.IgnoreCase);
        s = Funcs.Replace(s, @"\$1");
        s = Pi.Replace(s, @"\pi");
        s = BigExp.Replace(s, "^{$1}");
        s = ParenExp.Replace(s, "^{$1}");
        return s.Replace("*", @"\cdot ");
    }
}