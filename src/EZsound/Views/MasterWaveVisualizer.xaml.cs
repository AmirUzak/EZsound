using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EZsound.Services;

using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using UserControl = System.Windows.Controls.UserControl;

namespace EZsound.Views;

public partial class MasterWaveVisualizer : UserControl
{
    public static readonly DependencyProperty CaptureServiceProperty =
        DependencyProperty.Register(
            nameof(CaptureService),
            typeof(IAudioCaptureService),
            typeof(MasterWaveVisualizer),
            new PropertyMetadata(null, OnCaptureServiceChanged));

    public IAudioCaptureService? CaptureService
    {
        get => (IAudioCaptureService?)GetValue(CaptureServiceProperty);
        set => SetValue(CaptureServiceProperty, value);
    }

    private readonly WaveCanvasElement _canvas;

    public MasterWaveVisualizer()
    {
        InitializeComponent();

        _canvas = new WaveCanvasElement();
        WaveContainer.Child = _canvas;

        IsVisibleChanged += MasterWaveVisualizer_IsVisibleChanged;
        Loaded += MasterWaveVisualizer_Loaded;
        Unloaded += MasterWaveVisualizer_Unloaded;
    }

    private static void OnCaptureServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MasterWaveVisualizer visualizer)
        {
            visualizer._canvas.CaptureService = e.NewValue as IAudioCaptureService;
        }
    }

    private void MasterWaveVisualizer_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAnimationState();
    }

    private void MasterWaveVisualizer_Unloaded(object sender, RoutedEventArgs e)
    {
        _canvas.StopAnimation();
    }

    private void MasterWaveVisualizer_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        if (IsVisible && IsLoaded)
        {
            _canvas.StartAnimation();
        }
        else
        {
            _canvas.StopAnimation();
        }
    }

    internal void UpdateStatusText(float level)
    {
        if (level > 0.03f)
        {
            LevelPercentText.Text = $"{(int)(level * 100)}%";
            LiveIndicatorDot.Fill = new SolidColorBrush(Color.FromRgb(0, 210, 255));
            DotGlow.Color = Color.FromRgb(0, 210, 255);
            DotGlow.Opacity = 0.6;
        }
        else
        {
            LevelPercentText.Text = "CALM";
            LiveIndicatorDot.Fill = new SolidColorBrush(Color.FromRgb(56, 189, 248));
            DotGlow.Color = Color.FromRgb(56, 189, 248);
            DotGlow.Opacity = 0.25;
        }
    }

    /// <summary>
    /// High-performance FrameworkElement for drawing animated equalizer bars with mirror reflection.
    /// Smooth horizontal gradient: Cyan (#00D2FF) -> Blue (#38BDF8) -> Purple (#9D50BB).
    /// </summary>
    private sealed class WaveCanvasElement : FrameworkElement
    {
        public IAudioCaptureService? CaptureService { get; set; }

        private double _phase;
        private float _displayLevel;
        private bool _isAnimating;

        private readonly Brush _barsBrush;
        private readonly Brush _reflBrush;
        private readonly Pen _baselinePen;

        public WaveCanvasElement()
        {
            // Horizontal gradient brush from Cyan (#00D2FF) through Blue (#38BDF8) to Purple (#9D50BB)
            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(0, 210, 255), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(56, 189, 248), 0.45));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(157, 80, 187), 1.0));
            grad.Freeze();
            _barsBrush = grad;

            // Mirror reflection brush with soft opacity
            var reflGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0)
            };
            reflGrad.GradientStops.Add(new GradientStop(Color.FromArgb(60, 0, 210, 255), 0.0));
            reflGrad.GradientStops.Add(new GradientStop(Color.FromArgb(60, 56, 189, 248), 0.45));
            reflGrad.GradientStops.Add(new GradientStop(Color.FromArgb(60, 157, 80, 187), 1.0));
            reflGrad.Freeze();
            _reflBrush = reflGrad;

            // Subtle baseline divider pen
            var lineBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
            lineBrush.Freeze();
            _baselinePen = new Pen(lineBrush, 0.7);
            _baselinePen.Freeze();
        }

        public void StartAnimation()
        {
            if (_isAnimating) return;
            _isAnimating = true;
            CompositionTarget.Rendering += OnRendering;
        }

        public void StopAnimation()
        {
            if (!_isAnimating) return;
            _isAnimating = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            float targetLevel = CaptureService?.CurrentLevel ?? 0f;

            // Fast attack, smooth decay
            if (targetLevel > _displayLevel)
            {
                _displayLevel += (targetLevel - _displayLevel) * 0.4f;
            }
            else
            {
                _displayLevel += (targetLevel - _displayLevel) * 0.12f;
            }

            _phase += 0.06 + (_displayLevel * 0.12);
            if (_phase > Math.PI * 200)
            {
                _phase -= Math.PI * 200;
            }

            if (Parent is Border border && border.Parent is Grid grid && grid.Parent is MasterWaveVisualizer visualizer)
            {
                visualizer.UpdateStatusText(_displayLevel);
            }

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 30 || height < 15) return;

            // Baseline placed at ~64% from top, leaving room for main bars and reflection
            double baselineY = Math.Round(height * 0.63);
            double maxBarHeight = Math.Max(4.0, baselineY - 6.0);
            double maxReflHeight = Math.Max(2.0, (height - baselineY) - 4.0);

            // Draw subtle baseline guide
            dc.DrawLine(_baselinePen, new Point(10, baselineY), new Point(width - 10, baselineY));

            double barWidth = 3.0;
            double barGap = 2.5;
            double step = barWidth + barGap;

            int count = (int)Math.Max(10, Math.Floor((width - 32.0) / step));
            double totalBarsWidth = (count * step) - barGap;
            double startX = Math.Round((width - totalBarsWidth) / 2.0);

            for (int i = 0; i < count; i++)
            {
                double x = startX + (i * step);
                double u = (double)i / (count - 1); // 0.0 to 1.0

                // Frequency curve weighting (bass emphasis on left, mid harmonics, treble on right)
                double freqWeight = 0.55 + (0.45 * Math.Sin(Math.PI * (0.1 + (0.8 * u))));

                // Multi-harmonic oscillation
                double h1 = Math.Sin((2.8 * Math.PI * u) - (_phase * 1.3));
                double h2 = Math.Cos((5.2 * Math.PI * u) + (_phase * 0.8)) * 0.4;
                double waveDynamic = Math.Abs(h1 + h2);

                // Idle organic breathing height (2-3.5px)
                double idleHeight = 2.0 + (1.2 * Math.Sin((_phase * 0.7) + (i * 0.3)));

                // Audio reactive height
                double audioHeight = _displayLevel * maxBarHeight * freqWeight * (0.55 + (0.55 * waveDynamic));
                double barHeight = Math.Min(maxBarHeight, Math.Max(2.0, idleHeight + audioHeight));

                // 1. Main bar going upwards from baseline
                var mainRect = new Rect(x, baselineY - barHeight, barWidth, barHeight);
                dc.DrawRoundedRectangle(_barsBrush, null, mainRect, 1.5, 1.5);

                // 2. Mirror reflection going downwards from baseline
                double reflHeight = Math.Min(maxReflHeight, barHeight * 0.35);
                if (reflHeight > 1.0)
                {
                    var reflRect = new Rect(x, baselineY + 1.5, barWidth, reflHeight);
                    dc.DrawRoundedRectangle(_reflBrush, null, reflRect, 1.0, 1.0);
                }
            }
        }
    }
}
