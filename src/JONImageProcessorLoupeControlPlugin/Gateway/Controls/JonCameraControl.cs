namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls
{
    using System;
    using System.Threading.Tasks;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;

    internal sealed class JonCameraControl
    {
        public const String EnabledKey = "camera.enabled";

        private readonly JonGatewayClient _gatewayClient;

        public JonCameraControl(JonGatewayClient gatewayClient)
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

        public Task SetEnabledAsync(Boolean enabled) => this._gatewayClient.SetValueAsync(EnabledKey, enabled);

        public Task ToggleEnabledAsync()
        {
            var current = this.Enabled ?? false;
            return this.SetEnabledAsync(!current);
        }
    }
}
