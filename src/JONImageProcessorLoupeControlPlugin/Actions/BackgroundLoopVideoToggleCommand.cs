namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class BackgroundLoopVideoToggleCommand : PluginDynamicCommand
    {
        public BackgroundLoopVideoToggleCommand()
            : base(groupName: "Background", displayName: "Loop Video ON/OFF", description: "Toggles background.loopIfVideo through JONImageProcessor-Gateway")
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

            _ = this.ToggleAsync();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            BackgroundEnabledToggleCommand.ToggleButtonImage(this.BackgroundControl.IsConnected, this.BackgroundControl.LoopIfVideo == true, "Loop Video", imageSize);

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.BackgroundControl.LoopIfVideo == true ? "Loop Video ON" : "Loop Video OFF";

        private async System.Threading.Tasks.Task ToggleAsync()
        {
            try
            {
                await this.BackgroundControl.ToggleLoopIfVideoAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundLoopVideoToggleCommand] toggle failed: {ex.Message}");
            }
            finally
            {
                this.ActionImageChanged();
            }
        }

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonBackgroundControl.LoopIfVideoKey))
            {
                this.ActionImageChanged();
            }
        }
    }
}
