using Microsoft.Maui.Graphics;
using MeditationApp.Services;
using System.Collections.ObjectModel;

namespace MeditationApp.Controls;

public partial class MoodChartView : ContentView
{
    private ObservableCollection<MoodDataPoint> _moodData;
    private MoodChartDrawable _chartDrawable;

    public static readonly BindableProperty MoodDataProperty =
        BindableProperty.Create(nameof(MoodData), typeof(ObservableCollection<MoodDataPoint>), typeof(MoodChartView),
            new ObservableCollection<MoodDataPoint>(), propertyChanged: OnMoodDataChanged);

    public ObservableCollection<MoodDataPoint> MoodData
    {
        get => (ObservableCollection<MoodDataPoint>)GetValue(MoodDataProperty);
        set => SetValue(MoodDataProperty, value);
    }

    public MoodChartView()
    {
        InitializeComponent();
        _moodData = new ObservableCollection<MoodDataPoint>();
        _chartDrawable = new MoodChartDrawable(_moodData);
        ChartGraphicsView.Drawable = _chartDrawable;
    }

    private static void OnMoodDataChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MoodChartView chartView && newValue is ObservableCollection<MoodDataPoint> moodData)
        {
            chartView.UpdateChart(moodData);
        }
    }

    private void UpdateChart(ObservableCollection<MoodDataPoint> moodData)
    {
        _moodData = moodData;
        _chartDrawable.UpdateData(moodData);
        ChartGraphicsView.Invalidate();
        UpdateDayLabels();
    }

    private void UpdateDayLabels()
    {
        DayLabelsContainer.Children.Clear();

        foreach (var dataPoint in _moodData)
        {
            var label = new Label
            {
                Text = dataPoint.DayName,
                FontSize = 12,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.Center,
                WidthRequest = 40
            };
            DayLabelsContainer.Children.Add(label);
        }
    }
}

public class MoodChartDrawable : IDrawable
{
    private ObservableCollection<MoodDataPoint> _moodData;
    private readonly Color[] _moodColors = new Color[]
    {
        Color.FromArgb("#FF6B6B"), // Mood 1 - Very Sad
        Color.FromArgb("#FFA07A"), // Mood 2 - Sad
        Color.FromArgb("#FFD93D"), // Mood 3 - Neutral
        Color.FromArgb("#6BCF7F"), // Mood 4 - Happy
        Color.FromArgb("#4ECDC4"), // Mood 5 - Very Happy
        Color.FromArgb("#E9ECEF")  // No Data
    };

    public MoodChartDrawable(ObservableCollection<MoodDataPoint> moodData)
    {
        _moodData = moodData;
    }

    public void UpdateData(ObservableCollection<MoodDataPoint> moodData)
    {
        _moodData = moodData;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_moodData == null || _moodData.Count == 0)
            return;

        canvas.SaveState();

        // Chart dimensions
        float padding = 20;
        float chartWidth = dirtyRect.Width - (padding * 2);
        float chartHeight = dirtyRect.Height - (padding * 2);
        float startX = padding;
        float endX = padding + chartWidth;

        // Draw background
        canvas.FillColor = Color.FromArgb("#F8F9FA");
        canvas.FillRectangle(dirtyRect);

        // Draw grid lines
        DrawGridLines(canvas, dirtyRect, padding, chartWidth, chartHeight);

        // Draw line chart
        DrawLineChart(canvas, padding, chartWidth, chartHeight);

        canvas.RestoreState();
    }

    private void DrawLineChart(ICanvas canvas, float padding, float chartWidth, float chartHeight)
    {
        List<PointF> points = new List<PointF>();
        float xStep = chartWidth / (_moodData.Count - 1); // Calculate step for x-axis

        for (int i = 0; i < _moodData.Count; i++)
        {
            var dataPoint = _moodData[i];
            float x = padding + (i * xStep);
            float y;

            if (dataPoint.HasData && dataPoint.Mood.HasValue)
            {
                // Map mood (1-5) to y-coordinate (higher mood = lower y value in canvas)
                y = padding + chartHeight - ((dataPoint.Mood.Value / 5.0f) * chartHeight * 0.8f);
            }
            else
            {
                // For no data, place it at the lowest mood level visually or a baseline
                y = padding + chartHeight - (1.0f / 5.0f * chartHeight * 0.8f); // Represent as mood 1, but with no data color
            }
            points.Add(new PointF(x, y));
        }

        // Draw the line
        canvas.StrokeColor = Colors.DarkOrange;
        canvas.StrokeSize = 3;
        canvas.StrokeLineJoin = LineJoin.Round;

        if (points.Count > 1)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                canvas.DrawLine(points[i], points[i + 1]);
            }
        }

        // Draw data points and emojis
        for (int i = 0; i < _moodData.Count; i++)
        {
            var dataPoint = _moodData[i];
            PointF point = points[i];

            if (dataPoint.HasData && dataPoint.Mood.HasValue)
            {
                canvas.FillColor = _moodColors[dataPoint.Mood.Value - 1];
                canvas.FillCircle(point, 8);

                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = 2;
                canvas.DrawCircle(point, 8);

                canvas.FontSize = 16;
                canvas.FontColor = Colors.Black;
                canvas.DrawString(dataPoint.MoodEmoji,
                    point.X, point.Y - 25, HorizontalAlignment.Center);
            }
            else
            {
                // Draw a hollow circle for no data
                canvas.StrokeColor = _moodColors[5]; // No data color
                canvas.StrokeSize = 2;
                canvas.DrawCircle(point, 8);
            }
        }
    }

    private void DrawGridLines(ICanvas canvas, RectF dirtyRect, float padding, float chartWidth, float chartHeight)
    {
        canvas.StrokeColor = Color.FromArgb("#DEE2E6");
        canvas.StrokeSize = 1;

        // Horizontal grid lines (mood levels)
        for (int i = 1; i <= 5; i++)
        {
            float y = padding + (chartHeight * (1 - (i / 5.0f)));
            canvas.DrawLine(padding, y, padding + chartWidth, y);
        }

        // Vertical grid lines (days)
        // Only draw grid lines between points if there are multiple points
        if (_moodData.Count > 1)
        {
            float xStep = chartWidth / (_moodData.Count - 1);
            for (int i = 0; i < _moodData.Count; i++)
            {
                float x = padding + (i * xStep);
                canvas.DrawLine(x, padding, x, padding + chartHeight);
            }
        }
    }
}