namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    internal sealed class JonPauseControl
    {
        public const String EnabledKey = "pause.enabled";
        public const String ImageKey = "pause.image";
        public const String LoopIfVideoKey = "pause.loopIfVideo";
        public const String ShowStatusTextKey = "pause.showStatusText";
        public const String TextColorKey = "pause.textColor";
        public const String TextPositionKey = "pause.textPosition";
        public const String TextSizeKey = "pause.textSize";
        public const String FontKey = "pause.font";
        public const String FontAlignKey = "pause.fontAlign";
        private static readonly BackgroundRgba DefaultTextColor = new(0, 255, 0, 255);
        private static readonly String[] DefaultBuiltinFonts = ["plain", "simplex", "duplex", "complex", "triplex", "complex-small", "script-simplex", "script-complex"];
        private const String PauseAssetRoot = "pause";
        private const String FontAssetRoot = "fonts";

        private readonly JonGatewayClient _gatewayClient;
        private IReadOnlyList<String> _fontAlignOptions;

        public JonPauseControl(JonGatewayClient gatewayClient)
        {
            this._gatewayClient = gatewayClient;
        }

        public event EventHandler<JonGatewayStateChangedEventArgs> StateChanged
        {
            add => this._gatewayClient.StateChanged += value;
            remove => this._gatewayClient.StateChanged -= value;
        }

        public event Action<Boolean> ConnectionChanged
        {
            add => this._gatewayClient.ConnectionChanged += value;
            remove => this._gatewayClient.ConnectionChanged -= value;
        }

        public Boolean IsConnected => this._gatewayClient.IsConnected;

        public Boolean? Enabled => this._gatewayClient.GetBoolean(EnabledKey);

        public String Image => this._gatewayClient.GetString(ImageKey) ?? "";

        public Boolean? LoopIfVideo => this._gatewayClient.GetBoolean(LoopIfVideoKey);

        public Boolean? ShowStatusText => this._gatewayClient.GetBoolean(ShowStatusTextKey);

        public BackgroundRgba TextColor => BackgroundRgba.FromHex(this._gatewayClient.GetString(TextColorKey), DefaultTextColor);

        public Double TextSize => Math.Clamp(this._gatewayClient.GetNumber(TextSizeKey) ?? 1.0, 0.1, 10.0);

        public (Int32 X, Int32 Y) TextPosition => ParseTextPosition(this._gatewayClient.GetString(TextPositionKey));

        public String Font => this._gatewayClient.GetString(FontKey) ?? "";

        public String FontAlign => NormalizeFontAlign(this._gatewayClient.GetString(FontAlignKey), this._fontAlignOptions);

        public Task SetEnabledAsync(Boolean enabled) => this._gatewayClient.SetValueAsync(EnabledKey, enabled);

        public Task ToggleEnabledAsync() => this.SetEnabledAsync(!(this.Enabled ?? false));

        public Task SetImageAsync(String assetId) => this._gatewayClient.SetValueAsync(ImageKey, assetId ?? "");

        public Task SetLoopIfVideoAsync(Boolean enabled) => this._gatewayClient.SetValueAsync(LoopIfVideoKey, enabled);

        public Task ToggleLoopIfVideoAsync() => this.SetLoopIfVideoAsync(!(this.LoopIfVideo ?? false));

        public Task SetShowStatusTextAsync(Boolean enabled) => this._gatewayClient.SetValueAsync(ShowStatusTextKey, enabled);

        public Task ToggleShowStatusTextAsync() => this.SetShowStatusTextAsync(!(this.ShowStatusText ?? false));

        public Task SetTextColorAsync(BackgroundRgba color) => this._gatewayClient.SetValueAsync(TextColorKey, color.ToHex());

        public Task SetTextSizeAsync(Double value) => this._gatewayClient.SetValueAsync(TextSizeKey, Math.Clamp(Math.Round(value, 2), 0.1, 10.0));

        public Task SetTextPositionAsync(Int32 x, Int32 y) =>
            this._gatewayClient.SetValueAsync(TextPositionKey, $"{Math.Clamp(x, 0, 1920).ToString(CultureInfo.InvariantCulture)}x{Math.Clamp(y, 0, 1080).ToString(CultureInfo.InvariantCulture)}");

        public Task SetFontAsync(String fontId) => this._gatewayClient.SetValueAsync(FontKey, fontId ?? "");

        public Task SetFontAlignAsync(String value) => this._gatewayClient.SetValueAsync(FontAlignKey, NormalizeFontAlign(value, this._fontAlignOptions));

        public async Task<IReadOnlyList<String>> GetFontAlignOptionsAsync()
        {
            if (this._fontAlignOptions?.Count > 0)
            {
                return this._fontAlignOptions;
            }

            this._fontAlignOptions = await this._gatewayClient.GetSchemaEnumOptionsAsync(FontAlignKey, ["left", "center", "right"]).ConfigureAwait(false);
            return this._fontAlignOptions;
        }

        public Task<IReadOnlyList<JonAssetSummary>> ListPauseAssetsAsync() => this.ListAssetsAsync(PauseAssetRoot);

        public async Task<IReadOnlyList<JonAssetSummary>> ListFontsAsync()
        {
            var fonts = new List<JonAssetSummary>();
            foreach (var font in await this.GetBuiltinFontsAsync().ConfigureAwait(false))
            {
                fonts.Add(new JonAssetSummary
                {
                    Id = font,
                    Name = Title(font),
                    Type = "Built-in",
                    Description = "OpenCV Hershey font"
                });
            }

            fonts.AddRange(await this.ListAssetsAsync(FontAssetRoot).ConfigureAwait(false));
            return fonts;
        }

        public async Task<String> DownloadFontAsync(JonAssetSummary font)
        {
            if (font == null || font.Type?.Equals("Built-in", StringComparison.OrdinalIgnoreCase) == true)
            {
                return null;
            }

            var cachePath = GetCachedFontPath(font);
            if (File.Exists(cachePath))
            {
                return cachePath;
            }

            await this._gatewayClient.DownloadApiFileAsync($"/api/files/{FontAssetRoot}/{Uri.EscapeDataString(font.Id)}", cachePath).ConfigureAwait(false);
            return cachePath;
        }

        private async Task<IReadOnlyList<String>> GetBuiltinFontsAsync()
        {
            try
            {
                var schema = await this._gatewayClient.GetApiAsync("/api/schema").ConfigureAwait(false);
                if (schema?["config"]?["api"]?["commands"]?["set"]?["items"]?[FontKey]?["ui"]?["builtinFonts"] is JsonArray array)
                {
                    var fonts = array
                        .Select(item => item?.GetValue<String>())
                        .Where(font => !String.IsNullOrWhiteSpace(font))
                        .ToArray();
                    if (fonts.Length > 0)
                    {
                        return fonts;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[JonPauseControl] builtin font schema load failed: {ex.Message}");
            }

            return DefaultBuiltinFonts;
        }

        private async Task<IReadOnlyList<JonAssetSummary>> ListAssetsAsync(String root)
        {
            var response = await this._gatewayClient.GetApiAsync($"/api/files/{root}").ConfigureAwait(false);
            var assets = new List<JonAssetSummary>();
            if (response?["files"] is not JsonArray array)
            {
                return assets;
            }

            foreach (var item in array.OfType<JsonObject>())
            {
                var id = item["id"]?.GetValue<String>() ?? "";
                if (String.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                assets.Add(new JonAssetSummary
                {
                    Id = id,
                    Name = item["name"]?.GetValue<String>() ?? id,
                    Type = item["type"]?.GetValue<String>() ?? "",
                    Description = item["description"]?.GetValue<String>() ?? "",
                    Mtime = item["mtime"]?.GetValue<String>() ?? ""
                });
            }

            return assets;
        }

        private static String GetCachedFontPath(JonAssetSummary font)
        {
            var cacheRoot = Path.Combine(Path.GetTempPath(), "JONImageProcessorLoupeControl", "fonts");
            var safeId = new String((font.Id ?? "font")
                .Select(ch => Char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '_')
                .ToArray());
            var cacheKey = new String((String.IsNullOrWhiteSpace(font.Mtime) ? "current" : font.Mtime)
                .Select(ch => Char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray());
            return Path.Combine(cacheRoot, $"{safeId}-{cacheKey}.ttf");
        }

        private static (Int32 X, Int32 Y) ParseTextPosition(String value)
        {
            var parts = (value ?? "").Split('x');
            if (parts.Length == 2
                && Int32.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                return (Math.Clamp(x, 0, 1920), Math.Clamp(y, 0, 1080));
            }

            return (0, 0);
        }

        private static String NormalizeFontAlign(String value, IReadOnlyList<String> options)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (options?.Contains(normalized) == true)
            {
                return normalized;
            }

            return normalized switch
            {
                "left" => "left",
                "right" => "right",
                _ => "center"
            };
        }

        private static String Title(String value) =>
            String.IsNullOrWhiteSpace(value) ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.Replace('-', ' '));
    }
}
