using Microsoft.Maui.Controls;

namespace MeditationApp.Controls;

public partial class SkeletonView : ContentView
{
    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(SkeletonView), false, propertyChanged: OnIsLoadingChanged);

    public static readonly BindableProperty CornerRadiusProperty =
        BindableProperty.Create(nameof(CornerRadius), typeof(double), typeof(SkeletonView), 4.0);

    public static readonly BindableProperty HeightRequestProperty =
        BindableProperty.Create(nameof(HeightRequest), typeof(double), typeof(SkeletonView), 20.0);

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public new double HeightRequest
    {
        get => (double)GetValue(HeightRequestProperty);
        set => SetValue(HeightRequestProperty, value);
    }

    public SkeletonView()
    {
        InitializeComponent();
    }

    private static void OnIsLoadingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkeletonView skeleton)
        {
            skeleton.UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (IsLoading)
        {
            StartShimmerAnimation();
        }
        else
        {
            StopShimmerAnimation();
        }
    }

    private void StartShimmerAnimation()
    {
        var animation = new Animation(v => Opacity = v, 0.3, 1.0);
        animation.Commit(this, "ShimmerAnimation", 16, 1000, Easing.SinInOut, null, () => true);
    }

    private void StopShimmerAnimation()
    {
        this.AbortAnimation("ShimmerAnimation");
        Opacity = 1.0;
    }
}
