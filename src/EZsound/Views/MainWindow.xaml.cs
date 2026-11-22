using System.Windows;
using System.Windows.Input;
using EZsound.Models;
using EZsound.ViewModels;

namespace EZsound.Views;

public partial class MainWindow : Window
{
    private readonly AppSettings? _settings;
    private bool _isClosingFromApp;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel, AppSettings settings) : this()
    {
        DataContext = viewModel;
        _settings = settings;

        Topmost = _settings.AlwaysOnTop;
        UpdatePinButtonGlyph();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings != null)
        {
            _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
            _settings.Save();
            Topmost = _settings.AlwaysOnTop;
            UpdatePinButtonGlyph();
        }
    }

    private void UpdatePinButtonGlyph()
    {
        if (PinButton != null && _settings != null)
        {
            PinButton.Content = _settings.AlwaysOnTop ? "\uE840" : "\uE718";
            PinButton.ToolTip = _settings.AlwaysOnTop ? "Always on Top: Enabled" : "Always on Top: Disabled";
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings?.HideToTrayOnClose == true)
        {
            Hide();
        }
        else
        {
            ForceClose();
        }
    }

    private void TrayIndicator_Click(object sender, MouseButtonEventArgs e)
    {
        Hide();
    }

    public void ForceClose()
    {
        _isClosingFromApp = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosingFromApp && _settings?.HideToTrayOnClose == true)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
