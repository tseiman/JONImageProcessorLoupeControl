namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    internal sealed class JonBackgroundControl
    {
        public const String NoOverlayKey = "runtime.noOverlay";
        public const String EffectKey = "background.effect";
        public const String BlurStrengthKey = "background.blurStrength";
        public const String ImageKey = "background.image";
        public const String LoopIfVideoKey = "background.loopIfVideo";
        public const String OverlayColorKey = "background.overlayColor";
        public const String OverlayAlphaKey = "background.overlayAlpha";
        private const String BackgroundAssetRoot = "backgrounds";

        private readonly JonGatewayClient _gatewayClient;
        private IReadOnlyList<String> _effectOptions;

        public JonBackgroundControl(JonGatewayClient gatewayClient)
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

        public Boolean? BackgroundEnabled
        {
            get
            {
                var noOverlay = this._gatewayClient.GetBoolean(NoOverlayKey);
                return noOverlay.HasValue ? !noOverlay.Value : null;
            }
        }

        public String Effect => NormalizeEffect(this._gatewayClient.GetString(EffectKey), this._effectOptions);

        public Int32 BlurStrength => Math.Clamp((Int32)Math.Round(this._gatewayClient.GetNumber(BlurStrengthKey) ?? 1), 1, 100);

        public String Image => this._gatewayClient.GetString(ImageKey) ?? "";

        public Boolean? LoopIfVideo => this._gatewayClient.GetBoolean(LoopIfVideoKey);

        public BackgroundRgba OverlayRgba => BackgroundRgba.From(
            this._gatewayClient.GetString(OverlayColorKey),
            this._gatewayClient.GetNumber(OverlayAlphaKey) ?? 1.0);

        public Task SetBackgroundEnabledAsync(Boolean enabled) => this._gatewayClient.SetValueAsync(NoOverlayKey, !enabled);

        public Task ToggleBackgroundEnabledAsync() => this.SetBackgroundEnabledAsync(!(this.BackgroundEnabled ?? false));

        public Task SetEffectAsync(String effect) => this._gatewayClient.SetValueAsync(EffectKey, NormalizeEffect(effect, this._effectOptions));

        public Task SetBlurStrengthAsync(Int32 value) => this._gatewayClient.SetValueAsync(BlurStrengthKey, Math.Clamp(value, 1, 100));

        public Task SetImageAsync(String assetId) => this._gatewayClient.SetValueAsync(ImageKey, assetId ?? "");

        public Task SetLoopIfVideoAsync(Boolean enabled) => this._gatewayClient.SetValueAsync(LoopIfVideoKey, enabled);

        public Task ToggleLoopIfVideoAsync() => this.SetLoopIfVideoAsync(!(this.LoopIfVideo ?? false));

        public async Task SetOverlayRgbaAsync(BackgroundRgba color)
        {
            await this._gatewayClient.SetValueAsync(OverlayColorKey, color.ToRgbString()).ConfigureAwait(false);
            await this._gatewayClient.SetValueAsync(OverlayAlphaKey, Math.Round(color.AlphaUnit, 4)).ConfigureAwait(false);
        }

        public Task RefreshAsync() => this._gatewayClient.RefreshAsync();

        public async Task<IReadOnlyList<String>> GetEffectOptionsAsync()
        {
            if (this._effectOptions?.Count > 0)
            {
                return this._effectOptions;
            }

            this._effectOptions = await this._gatewayClient.GetSchemaEnumOptionsAsync(EffectKey, ["none", "color", "blur", "image"]).ConfigureAwait(false);
            return this._effectOptions;
        }

        public async Task<IReadOnlyList<JonAssetSummary>> ListBackgroundAssetsAsync()
        {
            var response = await this._gatewayClient.GetApiAsync($"/api/files/{BackgroundAssetRoot}").ConfigureAwait(false);
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
                    Description = item["description"]?.GetValue<String>() ?? ""
                });
            }

            return assets;
        }

        private static String NormalizeEffect(String value, IReadOnlyList<String> options)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (options?.Contains(normalized) == true)
            {
                return normalized;
            }

            return options?.FirstOrDefault() ?? "none";
        }
    }
}
