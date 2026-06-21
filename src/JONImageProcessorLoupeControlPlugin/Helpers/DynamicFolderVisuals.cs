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
            var background = connected ? new BitmapColor(color.R, color.G, color.B) : Colors.DisabledBackground;
            var textColor = connected && color.Brightness < 128 ? BitmapColor.White : Colors.DisabledText;

            bitmapBuilder.FillRectangle(0, 0, width, height, BitmapColor.Black);
            DrawRoundedRectangle(bitmapBuilder, 0, 0, width, height, Math.Max(4, Math.Min(width, height) / 6), background);
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
    }
}
