namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Tools
{
    using System;
    using System.IO;
    using System.Threading;

    using Loupedeck.Devices.Loupedeck2Devices;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Drawing.Processing;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;

    public sealed class MultiWheelClockTool : WheelTool
    {
        private const Int32 TouchDebounceMs = 500;
        private const Int32 WheelCanvasSize = 512;
        private readonly Object _drawLock = new();
        private DateTime _lastTouch = DateTime.MinValue;
        private MultiWheelDispatch _multiWheelDispatch;
        private Timer _tickTimer;

        public MultiWheelClockTool()
            : base(templateDisplayName: "JON Preset Select / Clock", templateGroupName: "JON Image Processor")
        {
        }

        protected override void OnStart()
        {
            base.OnStart();

            this._multiWheelDispatch = ServiceDirectory.Get(ServiceDirectory.T_MultiWheelDispatch) as MultiWheelDispatch;
            if (this._multiWheelDispatch != null)
            {
                this._multiWheelDispatch.DisplayChanged += this.RequestRedraw;
            }

            this._tickTimer = new Timer(_ => this.RequestRedraw(), null, 1000, 1000);
            this.RequestRedraw();
        }

        protected override void OnStop()
        {
            this._tickTimer?.Dispose();
            this._tickTimer = null;

            if (this._multiWheelDispatch != null)
            {
                this._multiWheelDispatch.DisplayChanged -= this.RequestRedraw;
            }

            base.OnStop();
        }

        protected override BitmapImage CreateImage()
        {
            var activeDisplay = this._multiWheelDispatch?.ActiveDisplay;
            using var bitmapBuilder = this.CreateBitmapBuilder();
            if (activeDisplay != null)
            {
                activeDisplay.RenderDisplay(bitmapBuilder);
                return bitmapBuilder.ToImage();
            }

            bitmapBuilder.FillRectangle(0, 0, 512, 512, BitmapColor.Black);
            bitmapBuilder.SetBackgroundImage(RenderAnalogClock(DateTime.Now));
            return bitmapBuilder.ToImage();
        }

        protected override void OnEncoderEvent(DeviceEncoderEvent encoderEvent)
        {
            base.OnEncoderEvent(encoderEvent);

            if (encoderEvent == null || encoderEvent.Clicks == 0)
            {
                return;
            }

            this._multiWheelDispatch ??= ServiceDirectory.Get(ServiceDirectory.T_MultiWheelDispatch) as MultiWheelDispatch;
            this._multiWheelDispatch?.ApplyAdjustment(encoderEvent.Clicks);
            this.RequestRedraw();
        }

        protected override void OnTouchEvent(DeviceTouchEvent touchEvent)
        {
            base.OnTouchEvent(touchEvent);

            if (touchEvent == null)
            {
                return;
            }

            this._multiWheelDispatch ??= ServiceDirectory.Get(ServiceDirectory.T_MultiWheelDispatch) as MultiWheelDispatch;
            var activeDisplay = this._multiWheelDispatch?.ActiveDisplay;
            if (activeDisplay == null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - this._lastTouch).TotalMilliseconds < TouchDebounceMs)
            {
                return;
            }

            this._lastTouch = now;
            activeDisplay.Touch();
            this._multiWheelDispatch.InformUploadCompleted();
            this.RequestRedraw();
        }

        private void RequestRedraw()
        {
            lock (this._drawLock)
            {
                try
                {
                    this.Draw();
                }
                catch (Exception ex)
                {
                    PluginLog.Warning($"[MultiWheelClockTool] draw failed: {ex.Message}");
                }
            }
        }

        private static BitmapImage RenderAnalogClock(DateTime now)
        {
            using var image = new Image<Rgba32>(WheelCanvasSize, WheelCanvasSize, Color.Black);
            using var stream = new MemoryStream();
            var center = new PointF(WheelCanvasSize / 2f, WheelCanvasSize / 2f);
            var radius = (WheelCanvasSize / 2f) - 16f;

            image.Mutate(context =>
            {
                var rim = new SixLabors.ImageSharp.Drawing.EllipsePolygon(center, radius);
                context.Draw(Color.White, 3f, rim);

                for (var i = 0; i < 12; i++)
                {
                    var angle = (i * 30f - 90f) * (Single)Math.PI / 180f;
                    var inner = i % 3 == 0 ? radius - 32f : radius - 18f;
                    var outer = radius - 6f;
                    var p1 = new PointF(center.X + (Single)Math.Cos(angle) * inner, center.Y + (Single)Math.Sin(angle) * inner);
                    var p2 = new PointF(center.X + (Single)Math.Cos(angle) * outer, center.Y + (Single)Math.Sin(angle) * outer);
                    context.DrawLine(Color.White, i % 3 == 0 ? 5f : 2f, p1, p2);
                }

                var hourAngle = ((now.Hour % 12) * 30f + now.Minute * 0.5f - 90f) * (Single)Math.PI / 180f;
                var minuteAngle = (now.Minute * 6f + now.Second * 0.1f - 90f) * (Single)Math.PI / 180f;
                var secondAngle = (now.Second * 6f - 90f) * (Single)Math.PI / 180f;

                DrawHand(context, center, hourAngle, radius * 0.5f, 10f, Color.White);
                DrawHand(context, center, minuteAngle, radius * 0.75f, 6f, Color.White);
                DrawHand(context, center, secondAngle, radius * 0.85f, 2f, Color.Red);

                var cap = new SixLabors.ImageSharp.Drawing.EllipsePolygon(center, 10f);
                context.Fill(Color.White, cap);
            });

            image.SaveAsPng(stream);
            return BitmapImage.FromArray(stream.ToArray());
        }

        private static void DrawHand(IImageProcessingContext context, PointF center, Single angle, Single length, Single width, Color color)
        {
            var tip = new PointF(center.X + (Single)Math.Cos(angle) * length, center.Y + (Single)Math.Sin(angle) * length);
            context.DrawLine(color, width, center, tip);
        }
    }
}
