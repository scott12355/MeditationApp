using Microsoft.Maui.Controls;

namespace MeditationApp.Behaviors;

public class FloatingAnimationBehavior : Behavior<VisualElement>
{
    public static readonly BindableProperty FloatRangeXProperty = BindableProperty.Create(
        nameof(FloatRangeX), typeof(double), typeof(FloatingAnimationBehavior), 30.0);

    public static readonly BindableProperty FloatRangeYProperty = BindableProperty.Create(
        nameof(FloatRangeY), typeof(double), typeof(FloatingAnimationBehavior), 20.0);

    public static readonly BindableProperty DurationProperty = BindableProperty.Create(
        nameof(Duration), typeof(uint), typeof(FloatingAnimationBehavior), (uint)8000);

    public static readonly BindableProperty DelayProperty = BindableProperty.Create(
        nameof(Delay), typeof(uint), typeof(FloatingAnimationBehavior), (uint)0);

    public double FloatRangeX
    {
        get => (double)GetValue(FloatRangeXProperty);
        set => SetValue(FloatRangeXProperty, value);
    }

    public double FloatRangeY
    {
        get => (double)GetValue(FloatRangeYProperty);
        set => SetValue(FloatRangeYProperty, value);
    }

    public uint Duration
    {
        get => (uint)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public uint Delay
    {
        get => (uint)GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    private VisualElement? _associatedObject;
    private bool _isAnimating;

    protected override void OnAttachedTo(VisualElement bindable)
    {
        base.OnAttachedTo(bindable);
        _associatedObject = bindable;
        
        // Start animation when the element is loaded
        bindable.Loaded += OnElementLoaded;
    }

    protected override void OnDetachingFrom(VisualElement bindable)
    {
        base.OnDetachingFrom(bindable);
        bindable.Loaded -= OnElementLoaded;
        _isAnimating = false;
        _associatedObject = null;
    }

    private async void OnElementLoaded(object? sender, EventArgs e)
    {
        if (_associatedObject != null && !_isAnimating)
        {
            _isAnimating = true;
            await Task.Delay((int)Delay);
            _ = StartFloatingAnimation();
        }
    }

    private async Task StartFloatingAnimation()
    {
        if (_associatedObject == null) return;

        try
        {
            while (_isAnimating && _associatedObject != null)
            {
                // Random movement within the specified range
                var random = new Random();
                var targetX = (random.NextDouble() - 0.5) * 2 * FloatRangeX;
                var targetY = (random.NextDouble() - 0.5) * 2 * FloatRangeY;
                var targetScale = 0.9 + (random.NextDouble() * 0.3); // Scale between 0.9 and 1.2

                var animationDuration = Duration + (uint)(random.NextDouble() * 2000 - 1000); // Vary duration slightly

                // Animate to new position
                var tasks = new Task[]
                {
                    _associatedObject.TranslateTo(targetX, targetY, animationDuration, Easing.SinInOut),
                    _associatedObject.ScaleTo(targetScale, animationDuration / 2, Easing.SinInOut)
                };

                await Task.WhenAll(tasks);

                // Return to normal scale
                await _associatedObject.ScaleTo(1.0, animationDuration / 2, Easing.SinInOut);

                // Small delay before next movement
                await Task.Delay(500);
            }
        }
        catch
        {
            // Handle disposal or other exceptions gracefully
        }
    }
}
