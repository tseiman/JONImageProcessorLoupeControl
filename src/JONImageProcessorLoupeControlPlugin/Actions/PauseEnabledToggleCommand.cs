namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class PauseEnabledToggleCommand : PluginDynamicCommand
    {
        public PauseEnabledToggleCommand()
            : base(groupName: "Pause", displayName: "Pause ON/OFF", description: "Toggles pause.enabled through JONImageProcessor-Gateway")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonPauseControl PauseControl => JONImageProcessorLoupeControlPlugin.PauseControl;

        private void OnPluginReady()
        {
            this.PauseControl.StateChanged += this.OnGatewayStateChanged;
            this.PauseControl.ConnectionChanged += _ => this.ActionImageChanged();
            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.PauseControl.IsConnected)
            {
                this.ActionImageChanged();
                return;
            }

            try
            {
                _ = this.PauseControl.ToggleEnabledAsync();
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseEnabledToggleCommand] toggle failed: {ex.Message}");
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            BackgroundEnabledToggleCommand.ToggleButtonImage(this.PauseControl.IsConnected, this.PauseControl.Enabled == true, "Pause", imageSize);

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.PauseControl.Enabled == true ? "Pause ON" : "Pause OFF";

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonPauseControl.EnabledKey))
            {
                this.ActionImageChanged();
            }
        }
    }
}
