using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace SLSKDONET.Views.Avalonia.Controls
{
    public class WaveformControl : Control
    {
        public static readonly StyledProperty<byte[]> DataProperty =
            AvaloniaProperty.Register<WaveformControl, byte[]>(nameof(Data), Array.Empty<byte>());

        public byte[] Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
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
            AffectsRender<WaveformControl>(DataProperty, ProgressProperty);
        }

        public override void Render(DrawingContext context)
        {
            if (Data == null || Data.Length == 0)
            {
                // Draw a simple line if no data
                context.DrawLine(new Pen(Brushes.Gray, 1), new Point(0, Bounds.Height / 2), new Point(Bounds.Width, Bounds.Height / 2));
                return;
            }

            var width = Bounds.Width;
            var height = Bounds.Height;
            var mid = height / 2;

            // Simple waveform rendering (assuming Data is array of peaks)
            // Rekordbox PWAV is actually more complex (bits and colors), 
            // but for a start we treat it as 1 byte per column or similar.
            
            var pen = new Pen(Brushes.Gray, 1);
            var activePen = new Pen(Brushes.Cyan, 1);
            
            int samples = Data.Length;
            double step = width / samples;

            for (int i = 0; i < samples; i++)
            {
                float val = Data[i] / 255f;
                double x = i * step;
                double h = val * mid;
                
                var currentPen = (x / width <= Progress) ? activePen : pen;
                context.DrawLine(currentPen, new Point(x, mid - h), new Point(x, mid + h));
            }
        }
    }
}
