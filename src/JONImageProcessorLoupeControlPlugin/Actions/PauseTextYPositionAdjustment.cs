namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class PauseTextYPositionAdjustment : PluginDynamicAdjustment
    {
        public PauseTextYPositionAdjustment()
            : base(groupName: "Pause Text", displayName: "Pause Text Y", description: "Adjusts pause.textPosition Y through JONImageProcessor-Gateway", hasReset: false)
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
                _ = this.PauseControl.SetTextPositionAsync(position.X, position.Y + (diff * 10));
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseTextYPositionAdjustment] update failed: {ex.Message}");
            }
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => "Text Y";

        protected override String GetAdjustmentValue(String actionParameter) =>
            this.PauseControl.TextPosition.Y.ToString(CultureInfo.InvariantCulture);

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage("Text Y", this.PauseControl.IsConnected, imageSize);

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
