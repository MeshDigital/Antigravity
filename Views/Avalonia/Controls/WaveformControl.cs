using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using SLSKDONET.Models;

namespace SLSKDONET.Views.Avalonia.Controls
{
    public class WaveformControl : Control
    {
        public static readonly StyledProperty<WaveformAnalysisData> WaveformDataProperty =
            AvaloniaProperty.Register<WaveformControl, WaveformAnalysisData>(nameof(WaveformData));

        public WaveformAnalysisData WaveformData
        {
            get => GetValue(WaveformDataProperty);
            set => SetValue(WaveformDataProperty, value);
        }

        public static readonly StyledProperty<float> ProgressProperty =
            AvaloniaProperty.Register<WaveformControl, float>(nameof(Progress), 0f);

        public float Progress
        {
            get => GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        static WaveformControl()
        {
            AffectsRender<WaveformControl>(WaveformDataProperty, ProgressProperty);
        }

        public override void Render(DrawingContext context)
        {
            var data = WaveformData;
            if (data == null || data.IsEmpty)
            {
                // Draw a simple flat line if no data
                context.DrawLine(new Pen(Brushes.Gray, 1), new Point(0, Bounds.Height / 2), new Point(Bounds.Width, Bounds.Height / 2));
                return;
            }

            var width = Bounds.Width;
            var height = Bounds.Height;
            var mid = height / 2;

            // "Max Ultra" Tri-Color Rendering
            // 1. RMS Body (Blue/Cyan) - Shows average energy/loudness
            // 2. Peak Spikes (White) - Shows transient detail
            // 3. Progress Overlay - Dimmed vs Bright

            var rmsPen = new Pen(new SolidColorBrush(Color.Parse("#00BFFF")), 1); // Deep Sky Blue
            var peakPen = new Pen(Brushes.White, 1);
            var playedOverlayBrush = new SolidColorBrush(Colors.Black, 0.5); // Dim played sections? Or maybe just color differently.
            
            // Actually, standard DJ look:
            // Played: Bright Blue/White
            // Unplayed: Dim Blue/Gray
            
            var unplayedRmsPen = new Pen(new SolidColorBrush(Color.Parse("#4000BFFF")), 1); // Dim Blue
            var unplayedPeakPen = new Pen(new SolidColorBrush(Color.Parse("#80FFFFFF")), 1); // Dim White
            
            var playedRmsPen = new Pen(new SolidColorBrush(Color.Parse("#00BFFF")), 1); // Bright Blue
            var playedPeakPen = new Pen(Brushes.White, 1);

            int samples = data.PeakData.Length;
            double step = width / samples;

            // If too many samples for pixels, we can decimate or skip, but drawing lines is fast enough usually
            // Optimization: If samples > width, we should average to pixel width to avoid overdraw
            
            // Drawing loop
            for (int i = 0; i < samples; i++)
            {
                double x = i * step;
                if (x > width) break;

                bool isPlayed = (float)i / samples <= Progress;

                // Normalized height (0.0 - 1.0)
                float peakVal = data.PeakData[i] / 255f;
                float rmsVal = data.RmsData[i] / 255f;

                double peakH = peakVal * mid;
                double rmsH = rmsVal * mid;

                // Select Pens
                var currentRmsPen = isPlayed ? playedRmsPen : unplayedRmsPen;
                var currentPeakPen = isPlayed ? playedPeakPen : unplayedPeakPen;

                // Draw RMS (Thicker/Body)
                // Offset x slightly if needed, or draw line
                context.DrawLine(currentRmsPen, new Point(x, mid - rmsH), new Point(x, mid + rmsH));

                // Draw Peak (Tips) - Only if peak > rms (it usually is)
                if (peakH > rmsH)
                {
                     // Top spike
                     context.DrawLine(currentPeakPen, new Point(x, mid - peakH), new Point(x, mid - rmsH));
                     // Bottom spike
                     context.DrawLine(currentPeakPen, new Point(x, mid + rmsH), new Point(x, mid + peakH));
                }
            }
        }
    }
}
