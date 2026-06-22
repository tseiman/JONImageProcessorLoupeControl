namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class MaskMorphologyAdjustment : PluginDynamicAdjustment
    {
        private const Int32 DraftHoldSeconds = 10;
        private static readonly String[] DefaultOptions = ["off", "light", "strong"];
        private IReadOnlyList<String> _options = DefaultOptions;
        private String _draftValue = "light";
        private DateTime _lastDraftChangeUtc = DateTime.MinValue;

        public MaskMorphologyAdjustment()
            : base(groupName: "Mask", displayName: "Mask Morphology", description: "Selects segmentation.morphology; press the dial to apply", hasReset: true)
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private JonMaskControl MaskControl => JONImageProcessorLoupeControlPlugin.MaskControl;

        private void OnPluginReady()
        {
            this._draftValue = this.MaskControl.Morphology;
            this.MaskControl.StateChanged += this.OnGatewayStateChanged;
            this.MaskControl.ConnectionChanged += this.OnGatewayConnectionChanged;
            _ = this.LoadOptionsAsync();
            this.Refresh();
        }

        protected override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0 || !this.MaskControl.IsConnected)
            {
                return;
            }

            this.MoveDraft(diff);
        }

        protected override void RunCommand(String actionParameter)
        {
            if (this.MaskControl.IsConnected)
            {
                _ = this.CommitAsync();
            }
        }

        protected override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize) => "Morphology";

        protected override String GetAdjustmentValue(String actionParameter) => Title(this._draftValue);

        protected override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage("Morphology", this.MaskControl.IsConnected, imageSize);

        private void MoveDraft(Int32 diff)
        {
            var options = this._options?.Count > 0 ? this._options : DefaultOptions;
            var index = IndexOfOption(options, this._draftValue);
            if (index < 0)
            {
                index = 0;
            }

            var next = Math.Clamp(index + Math.Sign(diff), 0, options.Count - 1);
            if (next == index)
            {
                return;
            }

            this._draftValue = options[next];
            this._lastDraftChangeUtc = DateTime.UtcNow;
            this.Refresh();
        }

        private async System.Threading.Tasks.Task CommitAsync()
        {
            try
            {
                await this.MaskControl.SetMorphologyAsync(this._draftValue).ConfigureAwait(false);
                this._draftValue = this.MaskControl.Morphology;
                this._lastDraftChangeUtc = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskMorphologyAdjustment] update failed: {ex.Message}");
            }
            finally
            {
                this.Refresh();
            }
        }

        private async System.Threading.Tasks.Task LoadOptionsAsync()
        {
            try
            {
                if (this.MaskControl.IsConnected)
                {
                    this._options = await this.MaskControl.GetMorphologyOptionsAsync().ConfigureAwait(false);
                    this.Refresh();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskMorphologyAdjustment] schema load failed: {ex.Message}");
            }
        }

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (!e.Values.ContainsKey(JonMaskControl.MorphologyKey))
            {
                return;
            }

            if (!this.IsDraftPinned())
            {
                this._draftValue = this.MaskControl.Morphology;
                this._lastDraftChangeUtc = DateTime.MinValue;
            }

            this.Refresh();
        }

        private void OnGatewayConnectionChanged(Boolean connected)
        {
            if (connected)
            {
                this._draftValue = this.MaskControl.Morphology;
                _ = this.LoadOptionsAsync();
            }

            this.Refresh();
        }

        private Boolean IsDraftPinned() =>
            this._lastDraftChangeUtc != DateTime.MinValue
            && (DateTime.UtcNow - this._lastDraftChangeUtc).TotalSeconds < DraftHoldSeconds;

        private void Refresh()
        {
            this.AdjustmentValueChanged("");
            this.ActionImageChanged();
        }

        private static Int32 IndexOfOption(IReadOnlyList<String> options, String value)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Equals(value, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static String Title(String value) =>
            String.IsNullOrWhiteSpace(value) ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
    }
}
