using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace MeditationApp.Controls
{
    public class BreathingCircleView : ContentView
    {
        public static readonly BindableProperty ScaleProperty = BindableProperty.Create(
            nameof(Scale), typeof(double), typeof(BreathingCircleView), 1.0, propertyChanged: OnScaleChanged);

        public static readonly BindableProperty CircleColorProperty = BindableProperty.Create(
            nameof(CircleColor), typeof(Color), typeof(BreathingCircleView), Colors.LightGreen, propertyChanged: OnColorChanged);

        public static readonly BindableProperty PhaseProperty = BindableProperty.Create(
            nameof(Phase), typeof(string), typeof(BreathingCircleView), string.Empty, propertyChanged: OnPhaseChanged);

        public static readonly BindableProperty RemainingTimeProperty = BindableProperty.Create(
            nameof(RemainingTime), typeof(int), typeof(BreathingCircleView), 0, propertyChanged: OnTimeChanged);

        private readonly Ellipse _outerRing;
        private readonly Ellipse _mainCircle;
        private readonly Label _timerLabel;
        private readonly Label _phaseLabel;

        public double Scale
        {
            get => (double)GetValue(ScaleProperty);
            set => SetValue(ScaleProperty, value);
        }

        public Color CircleColor
        {
            get => (Color)GetValue(CircleColorProperty);
            set => SetValue(CircleColorProperty, value);
        }

        public string Phase
        {
            get => (string)GetValue(PhaseProperty);
            set => SetValue(PhaseProperty, value);
        }

        public int RemainingTime
        {
            get => (int)GetValue(RemainingTimeProperty);
            set => SetValue(RemainingTimeProperty, value);
        }

        public BreathingCircleView()
        {
            HeightRequest = 300;
            WidthRequest = 300;

            var grid = new Grid
            {
                HeightRequest = 300,
                WidthRequest = 300
            };

            // Outer ring
            _outerRing = new Ellipse
            {
                WidthRequest = 280,
                HeightRequest = 280,
                Fill = Colors.Transparent,
                Stroke = Colors.LightGray,
                StrokeThickness = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Opacity = 0.3
            };

            // Main breathing circle
            _mainCircle = new Ellipse
            {
                WidthRequest = 200,
                HeightRequest = 200,
                Fill = CircleColor,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Opacity = 0.8
            };

            // Timer label
            _timerLabel = new Label
            {
                FontSize = 48,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                IsVisible = false
            };

            // Phase label
            _phaseLabel = new Label
            {
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 60)
            };

            grid.Children.Add(_outerRing);
            grid.Children.Add(_mainCircle);
            grid.Children.Add(_timerLabel);
            grid.Children.Add(_phaseLabel);

            Content = grid;
        }

        private static async void OnScaleChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BreathingCircleView control && newValue is double scale)
            {
                // Use ScaleTo animation for smooth scaling in MAUI
                await control._mainCircle.ScaleTo(scale, 100, Easing.Linear);
            }
        }

        private static void OnColorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BreathingCircleView control && newValue is Color color)
            {
                control._mainCircle.Fill = new SolidColorBrush(color);
            }
        }

        private static void OnPhaseChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BreathingCircleView control && newValue is string phase)
            {
                control._phaseLabel.Text = phase;
            }
        }

        private static void OnTimeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BreathingCircleView control && newValue is int time)
            {
                control._timerLabel.Text = time > 0 ? time.ToString() : "";
                control._timerLabel.IsVisible = time > 0;
            }
        }

        public async Task AnimateToScaleAsync(double targetScale, uint duration)
        {
            await _mainCircle.ScaleTo(targetScale, duration, Easing.SinInOut);
        }

        public async Task PulseAsync()
        {
            await _mainCircle.ScaleTo(1.1, 500, Easing.SinInOut);
            await _mainCircle.ScaleTo(1.0, 500, Easing.SinInOut);
        }
    }
}
