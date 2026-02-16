using System.Globalization;
using System.Windows;
using MessageBoxWpf = System.Windows.MessageBox;

namespace LayoutFixer;

public partial class MainWindow : Window
{
    private Settings _settings;

    public event EventHandler<Settings>? SettingsSaved;

    public MainWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;

        ReloadInstalledLanguages();
        ApplySettingsToUi();
    }

    public void ReloadInstalledLanguages()
    {
        // Ensure dropdowns reflect current installed languages
        var langs = InstalledLanguages.GetInstalledCultureNames();

        LangABox.ItemsSource = langs;
        LangBBox.ItemsSource = langs;

        // Keep selected if possible
        if (!string.IsNullOrWhiteSpace(_settings.LangA)) LangABox.SelectedItem = _settings.LangA;
        if (!string.IsNullOrWhiteSpace(_settings.LangB)) LangBBox.SelectedItem = _settings.LangB;
    }

    private void ApplySettingsToUi()
    {
        LangABox.SelectedItem = _settings.LangA ?? "he-IL";
        LangBBox.SelectedItem = _settings.LangB ?? "en-US";
        HotkeyBox.Text = _settings.Hotkey ?? "F9";
        StartupCheck.IsChecked = _settings.RunAtStartup;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var newSettings = new Settings
        {
            LangA = (LangABox.SelectedItem as string) ?? "he-IL",
            LangB = (LangBBox.SelectedItem as string) ?? "en-US",
            Hotkey = string.IsNullOrWhiteSpace(HotkeyBox.Text) ? "F9" : HotkeyBox.Text.Trim(),
            RunAtStartup = StartupCheck.IsChecked == true
        };

        _settings = newSettings;

        // Apply startup setting immediately
        StartupManager.SetRunAtStartup(newSettings.RunAtStartup);

        SettingsSaved?.Invoke(this, newSettings);
        MessageBoxWpf.Show("Saved.", "LayoutFixer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Keep app running in tray
        e.Cancel = true;
        Hide();
    }
}

public static class InstalledLanguages
{
    public static List<string> GetInstalledCultureNames()
    {
        var list = new List<string>();
        foreach (System.Windows.Forms.InputLanguage l in System.Windows.Forms.InputLanguage.InstalledInputLanguages)
        {
            var name = l.Culture?.Name;
            if (!string.IsNullOrWhiteSpace(name) && !list.Contains(name))
                list.Add(name);
        }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }
}
