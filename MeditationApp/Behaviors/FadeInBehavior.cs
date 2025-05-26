using Microsoft.Maui.Controls;

namespace MeditationApp.Behaviors;

public class FadeInBehavior : Behavior<VisualElement>
{
    private VisualElement? _associatedObject;

    protected override void OnAttachedTo(VisualElement bindable)
    {
        _associatedObject = bindable;
        bindable.PropertyChanged += OnElementPropertyChanged;
        base.OnAttachedTo(bindable);
    }

    protected override void OnDetachingFrom(VisualElement bindable)
    {
        bindable.PropertyChanged -= OnElementPropertyChanged;
        _associatedObject = null;
        base.OnDetachingFrom(bindable);
    }

    private async void OnElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName && _associatedObject != null)
        {
            if (_associatedObject.IsVisible)
            {
                _associatedObject.Opacity = 0;
                await _associatedObject.FadeTo(1, 300, Easing.CubicInOut);
            }
        }
    }
}
