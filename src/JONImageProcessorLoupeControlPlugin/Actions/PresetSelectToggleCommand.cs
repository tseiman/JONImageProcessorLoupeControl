namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel;

    public sealed class PresetSelectToggleCommand : PluginMultistateDynamicCommand, IBlinkenLightsReceiver, IMultiWheelDispatchable, IMultiWheelUploadCompletedHandler
    {
        private readonly BitmapColor _blinkColor = new(0xff, 0xc0, 0x00);
        private Boolean _blinkState = true;
        private Boolean _keepActiveAfterCompletion;
        private MultiWheelDispatch _multiWheelDispatch;
        private MultiWheelFnState _fnState;
        private PresetScrollAdjustment _presetScrollAdjustment;
        private JonPresetControl _presetControl;

        public PresetSelectToggleCommand()
            : base(groupName: "Wheel Select", displayName: "Preset Select ON/OFF", description: "Enables preset selection with the big multi wheel")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;

            this.AddState("OFF", "Preset Select\nOFF", "Preset Select ON");
            this.AddState("ON", "Preset Select\nON", "Preset Select ON");
        }

        private void OnPluginReady()
        {
            (ServiceDirectory.Get(ServiceDirectory.T_BlinkenLightsTimeSource) as BlinkenLightsTimeSource)?.RegisterBlinkenLightReceiver(this);
            this._multiWheelDispatch = ServiceDirectory.Get(ServiceDirectory.T_MultiWheelDispatch) as MultiWheelDispatch;
            this._fnState = ServiceDirectory.Get(ServiceDirectory.T_MultiWheelFnState) as MultiWheelFnState;
            this._presetControl = ServiceDirectory.Get(ServiceDirectory.T_JonPresetControl) as JonPresetControl;
            this._presetScrollAdjustment = ServiceDirectory.Get(ServiceDirectory.T_PresetScrollAdjustment) as PresetScrollAdjustment;
            this._multiWheelDispatch?.RegisterDispatchable(this, this._presetScrollAdjustment);

            if (JONImageProcessorLoupeControlPlugin.GatewayClient != null)
            {
                JONImageProcessorLoupeControlPlugin.GatewayClient.ConnectionChanged += this.OnGatewayConnectionChanged;
            }

            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (this._presetControl?.IsConnected != true)
            {
                this.ActionImageChanged();
                return;
            }

            this.ToggleCurrentState();

            if (this.GetCurrentState().Name.Equals("ON", StringComparison.Ordinal))
            {
                this._keepActiveAfterCompletion = this._fnState?.IsEnabled == true;
                this._blinkState = true;
                _ = this._presetScrollAdjustment?.ReloadPresetsAsync();
                this._multiWheelDispatch?.InformActive(this);
            }
            else
            {
                this._keepActiveAfterCompletion = false;
                this._fnState?.Disable();
                this._multiWheelDispatch?.InformInActive(this);
            }

            this.ActionImageChanged();
        }

        protected override BitmapImage GetCommandImage(String actionParameter, Int32 stateIndex, PluginImageSize imageSize) =>
            this.GetCommandImage(actionParameter, imageSize);

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var connected = this._presetControl?.IsConnected == true;
            var isOn = this.GetCurrentState().Name.Equals("ON", StringComparison.Ordinal);
            var background = !connected
                ? Colors.DisabledBackground
                : isOn && this._keepActiveAfterCompletion
                ? Colors.Red
                : isOn && this._blinkState
                    ? this._blinkColor
                    : BitmapColor.Black;

            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, background);
            ButtonVisuals.DrawText(bitmapBuilder, this.GetCurrentState().DisplayName, connected ? BitmapColor.White : Colors.DisabledText);
            return bitmapBuilder.ToImage();
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            this.GetCurrentState().DisplayName;

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
