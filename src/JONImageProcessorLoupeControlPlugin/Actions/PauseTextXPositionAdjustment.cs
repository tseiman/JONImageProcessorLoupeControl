namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class PauseTextXPositionAdjustment : PluginDynamicAdjustment
    {
        public PauseTextXPositionAdjustment()
            : base(groupName: "Pause Text", displayName: "Pause Text X", description: "Adjusts pause.textPosition X through JONImageProcessor-Gateway", hasReset: false)
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonPauseControl PauseControl => JONImageProcessorLoupeControlPlugin.PauseControl;

        private void OnPluginReady()
        {
            this.PauseControl.StateChanged += this.OnGatewayStateChanged;
            this.PauseControl.ConnectionChanged += _ => this.Refresh();
            this.Refresh();
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0 || !this.PauseControl.IsConnected)
            {
                return;
            }

            try
            {
                var position = this.PauseControl.TextPosition;
                _ = this.PauseControl.SetTextPositionAsync(position.X + (diff * 10), position.Y);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseTextXPositionAdjustment] update failed: {ex.Message}");
            }
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => "Text X";

        protected override String GetAdjustmentValue(String actionParameter) =>
            this.PauseControl.TextPosition.X.ToString(CultureInfo.InvariantCulture);

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage("Text X", this.PauseControl.IsConnected, imageSize);

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonPauseControl.TextPositionKey))
            {
                this.Refresh();
            }
        }

        private void Refresh()
        {
            this.AdjustmentValueChanged("");
            this.ActionImageChanged();
        }
    }
}
