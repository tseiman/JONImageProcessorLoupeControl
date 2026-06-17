namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers
{
    using System;

    internal static class ButtonVisuals
    {
        public static void FillBackground(BitmapBuilder bitmapBuilder, PluginImageSize imageSize, BitmapColor color)
        {
            bitmapBuilder.FillRectangle(0, 0, imageSize.GetWidth(), imageSize.GetHeight(), color);
        }

        public static void DrawText(BitmapBuilder bitmapBuilder, String text, BitmapColor color)
        {
            bitmapBuilder.DrawText(text, color);
        }
    }
}
