using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace AVAFlight.Avalonia.Rendering;

/// <summary>
/// Base class for views that draw directly with SkiaSharp (star map, space, terrain, combat).
/// Avalonia has no built-in "SKCanvasControl"; this control is the equivalent, using
/// <see cref="ICustomDrawOperation"/> plus the <see cref="ISkiaSharpApiLeaseFeature"/> to
/// obtain the renderer's live <see cref="SKCanvas"/>. Drawing happens in logical (DIP)
/// units; the canvas transform already includes the HiDPI scale factor.
/// </summary>
public abstract class SkiaView : Control
{
    private DispatcherTimer? _timer;
    private DateTime _lastTick;

    /// <summary>When true, the view redraws continuously (~60 fps) and receives <see cref="OnTick"/>.</summary>
    protected bool Animated { get; init; }

    /// <summary>Elapsed animation time in seconds; deterministic when set externally (screenshots/tests).</summary>
    public double Time { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!Animated) return;
        _lastTick = DateTime.UtcNow;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            var now = DateTime.UtcNow;
            var dt = Math.Min(0.1, (now - _lastTick).TotalSeconds);
            _lastTick = now;
            Time += dt;
            OnTick(dt);
            InvalidateVisual();
        });
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Per-frame update hook for animated views.</summary>
    protected virtual void OnTick(double dt) { }

    public sealed override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.Custom(new DrawOp(this, bounds));
    }

    /// <summary>Draws the view. <paramref name="size"/> is in logical pixels.</summary>
    protected abstract void RenderSkia(SKCanvas canvas, SKSize size);

    private sealed class DrawOp(SkiaView owner, Rect bounds) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;
        public bool HitTest(Point p) => bounds.Contains(p);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null) return; // Non-Skia backend: nothing we can draw.
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            int save = canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
            try
            {
                owner.RenderSkia(canvas, new SKSize((float)bounds.Width, (float)bounds.Height));
            }
            finally
            {
                canvas.RestoreToCount(save);
            }
        }
    }
}
