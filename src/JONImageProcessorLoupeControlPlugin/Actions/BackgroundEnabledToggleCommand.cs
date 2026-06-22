namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class BackgroundEnabledToggleCommand : PluginDynamicCommand
    {
        public BackgroundEnabledToggleCommand()
            : base(groupName: "Background", displayName: "Background ON/OFF", description: "Toggles runtime.noOverlay through JONImageProcessor-Gateway")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonBackgroundControl BackgroundControl => JONImageProcessorLoupeControlPlugin.BackgroundControl;

        private void OnPluginReady()
        {
            this.BackgroundControl.StateChanged += this.OnGatewayStateChanged;
            this.BackgroundControl.ConnectionChanged += _ => this.ActionImageChanged();
            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.BackgroundControl.IsConnected)
            {
                this.ActionImageChanged();
                return;
            }

            try
            {
                _ = this.BackgroundControl.ToggleBackgroundEnabledAsync();
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundEnabledToggleCommand] toggle failed: {ex.Message}");
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            ToggleButtonImage(this.BackgroundControl.IsConnected, this.BackgroundControl.BackgroundEnabled == true, "Background", imageSize);

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.BackgroundControl.BackgroundEnabled == true ? "Background ON" : "Background OFF";

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonBackgroundControl.NoOverlayKey))
            {
                this.ActionImageChanged();
            }
        }

        internal static BitmapImage ToggleButtonImage(Boolean connected, Boolean enabled, String label, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var background = !connected ? Colors.DisabledBackground : enabled ? Colors.Green : Colors.Red;
            var textColor = connected ? BitmapColor.White : Colors.DisabledText;
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, background);
            ButtonVisuals.DrawText(bitmapBuilder, $"{label}\n{(enabled ? "ON" : "OFF")}", textColor);
            return bitmapBuilder.ToImage();
        }
    }
}
