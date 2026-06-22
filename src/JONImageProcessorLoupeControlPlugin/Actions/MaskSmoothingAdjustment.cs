namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class MaskSmoothingAdjustment : PluginDynamicAdjustment
    {
        public MaskSmoothingAdjustment()
            : base(groupName: "Mask", displayName: "Mask Smoothing", description: "Adjusts segmentation.smoothing through JONImageProcessor-Gateway", hasReset: false)
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonMaskControl MaskControl => JONImageProcessorLoupeControlPlugin.MaskControl;

        private void OnPluginReady()
        {
            this.MaskControl.StateChanged += this.OnGatewayStateChanged;
            this.MaskControl.ConnectionChanged += _ => this.Refresh();
            this.Refresh();
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0 || !this.MaskControl.IsConnected)
            {
                return;
            }

            try
            {
                _ = this.MaskControl.SetSmoothingAsync(JonMaskControl.ClampUnit(this.MaskControl.Smoothing + (diff * 0.01)));
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskSmoothingAdjustment] update failed: {ex.Message}");
            }
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => "Smoothing";

        protected override String GetAdjustmentValue(String actionParameter) =>
            ((Int32)Math.Round(this.MaskControl.Smoothing * 100)).ToString(CultureInfo.InvariantCulture);

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage("Smoothing", this.MaskControl.IsConnected, imageSize);

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonMaskControl.SmoothingKey))
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
