namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class PauseLoopVideoToggleCommand : PluginDynamicCommand
    {
        public PauseLoopVideoToggleCommand()
            : base(groupName: "Pause", displayName: "Loop Pause Video ON/OFF", description: "Toggles pause.loopIfVideo through JONImageProcessor-Gateway")
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
                _ = this.PauseControl.ToggleLoopIfVideoAsync();
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseLoopVideoToggleCommand] toggle failed: {ex.Message}");
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            BackgroundEnabledToggleCommand.ToggleButtonImage(this.PauseControl.IsConnected, this.PauseControl.LoopIfVideo == true, "Loop Pause", imageSize);

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.PauseControl.LoopIfVideo == true ? "Loop Pause ON" : "Loop Pause OFF";

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonPauseControl.LoopIfVideoKey))
            {
                this.ActionImageChanged();
            }
        }
    }
}
