namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class CameraEnabledToggleCommand : PluginDynamicCommand
    {
        public CameraEnabledToggleCommand()
            : base(groupName: "Input", displayName: "Camera ON/OFF", description: "Toggles camera.enabled through JONImageProcessor-Gateway")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonCameraControl CameraControl => JONImageProcessorLoupeControlPlugin.CameraControl;

        private void OnPluginReady()
        {
            this.CameraControl.StateChanged += this.OnGatewayStateChanged;
            this.CameraControl.ConnectionChanged += _ => this.ActionImageChanged();
            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.CameraControl.IsConnected)
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
                await this.CameraControl.ToggleEnabledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[CameraEnabledToggleCommand] toggle failed: {ex.Message}");
            }
            finally
            {
                this.ActionImageChanged();
            }
        }

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonCameraControl.EnabledKey))
            {
                this.ActionImageChanged();
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var enabled = this.CameraControl.Enabled == true;
            var background = !this.CameraControl.IsConnected
                ? Colors.DisabledBackground
                : enabled ? Colors.Green : Colors.Red;
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, background);

            var text = enabled ? "Camera\nON" : "Camera\nOFF";
            var textColor = this.CameraControl.IsConnected ? BitmapColor.White : Colors.DisabledText;
            ButtonVisuals.DrawText(bitmapBuilder, text, textColor);

            return bitmapBuilder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
            => this.CameraControl.Enabled == true ? "Camera ON" : "Camera OFF";
    }
}
