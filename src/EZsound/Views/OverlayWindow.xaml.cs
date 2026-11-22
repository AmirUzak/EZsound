using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using EZsound.ViewModels;

namespace EZsound.Views;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly OverlayViewModel _viewModel;
    private bool _isClosingFromApp;
    private bool _isAnimating;

    public event EventHandler? PositionChanged;

    public OverlayWindow(OverlayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Topmost = _viewModel.IsAlwaysOnTop;
        _viewModel.AlwaysOnTopChanged += (_, isTop) => Topmost = isTop;
        _viewModel.RequestHide += (_, _) => SmoothHide();

        LocationChanged += (_, _) =>
        {
            if (IsLoaded && Visibility == Visibility.Visible && WindowState == WindowState.Normal)
            {
                PositionChanged?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public void SmoothShow()
    {
        if (_isAnimating) return;

        // Ensure window is in normal state and within bounds
        WindowState = WindowState.Normal;
        Topmost = _viewModel.IsAlwaysOnTop;
        Opacity = 0.0;
        Visibility = Visibility.Visible;
        Activate();

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetForegroundWindow(handle);
        }

        var anim = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(130),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        _isAnimating = true;
        anim.Completed += (_, _) =>
        {
            _isAnimating = false;
            Activate();
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
        };

        BeginAnimation(OpacityProperty, anim);
    }

    public void SmoothHide(Action? onHidden = null)
    {
        if (_isAnimating || Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Hidden;
            onHidden?.Invoke();
            return;
        }

        var anim = new DoubleAnimation
        {
            From = Opacity,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        _isAnimating = true;
        anim.Completed += (_, _) =>
        {
            _isAnimating = false;
            Visibility = Visibility.Hidden;
            onHidden?.Invoke();
        };

        BeginAnimation(OpacityProperty, anim);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OverlayWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            SmoothHide();
            e.Handled = true;
        }
    }

    public void ForceClose()
    {
        _isClosingFromApp = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosingFromApp)
        {
            // Cancel standard close and smooth hide instead
            e.Cancel = true;
            SmoothHide();
            return;
        }

        base.OnClosing(e);
    }
}
