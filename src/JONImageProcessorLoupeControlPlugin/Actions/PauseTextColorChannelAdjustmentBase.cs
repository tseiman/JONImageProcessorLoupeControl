namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Pause;

    public abstract class PauseTextColorChannelAdjustmentBase : PluginDynamicAdjustment
    {
        private readonly BackgroundColorChannel _channel;
        private readonly String _shortName;

        protected PauseTextColorChannelAdjustmentBase(BackgroundColorChannel channel, String displayName, String shortName)
            : base(groupName: "Pause Text Color", displayName: displayName, description: $"Adjusts pause text color {shortName}", hasReset: false)
        {
            this._channel = channel;
            this._shortName = shortName;
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private PauseTextColorDraftState DraftState => JONImageProcessorLoupeControlPlugin.PauseTextColorDraftState;

        private void OnPluginReady()
        {
            this.DraftState.Changed += this.Refresh;
            this.Refresh();
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0 || !this.DraftState.IsConnected)
            {
                return;
            }

            this.DraftState.MoveChannel(this._channel, diff);
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => this._shortName;

        protected override String GetAdjustmentValue(String actionParameter) =>
            this.DraftState.GetChannel(this._channel).ToString(CultureInfo.InvariantCulture);

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage(this._shortName, this.DraftState.IsConnected, imageSize);

        private void Refresh()
        {
            this.AdjustmentValueChanged("");
            this.ActionImageChanged();
        }
    }
}
