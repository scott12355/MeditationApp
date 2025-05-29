using Microsoft.Maui.Graphics;
using System;

namespace MeditationApp.Drawing;

public class GlowingOrbDrawable : IDrawable
{
    private Color baseColor;
    private float animationPhase = 0;
    private const float animationSpeed = 0.015f; // Slightly faster animation

    public GlowingOrbDrawable(Color color)
    {
        this.baseColor = color;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        // Center of the drawing area
        float centerX = dirtyRect.Center.X;
        float centerY = dirtyRect.Center.Y;

        // Update animation phase
        animationPhase += animationSpeed;
        if (animationPhase > 2 * Math.PI)
        {
            animationPhase -= (float)(2 * Math.PI);
        }

        // Base size of the orb
        float baseSize = Math.Min(dirtyRect.Width, dirtyRect.Height) * 0.9f; // Make the orb slightly larger

        // Draw multiple concentric circles for a smoother gradient glow
        int numberOfCircles = 15; // More circles for smoother glow and better pulsation effect
        for (int i = numberOfCircles - 1; i >= 0; i--)
        {
            float progress = (float)i / numberOfCircles;
            
            // Vary the size more noticeably based on animation phase for pulsation
            float pulseEffect = (float)Math.Sin(animationPhase * 2) * 0.1f; // More rapid pulse
            float size = baseSize * (0.3f + progress * 0.7f + pulseEffect); // Adjusted base size factor and added pulse

            // Interpolate color from baseColor to a brighter shade based on animation phase
            // Introduce a slight color shift based on animation phase
            float color_progress_variation = (float)Math.Sin(animationPhase) * 0.05f; // Subtle color shift
            float color_progress = Math.Clamp(progress + color_progress_variation, 0, 1);

            // Interpolate from a slightly lighter shade of the base color towards the base color
            Color startColor = Color.FromHsla(baseColor.GetHue(), baseColor.GetSaturation(), Math.Min(1.0f, baseColor.GetLuminosity() + 0.2f));
            Color endColor = baseColor;

            // Manual linear interpolation between startColor and endColor
            double r = startColor.Red + (endColor.Red - startColor.Red) * color_progress;
            double g = startColor.Green + (endColor.Green - startColor.Green) * color_progress;
            double b = startColor.Blue + (endColor.Blue - startColor.Blue) * color_progress;
            double a = startColor.Alpha + (endColor.Alpha - startColor.Alpha) * color_progress;

            Color interpolatedColor = new Color((float)r, (float)g, (float)b, (float)a);

            // Opacity decreases outwards
            float opacity = (1.0f - progress) * 0.7f; // Adjusted opacity range for more glow

            canvas.FillColor = interpolatedColor.WithAlpha(opacity);

            canvas.FillCircle(centerX, centerY, size / 2);
        }

        canvas.RestoreState();
    }
} 