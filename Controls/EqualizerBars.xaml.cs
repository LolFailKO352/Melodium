using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Melodium.Controls;

public sealed partial class EqualizerBars : UserControl
{
    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(
            nameof(IsPlaying),
            typeof(bool),
            typeof(EqualizerBars),
            new PropertyMetadata(false, OnIsPlayingChanged));

    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(
            nameof(BarBrush),
            typeof(Brush),
            typeof(EqualizerBars),
            new PropertyMetadata(null, OnBarBrushChanged));

    public static readonly DependencyProperty BarHeightProperty =
        DependencyProperty.Register(
            nameof(BarHeight),
            typeof(double),
            typeof(EqualizerBars),
            new PropertyMetadata(14.0, OnDimensionsChanged));

    public static readonly DependencyProperty BarWidthProperty =
        DependencyProperty.Register(
            nameof(BarWidth),
            typeof(double),
            typeof(EqualizerBars),
            new PropertyMetadata(3.0, OnDimensionsChanged));

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public Brush? BarBrush
    {
        get => (Brush?)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public double BarHeight
    {
        get => (double)GetValue(BarHeightProperty);
        set => SetValue(BarHeightProperty, value);
    }

    public double BarWidth
    {
        get => (double)GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    private readonly Border[] _bars;
    private bool _isLoaded;
    private bool _isAnimating;

    public EqualizerBars()
    {
        this.InitializeComponent();
        _bars = new[] { Bar0, Bar1, Bar2, Bar3 };

        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyDimensions();
        ApplyBarBrush();
        UpdateAnimationState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        StopAnimations();
    }

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EqualizerBars control)
        {
            control.UpdateAnimationState();
        }
    }

    private static void OnBarBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EqualizerBars control)
        {
            control.ApplyBarBrush();
        }
    }

    private static void OnDimensionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EqualizerBars control)
        {
            control.ApplyDimensions();
        }
    }

    private void ApplyDimensions()
    {
        if (_bars == null) return;
        double w = BarWidth;
        double h = BarHeight;
        double corner = Math.Max(1.0, w / 2.0);

        foreach (var bar in _bars)
        {
            bar.Width = w;
            bar.Height = h;
            bar.CornerRadius = new CornerRadius(corner);
        }
    }

    private void ApplyBarBrush()
    {
        if (_bars == null || BarBrush == null) return;
        foreach (var bar in _bars)
        {
            bar.Background = BarBrush;
        }
    }

    private void UpdateAnimationState()
    {
        if (!_isLoaded) return;

        if (IsPlaying)
        {
            StartAnimations();
        }
        else
        {
            StopAnimations();
        }
    }

    private void StartAnimations()
    {
        if (_isAnimating || _bars == null) return;

        try
        {
            int[] durations = { 420, 310, 530, 370 };
            float[] minScales = { 0.20f, 0.35f, 0.18f, 0.28f };
            float[] maxScales = { 0.90f, 1.00f, 0.85f, 0.95f };

            for (int i = 0; i < _bars.Length; i++)
            {
                var bar = _bars[i];
                var visual = ElementCompositionPreview.GetElementVisual(bar);
                var compositor = visual.Compositor;

                visual.CenterPoint = new Vector3((float)bar.Width / 2f, (float)bar.Height, 0f);

                var anim = compositor.CreateScalarKeyFrameAnimation();
                anim.Duration = TimeSpan.FromMilliseconds(durations[i]);
                anim.IterationBehavior = AnimationIterationBehavior.Forever;

                var easing = compositor.CreateCubicBezierEasingFunction(
                    new Vector2(0.35f, 0.1f),
                    new Vector2(0.25f, 1.0f));

                anim.InsertKeyFrame(0.0f, minScales[i], easing);
                anim.InsertKeyFrame(0.5f, maxScales[i], easing);
                anim.InsertKeyFrame(1.0f, minScales[i], easing);

                visual.StartAnimation("Scale.Y", anim);
            }

            _isAnimating = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Equalizer animation error: {ex.Message}");
        }
    }

    private void StopAnimations()
    {
        if (_bars == null) return;

        try
        {
            for (int i = 0; i < _bars.Length; i++)
            {
                var bar = _bars[i];
                var visual = ElementCompositionPreview.GetElementVisual(bar);
                visual.StopAnimation("Scale.Y");
                visual.CenterPoint = new Vector3((float)bar.Width / 2f, (float)bar.Height, 0f);
                visual.Scale = new Vector3(1f, 0.25f, 1f);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Equalizer stop error: {ex.Message}");
        }

        _isAnimating = false;
    }
}
