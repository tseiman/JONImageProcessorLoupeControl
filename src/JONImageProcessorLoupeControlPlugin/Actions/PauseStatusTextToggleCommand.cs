namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class PauseStatusTextToggleCommand : PluginDynamicCommand
    {
        public PauseStatusTextToggleCommand()
            : base(groupName: "Pause", displayName: "Status Text ON/OFF", description: "Toggles pause.showStatusText through JONImageProcessor-Gateway")
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
                _ = this.PauseControl.ToggleShowStatusTextAsync();
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseStatusTextToggleCommand] toggle failed: {ex.Message}");
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            BackgroundEnabledToggleCommand.ToggleButtonImage(this.PauseControl.IsConnected, this.PauseControl.ShowStatusText == true, "Status Text", imageSize);

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.PauseControl.ShowStatusText == true ? "Status Text ON" : "Status Text OFF";

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonPauseControl.ShowStatusTextKey))
            {
                this.ActionImageChanged();
            }
        }
    }
}
