namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel;

    public sealed class MultiWheelFnToggleCommand : PluginMultistateDynamicCommand
    {
        private MultiWheelFnState _fnState;

        public MultiWheelFnToggleCommand()
            : base(groupName: "Wheel Select", displayName: "MultiWheel Fn", description: "Keeps the next MultiWheel function active after its action completes")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;

            this.AddState("OFF", "MultiWheel Fn\nOFF", "MultiWheel Fn ON");
            this.AddState("ON", "MultiWheel Fn\nON", "MultiWheel Fn ON");
        }

        private void OnPluginReady()
        {
            this._fnState = ServiceDirectory.Get(ServiceDirectory.T_MultiWheelFnState) as MultiWheelFnState;
            if (this._fnState != null)
            {
                this._fnState.Changed += this.SyncStateFromService;
            }

            this.SyncStateFromService();
        }

        protected override void RunCommand(String actionParameter)
        {
            this._fnState?.Toggle();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, Int32 stateIndex, PluginImageSize imageSize) =>
            this.GetCommandImage(actionParameter, imageSize);

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, this._fnState?.IsEnabled == true ? Colors.Red : BitmapColor.Black);
            ButtonVisuals.DrawText(bitmapBuilder, this.GetCurrentState().DisplayName, BitmapColor.White);
            return bitmapBuilder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.GetCurrentState().DisplayName;

        protected override String GetCommandDisplayName(String actionParameter, Int32 stateIndex, PluginImageSize imageSize) =>
            this.GetCommandDisplayName(actionParameter, imageSize);

        private void SyncStateFromService()
        {
            this.SetCurrentState(this._fnState?.IsEnabled == true ? 1 : 0);
            this.ActionImageChanged();
        }
    }
}
