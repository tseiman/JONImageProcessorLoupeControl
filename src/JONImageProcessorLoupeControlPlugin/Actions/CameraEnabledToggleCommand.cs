namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class CameraEnabledToggleCommand : PluginDynamicCommand
    {
        public CameraEnabledToggleCommand()
            : base(groupName: "Camera", displayName: "Camera ON/OFF", description: "Toggles camera.enabled through JONImageProcessor-Gateway")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonGatewayClient GatewayClient => JONImageProcessorLoupeControlPlugin.GatewayClient;

        private void OnPluginReady()
        {
            this.GatewayClient.StateChanged += this.OnGatewayStateChanged;
            this.GatewayClient.ConnectionChanged += _ => this.ActionImageChanged();
            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.GatewayClient.IsConnected)
            {
                PluginLog.Verbose("[CameraEnabledToggleCommand] ignoring toggle because gateway is disconnected");
                this.ActionImageChanged();
                return;
            }

            _ = this.ToggleAsync();
        }

        private async System.Threading.Tasks.Task ToggleAsync()
        {
            try
            {
                await this.GatewayClient.ToggleCameraEnabledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "[CameraEnabledToggleCommand] toggle failed");
            }
            finally
            {
                this.ActionImageChanged();
            }
        }

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey("camera.enabled"))
            {
                this.ActionImageChanged();
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var enabled = this.GatewayClient.CameraEnabled == true;
            var background = !this.GatewayClient.IsConnected
                ? Colors.DisabledBackground
                : enabled ? Colors.Green : Colors.Black;
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, background);

            var text = enabled ? "Camera\nON" : "Camera\nOFF";
            var textColor = this.GatewayClient.IsConnected ? BitmapColor.White : Colors.DisabledText;
            ButtonVisuals.DrawText(bitmapBuilder, text, textColor);

            return bitmapBuilder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
            => this.GatewayClient.CameraEnabled == true ? "Camera ON" : "Camera OFF";
    }
}
