namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class BackgroundBlurStrengthAdjustment : PluginDynamicAdjustment
    {
        public BackgroundBlurStrengthAdjustment()
            : base(groupName: "Background", displayName: "Blur Strength", description: "Adjusts background.blurStrength through JONImageProcessor-Gateway", hasReset: false)
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonBackgroundControl BackgroundControl => JONImageProcessorLoupeControlPlugin.BackgroundControl;

        private void OnPluginReady()
        {
            this.BackgroundControl.StateChanged += this.OnGatewayStateChanged;
            this.BackgroundControl.ConnectionChanged += _ => this.Refresh();
            this.Refresh();
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0 || !this.BackgroundControl.IsConnected)
            {
                return;
            }

            _ = this.SetAsync(Math.Clamp(this.BackgroundControl.BlurStrength + diff, 1, 100));
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => "Blur";

        protected override String GetAdjustmentValue(String actionParameter) =>
            this.BackgroundControl.BlurStrength.ToString(CultureInfo.InvariantCulture);

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage("Blur", this.BackgroundControl.IsConnected, imageSize);

        private async System.Threading.Tasks.Task SetAsync(Int32 value)
        {
            try
            {
                await this.BackgroundControl.SetBlurStrengthAsync(value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundBlurStrengthAdjustment] update failed: {ex.Message}");
            }
            finally
            {
                this.Refresh();
            }
        }

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonBackgroundControl.BlurStrengthKey))
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
