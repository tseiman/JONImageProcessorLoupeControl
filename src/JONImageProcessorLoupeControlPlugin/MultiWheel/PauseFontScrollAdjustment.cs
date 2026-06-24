namespace Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    using SixLabors.Fonts;
    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Drawing.Processing;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;

    internal sealed class PauseFontScrollAdjustment : IMultiWheelAdjustment, IMultiWheelDisplayable, IMultiWheelRenderedDisplayable
    {
        private const Int32 MultiWheelScrollTicksPerFont = 10;
        private const Int32 WheelCanvasSize = 512;
        private readonly JonPauseControl _pauseControl;
        private readonly Object _lock = new();
        private IReadOnlyList<JonAssetSummary> _fonts = Array.Empty<JonAssetSummary>();
        private Int32 _currentIndex;
        private Int32 _scrollAccumulator;
        private Int32 _lastFontStepDirection;
        private Int32 _lastScrollDirection;
        private Boolean _isLoading;

        public PauseFontScrollAdjustment(JonPauseControl pauseControl)
        {
            this._pauseControl = pauseControl;
        }

        public event Action DisplayChanged;

        public async Task ReloadFontsAsync()
        {
            if (this._pauseControl == null || !this._pauseControl.IsConnected)
            {
                this.SetFonts(Array.Empty<JonAssetSummary>());
                return;
            }

            lock (this._lock)
            {
                this._isLoading = true;
            }

            this.DisplayChanged?.Invoke();

            try
            {
                this.SetFonts(await this._pauseControl.ListFontsAsync().ConfigureAwait(false), this._pauseControl.Font);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseFontScrollAdjustment] font list failed: {ex.Message}");
                this.SetFonts(Array.Empty<JonAssetSummary>());
            }
            finally
            {
                lock (this._lock)
                {
                    this._isLoading = false;
                }

                this.DisplayChanged?.Invoke();
            }
        }

        public void ApplyAdjustment(Int32 diff)
        {
            IReadOnlyList<JonAssetSummary> fonts;
            lock (this._lock)
            {
                fonts = this._fonts;
            }

            if (fonts.Count == 0)
            {
                return;
            }

            var scrollDirection = Math.Sign(diff);
            if (scrollDirection == 0)
            {
                return;
            }

            if (this._lastFontStepDirection != 0 && scrollDirection == -this._lastFontStepDirection && this._scrollAccumulator == 0)
            {
                this.SelectRelativeFont(scrollDirection);
                return;
            }

            if (this._lastScrollDirection != 0 && scrollDirection != this._lastScrollDirection)
            {
                this._scrollAccumulator = 0;
            }

            this._lastScrollDirection = scrollDirection;
            this._scrollAccumulator += diff;
            var fontStep = this._scrollAccumulator / MultiWheelScrollTicksPerFont;
            if (fontStep == 0)
            {
                return;
            }

            this._scrollAccumulator -= fontStep * MultiWheelScrollTicksPerFont;
            this.SelectRelativeFont(fontStep, resetAccumulator: false);
        }

        public void RenderDisplay(BitmapBuilder bitmapBuilder)
        {
            bitmapBuilder.FillRectangle(0, 0, 512, 512, BitmapColor.Black);
            bitmapBuilder.DrawText(this.GetDisplayText(), BitmapColor.White);
        }

        public BitmapImage RenderDisplayImage()
        {
            using var image = new Image<Rgba32>(WheelCanvasSize, WheelCanvasSize, Color.Black);
            using var stream = new MemoryStream();
            var (font, index, count, isLoading) = this.GetCurrentFontDisplayData();
            var displayText = isLoading
                ? "Loading Fonts"
                : font == null
                    ? "No Fonts"
                    : font.Name;
            var indexText = count > 0 ? $"{index + 1}/{count}" : "";
            var previewFont = CreatePreviewFont(font, 76, FontStyle.Regular);
            var indexFont = SystemFonts.CreateFont("Arial", 44, FontStyle.Bold);
            var labelFont = SystemFonts.CreateFont("Arial", 24, FontStyle.Regular);
            var maxTextWidth = WheelCanvasSize - 56;
            var previewText = TrimToWidth(displayText, previewFont, maxTextWidth);
            var previewSize = TextMeasurer.MeasureSize(previewText, new TextOptions(previewFont));
            var indexSize = TextMeasurer.MeasureSize(indexText, new TextOptions(indexFont));
            var typeText = font?.Type ?? "";
            var typeSize = TextMeasurer.MeasureSize(typeText, new TextOptions(labelFont));
            var totalHeight = previewSize.Height + 18 + indexSize.Height + (String.IsNullOrWhiteSpace(typeText) ? 0 : 12 + typeSize.Height);
            var y = (WheelCanvasSize - totalHeight) / 2f;

            image.Mutate(context =>
            {
                context.DrawText(previewText, previewFont, Color.White, new PointF((WheelCanvasSize - previewSize.Width) / 2f, y));
                y += previewSize.Height + 18;

                if (!String.IsNullOrWhiteSpace(indexText))
                {
                    context.DrawText(indexText, indexFont, Color.White, new PointF((WheelCanvasSize - indexSize.Width) / 2f, y));
                    y += indexSize.Height + 12;
                }

                if (!String.IsNullOrWhiteSpace(typeText))
                {
                    context.DrawText(typeText, labelFont, Color.Gray, new PointF((WheelCanvasSize - typeSize.Width) / 2f, y));
                }
            });

            image.SaveAsPng(stream);
            return BitmapImage.FromArray(stream.ToArray());
        }

        public void Touch()
        {
            _ = this.ApplyCurrentFontAsync();
        }

        private async Task ApplyCurrentFontAsync()
        {
            var font = this.GetCurrentFont();
            if (font == null)
            {
                return;
            }

            try
            {
                await this._pauseControl.SetFontAsync(font.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseFontScrollAdjustment] apply font '{font.Name}' failed: {ex.Message}");
            }
        }

        private void SelectRelativeFont(Int32 fontStep, Boolean resetAccumulator = true)
        {
            if (fontStep == 0)
            {
                return;
            }

            lock (this._lock)
            {
                if (this._fonts.Count == 0)
                {
                    return;
                }

                if (resetAccumulator)
                {
                    this._scrollAccumulator = 0;
                }

                this._lastFontStepDirection = Math.Sign(fontStep);
                this._lastScrollDirection = this._lastFontStepDirection;
                this._currentIndex = Math.Clamp(this._currentIndex + fontStep, 0, this._fonts.Count - 1);
            }

            this.DisplayChanged?.Invoke();
        }

        private void SetFonts(IReadOnlyList<JonAssetSummary> fonts, String selectedFontId = null)
        {
            lock (this._lock)
            {
                this._fonts = fonts ?? Array.Empty<JonAssetSummary>();
                var selectedIndex = String.IsNullOrWhiteSpace(selectedFontId)
                    ? -1
                    : this._fonts.ToList().FindIndex(font => font.Id.Equals(selectedFontId, StringComparison.OrdinalIgnoreCase));
                this._currentIndex = this._fonts.Count == 0 ? 0 : Math.Clamp(selectedIndex >= 0 ? selectedIndex : this._currentIndex, 0, this._fonts.Count - 1);
                this._scrollAccumulator = 0;
                this._lastFontStepDirection = 0;
                this._lastScrollDirection = 0;
            }

            this.DisplayChanged?.Invoke();
        }

        private JonAssetSummary GetCurrentFont()
        {
            lock (this._lock)
            {
                return this._fonts.Count == 0 ? null : this._fonts[this._currentIndex];
            }
        }

        private (JonAssetSummary Font, Int32 Index, Int32 Count, Boolean IsLoading) GetCurrentFontDisplayData()
        {
            lock (this._lock)
            {
                return (
                    this._fonts.Count == 0 ? null : this._fonts[this._currentIndex],
                    this._currentIndex,
                    this._fonts.Count,
                    this._isLoading);
            }
        }

        private String GetDisplayText()
        {
            lock (this._lock)
            {
                if (this._isLoading)
                {
                    return "Loading\nFonts";
                }

                if (this._fonts.Count == 0)
                {
                    return "No\nFonts";
                }

                var font = this._fonts[this._currentIndex];
                return $"{font.Name}\n{this._currentIndex + 1}/{this._fonts.Count}";
            }
        }

        private static Font CreatePreviewFont(JonAssetSummary font, Single size, FontStyle style)
        {
            var candidates = new[]
            {
                font?.Id,
                font?.Name,
                "Arial"
            };

            foreach (var candidate in candidates)
            {
                if (String.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                try
                {
                    return SystemFonts.CreateFont(candidate, size, style);
                }
                catch
                {
                }
            }

            return SystemFonts.CreateFont("Arial", size, style);
        }

        private static String TrimToWidth(String text, Font font, Single maxWidth)
        {
            var value = String.IsNullOrWhiteSpace(text) ? "" : text.Trim();
            var options = new TextOptions(font);
            if (TextMeasurer.MeasureSize(value, options).Width <= maxWidth)
            {
                return value;
            }

            while (value.Length > 1 && TextMeasurer.MeasureSize($"{value}...", options).Width > maxWidth)
            {
                value = value[..^1];
            }

            return $"{value}...";
        }
    }
}
