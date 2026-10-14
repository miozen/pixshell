using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace PixShell.UI;

public class LatencyChart : FrameworkElement
{
    private double[] _ring = new double[60];
    private int _count;
    private int _head;

    private readonly Pen _gridPen;
    private readonly Brush _greenBrush;
    private readonly Brush _yellowBrush;
    private readonly Brush _redBrush;
    private readonly Brush _textBrush;
    private readonly Typeface _typeface;

    public LatencyChart()
    {
        _gridPen = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)), 1)
        {
            DashStyle = new DashStyle(new double[] { 2, 2 }, 0)
        };
        _gridPen.Freeze();

        _greenBrush = new SolidColorBrush(Color.FromArgb(204, 0x30, 0xD1, 0x58)); _greenBrush.Freeze();
        _yellowBrush = new SolidColorBrush(Color.FromArgb(204, 0xFF, 0x9F, 0x0A)); _yellowBrush.Freeze();
        _redBrush = new SolidColorBrush(Color.FromArgb(204, 0xFF, 0x45, 0x3A)); _redBrush.Freeze();

        _textBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // muted
        _textBrush.Freeze();

        _typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    }

    public void Push(double val)
    {
        _ring[_head] = val;
        _head = (_head + 1) % _ring.Length;
        if (_count < _ring.Length) _count++;
        InvalidateVisual();
    }

    public void Clear()
    {
        _count = 0;
        _head = 0;
        InvalidateVisual();
    }

    /// <summary>调整图表保留的采样数。改变时间窗时清空旧样本，避免混合不同窗口的比例。</summary>
    public void SetCapacity(int capacity)
    {
        capacity = Math.Clamp(capacity, 2, 300);
        if (capacity == _ring.Length) return;
        _ring = new double[capacity];
        Clear();
    }

    private double At(int logical)
    {
        var start = (_count < _ring.Length) ? 0 : _head;
        return _ring[(start + logical) % _ring.Length];
    }

    private string FormatLabel(double v)
    {
        return v.ToString("0");
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        if (_count == 0) return;

        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 10 || h < 10) return;

        double mx = 10; // minimum 10ms
        for (int i = 0; i < _count; i++)
        {
            mx = Math.Max(mx, At(i));
        }
        mx *= 1.1; // 10% headroom

        double labelWidth = 24;
        double chartX = labelWidth + 4;
        double chartW = w - chartX;

        int steps = 3;
        for (int i = 1; i <= steps; i++)
        {
            double val = mx * i / steps;
            double y = h - (h * i / steps); // Bottom to top

            // Draw line
            dc.DrawLine(_gridPen, new Point(chartX, y), new Point(w, y));

            // Draw label
            var text = FormatLabel(val);
#pragma warning disable CS0618
            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                _typeface,
                9,
                _textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
#pragma warning restore CS0618
            
            ft.TextAlignment = TextAlignment.Right;
            ft.MaxTextWidth = labelWidth;
            dc.DrawText(ft, new Point(0, y - ft.Height / 2));
        }

        double unit = chartW / _ring.Length;
        double barW = Math.Max(unit * 0.85, 1);
        double gap = unit * 0.15;
        double maxH = h * 0.95;

        for (int i = 0; i < _count; i++)
        {
            double x = chartX + i * (barW + gap);
            double val = At(i);
            double barH = maxH * (val / mx);

            if (barH > 0)
            {
                Brush b = val < 100 ? _greenBrush : (val < 200 ? _yellowBrush : _redBrush);
                dc.DrawRectangle(b, null, new Rect(x, h - barH, barW, Math.Max(1, barH)));
            }
        }
    }
}
