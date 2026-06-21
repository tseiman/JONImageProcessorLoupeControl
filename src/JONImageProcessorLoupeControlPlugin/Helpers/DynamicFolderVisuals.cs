namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;

    internal static class DynamicFolderVisuals
    {
        public static BitmapImage CreateToggleImage(Boolean connected, Boolean enabled, PluginImageSize imageSize)
        {
            var background = !connected
                ? Colors.DisabledBackground
                : enabled ? Colors.Green : Colors.Red;
            var textColor = connected ? BitmapColor.White : Colors.DisabledText;

            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var width = imageSize.GetWidth();
            var height = imageSize.GetHeight();

            bitmapBuilder.FillRectangle(0, 0, width, height, BitmapColor.Black);
            DrawRoundedRectangle(bitmapBuilder, 0, 0, width, height, Math.Max(4, Math.Min(width, height) / 6), background);
            ButtonVisuals.DrawText(bitmapBuilder, enabled ? "ON" : "OFF", textColor);
            return bitmapBuilder.ToImage();
        }

        public static BitmapImage CreateTextImage(String text, Boolean connected, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, connected ? BitmapColor.Black : Colors.DisabledBackground);
            ButtonVisuals.DrawText(bitmapBuilder, text, connected ? BitmapColor.White : Colors.DisabledText);
            return bitmapBuilder.ToImage();
        }

        public static BitmapImage CreateColorImage(BackgroundRgba color, PluginImageSize imageSize, Boolean connected, String text = null)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var width = imageSize.GetWidth();
            var height = imageSize.GetHeight();
            var textColor = connected && color.Brightness < 128 ? BitmapColor.White : Colors.DisabledText;

            bitmapBuilder.FillRectangle(0, 0, width, height, BitmapColor.Black);
            if (connected)
            {
                DrawAlphaColorRoundedRectangle(bitmapBuilder, 0, 0, width, height, Math.Max(4, Math.Min(width, height) / 6), color);
            }
            else
            {
                DrawRoundedRectangle(bitmapBuilder, 0, 0, width, height, Math.Max(4, Math.Min(width, height) / 6), Colors.DisabledBackground);
            }

            ButtonVisuals.DrawText(bitmapBuilder, text ?? color.ToHex(), textColor);
            return bitmapBuilder.ToImage();
        }

        public static void DrawRoundedRectangle(BitmapBuilder bitmapBuilder, Int32 x, Int32 y, Int32 width, Int32 height, Int32 radius, BitmapColor color)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            for (var row = 0; row < height; row++)
            {
                var topCurve = row < radius;
                var bottomCurve = row >= height - radius;
                var curveY = topCurve ? radius - row : bottomCurve ? row - (height - radius - 1) : 0;
                var inset = topCurve || bottomCurve
                    ? (Int32)Math.Round(radius - Math.Sqrt(Math.Max(0, (radius * radius) - (curveY * curveY))))
                    : 0;
                var rowWidth = width - (inset * 2);
                if (rowWidth > 0)
                {
                    bitmapBuilder.FillRectangle(x + inset, y + row, rowWidth, 1, color);
                }
            }
        }

        private static void DrawAlphaColorRoundedRectangle(BitmapBuilder bitmapBuilder, Int32 x, Int32 y, Int32 width, Int32 height, Int32 radius, BackgroundRgba color)
        {
            radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            var tileSize = Math.Max(6, Math.Min(width, height) / 7);
            var alpha = Math.Clamp(color.A, 0, 255) / 255.0;

            for (var row = 0; row < height; row++)
            {
                var topCurve = row < radius;
                var bottomCurve = row >= height - radius;
                var curveY = topCurve ? radius - row : bottomCurve ? row - (height - radius - 1) : 0;
                var inset = topCurve || bottomCurve
                    ? (Int32)Math.Round(radius - Math.Sqrt(Math.Max(0, (radius * radius) - (curveY * curveY))))
                    : 0;

                for (var col = inset; col < width - inset; col++)
                {
                    var useLightTile = (((col / tileSize) + (row / tileSize)) % 2) == 0;
                    var blended = useLightTile
                        ? Blend(0xb0, 0xb0, 0xb0, color, alpha)
                        : Blend(0x66, 0x66, 0x66, color, alpha);
                    bitmapBuilder.FillRectangle(x + col, y + row, 1, 1, blended);
                }
            }
        }

        private static BitmapColor Blend(Int32 backgroundR, Int32 backgroundG, Int32 backgroundB, BackgroundRgba foreground, Double alpha)
        {
            Byte BlendChannel(Int32 backgroundChannel, Int32 foregroundChannel) =>
                (Byte)Math.Clamp((Int32)Math.Round((foregroundChannel * alpha) + (backgroundChannel * (1.0 - alpha))), 0, 255);

            return new BitmapColor(
                BlendChannel(backgroundR, foreground.R),
                BlendChannel(backgroundG, foreground.G),
                BlendChannel(backgroundB, foreground.B));
        }
    }
}
