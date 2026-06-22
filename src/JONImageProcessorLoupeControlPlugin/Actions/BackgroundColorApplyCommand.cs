namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class BackgroundColorApplyCommand : PluginDynamicCommand
    {
        public BackgroundColorApplyCommand()
            : base(groupName: "Background Color", displayName: "Apply Color", description: "Applies the drafted RGBA background color through JONImageProcessor-Gateway")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        private BackgroundColorDraftState DraftState => JONImageProcessorLoupeControlPlugin.BackgroundColorDraftState;

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

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => "Apply Color";

        private async System.Threading.Tasks.Task ApplyAsync()
        {
            try
            {
                await this.DraftState.ApplyAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundColorApplyCommand] apply failed: {ex.Message}");
            }
            finally
            {
                this.ActionImageChanged();
            }
        }
    }
}
