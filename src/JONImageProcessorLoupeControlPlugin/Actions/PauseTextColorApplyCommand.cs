namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Pause;

    public sealed class PauseTextColorApplyCommand : PluginDynamicCommand
    {
        public PauseTextColorApplyCommand()
            : base(groupName: "Pause Text Color", displayName: "Apply Text Color", description: "Applies the drafted RGBA pause text color through JONImageProcessor-Gateway")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private PauseTextColorDraftState DraftState => JONImageProcessorLoupeControlPlugin.PauseTextColorDraftState;

        private void OnPluginReady()
        {
            this.DraftState.Changed += this.ActionImageChanged;
            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.DraftState.IsConnected)
            {
                this.ActionImageChanged();
                return;
            }

            _ = this.ApplyAsync();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateColorImage(this.DraftState.DraftColor, imageSize, this.DraftState.IsConnected, this.DraftState.DraftColor.ToHex());

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => "Apply Text Color";

        private async System.Threading.Tasks.Task ApplyAsync()
        {
            try
            {
                await this.DraftState.ApplyAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseTextColorApplyCommand] apply failed: {ex.Message}");
            }
            finally
            {
                this.ActionImageChanged();
            }
        }
    }
}
