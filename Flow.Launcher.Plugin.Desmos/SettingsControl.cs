using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Flow.Launcher.Plugin.Desmos;

public sealed class SettingsControl : UserControl
{
    public SettingsControl(DesmosSettings settings, Action save)
    {
        var syncTheme = new CheckBox
        {
            Content = "Synchronize graph theme with Flow Launcher",
            IsChecked = settings.SyncTheme,
            Margin = new Thickness(8)
        };

        syncTheme.Checked += (_, _) =>
        {
            settings.SyncTheme = true;
            save();
        };
        syncTheme.Unchecked += (_, _) =>
        {
            settings.SyncTheme = false;
            save();
        };

        var historyLimit = new TextBox
        {
            Text = settings.HistoryLimit.ToString(CultureInfo.InvariantCulture),
            Width = 64,
            Margin = new Thickness(8)
        };
        historyLimit.LostFocus += (_, _) =>
        {
            if (!int.TryParse(historyLimit.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                value = settings.HistoryLimit;

            settings.HistoryLimit = Math.Clamp(value, 1, 100);
            historyLimit.Text = settings.HistoryLimit.ToString(CultureInfo.InvariantCulture);
            save();
        };

        Content = new StackPanel
        {
            Children =
            {
                syncTheme,
                new TextBlock { Text = "History entries (1-100)", Margin = new Thickness(8, 0, 8, 0) },
                historyLimit
            }
        };
    }
}