namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls
{
    using System;
    using System.Threading.Tasks;

    internal sealed class JonMaskControl
    {
        public const String NoMaskKey = "runtime.noMask";
        public const String ThresholdKey = "segmentation.threshold";
        public const String SmoothingKey = "segmentation.smoothing";
        public const String MorphologyKey = "segmentation.morphology";

        private readonly JonGatewayClient _gatewayClient;

        public JonMaskControl(JonGatewayClient gatewayClient)
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

        public Boolean? MaskEnabled
        {
            get
            {
                var noMask = this._gatewayClient.GetBoolean(NoMaskKey);
                return noMask.HasValue ? !noMask.Value : null;
            }
        }

        public Double Threshold => ClampUnit(this._gatewayClient.GetNumber(ThresholdKey) ?? 0.5);

        public Double Smoothing => ClampUnit(this._gatewayClient.GetNumber(SmoothingKey) ?? 0.65);

        public String Morphology => NormalizeMorphology(this._gatewayClient.GetString(MorphologyKey));

        public Task SetMaskEnabledAsync(Boolean enabled) => this._gatewayClient.SetValueAsync(NoMaskKey, !enabled);

        public Task ToggleMaskEnabledAsync()
        {
            var current = this.MaskEnabled ?? false;
            return this.SetMaskEnabledAsync(!current);
        }

        public Task SetThresholdAsync(Double value) => this._gatewayClient.SetValueAsync(ThresholdKey, ClampUnit(value));

        public Task SetSmoothingAsync(Double value) => this._gatewayClient.SetValueAsync(SmoothingKey, ClampUnit(value));

        public Task SetMorphologyAsync(String value) => this._gatewayClient.SetValueAsync(MorphologyKey, NormalizeMorphology(value));

        public static Double ClampUnit(Double value) => Math.Clamp(value, 0.0, 1.0);

        public static String NormalizeMorphology(String value) =>
            value?.Trim().ToLowerInvariant() switch
            {
                "off" => "off",
                "strong" => "strong",
                _ => "light"
            };
    }
}
