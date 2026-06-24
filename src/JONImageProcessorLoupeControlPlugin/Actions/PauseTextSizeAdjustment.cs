namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class PauseTextSizeAdjustment : PluginDynamicAdjustment
    {
        public PauseTextSizeAdjustment()
            : base(groupName: "Pause Text", displayName: "Pause Text Size", description: "Adjusts pause.textSize through JONImageProcessor-Gateway", hasReset: false)
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
                _ = this.PauseControl.SetTextSizeAsync(this.PauseControl.TextSize + (diff * 0.1));
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseTextSizeAdjustment] update failed: {ex.Message}");
            }
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => "Text Size";

        protected override String GetAdjustmentValue(String actionParameter) =>
            this.PauseControl.TextSize.ToString("0.0", CultureInfo.InvariantCulture);

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage("Text Size", this.PauseControl.IsConnected, imageSize);

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonPauseControl.TextSizeKey))
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
