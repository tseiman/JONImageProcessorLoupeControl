namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel;

    public sealed class PauseAssetSelectToggleCommand : PluginMultistateDynamicCommand, IBlinkenLightsReceiver, IMultiWheelDispatchable, IMultiWheelUploadCompletedHandler
    {
        private Boolean _blinkState = true;
        private Boolean _keepActiveAfterCompletion;
        private MultiWheelDispatch _multiWheelDispatch;
        private MultiWheelFnState _fnState;
        private PauseAssetScrollAdjustment _assetScrollAdjustment;
        private JonPauseControl _pauseControl;

        public PauseAssetSelectToggleCommand()
            : base(groupName: "Wheel Select", displayName: "Pause Asset ON/OFF", description: "Enables pause asset selection with the big multi wheel")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
            this.AddState("OFF", "Pause Asset\nOFF", "Pause Asset ON");
            this.AddState("ON", "Pause Asset\nON", "Pause Asset ON");
        }

        private void OnPluginReady()
        {
            (ServiceDirectory.Get(ServiceDirectory.T_BlinkenLightsTimeSource) as BlinkenLightsTimeSource)?.RegisterBlinkenLightReceiver(this);
            this._multiWheelDispatch = ServiceDirectory.Get(ServiceDirectory.T_MultiWheelDispatch) as MultiWheelDispatch;
            this._fnState = ServiceDirectory.Get(ServiceDirectory.T_MultiWheelFnState) as MultiWheelFnState;
            this._pauseControl = ServiceDirectory.Get(ServiceDirectory.T_JonPauseControl) as JonPauseControl;
            this._assetScrollAdjustment = ServiceDirectory.Get(ServiceDirectory.T_PauseAssetScrollAdjustment) as PauseAssetScrollAdjustment;
            this._multiWheelDispatch?.RegisterDispatchable(this, this._assetScrollAdjustment);
            JONImageProcessorLoupeControlPlugin.GatewayClient.ConnectionChanged += this.OnGatewayConnectionChanged;
            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (this._pauseControl?.IsConnected != true)
            {
                this.ActionImageChanged();
                return;
            }

            this.ToggleCurrentState();
            if (this.GetCurrentState().Name.Equals("ON", StringComparison.Ordinal))
            {
                this._keepActiveAfterCompletion = this._fnState?.IsEnabled == true;
                this._blinkState = true;
                _ = this._assetScrollAdjustment?.ReloadAssetsAsync();
                this._multiWheelDispatch?.InformActive(this);
            }
            else
            {
                this._keepActiveAfterCompletion = false;
                this._multiWheelDispatch?.InformInActive(this);
            }

            this.ActionImageChanged();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, Int32 stateIndex, PluginImageSize imageSize) =>
            this.GetCommandImage(actionParameter, imageSize);

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var connected = this._pauseControl?.IsConnected == true;
            var isOn = this.GetCurrentState().Name.Equals("ON", StringComparison.Ordinal);
            var background = !connected
                ? Colors.DisabledBackground
                : isOn && this._keepActiveAfterCompletion
                    ? Colors.Red
                    : isOn && this._blinkState
                        ? Colors.ORANGE
                        : Colors.Black;

            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, background);
            ButtonVisuals.DrawText(bitmapBuilder, this.GetCurrentState().DisplayName, connected ? BitmapColor.White : Colors.DisabledText);
            return bitmapBuilder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) => this.GetCurrentState().DisplayName;

        protected override String GetCommandDisplayName(String actionParameter, Int32 stateIndex, PluginImageSize imageSize) =>
            this.GetCommandDisplayName(actionParameter, imageSize);

        public void ReceiveTimeThick(Boolean blinkState)
        {
            this._blinkState = blinkState;
            this.ActionImageChanged();
        }

        public void Disengage()
        {
            this._keepActiveAfterCompletion = false;
            this.SetCurrentState(0);
            this.ActionImageChanged();
        }

        public void UploadCompleted()
        {
            if (!this.GetCurrentState().Name.Equals("ON", StringComparison.Ordinal))
            {
                return;
            }

            if (this._keepActiveAfterCompletion)
            {
                this.ActionImageChanged();
                return;
            }

            this.SetCurrentState(0);
            this._multiWheelDispatch?.InformInActive(this);
            this.ActionImageChanged();
        }

        private void OnGatewayConnectionChanged(Boolean connected)
        {
            if (!connected && this.GetCurrentState().Name.Equals("ON", StringComparison.Ordinal))
            {
                this.Disengage();
                this._multiWheelDispatch?.InformInActive(this);
            }

            this.ActionImageChanged();
        }
    }
}
