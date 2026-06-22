namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class MaskEnabledToggleCommand : PluginDynamicCommand
    {
        public MaskEnabledToggleCommand()
            : base(groupName: "Mask", displayName: "Mask ON/OFF", description: "Toggles runtime.noMask through JONImageProcessor-Gateway")
        {
            this.IsWidget = true;
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
        }

        internal static BitmapImage CreateCommandImage(JonMaskControl maskControl, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var enabled = maskControl?.MaskEnabled == true;
            var background = maskControl?.IsConnected != true
                ? Colors.DisabledBackground
                : enabled ? Colors.Green : Colors.Red;
          /*  ButtonVisuals.FillBackground(bitmapBuilder, imageSize, background);

            var text = enabled ? "Mask\nON" : "Mask\nOFF";
            var textColor = maskControl?.IsConnected == true ? BitmapColor.White : Colors.DisabledText;
            ButtonVisuals.DrawText(bitmapBuilder, text, textColor);

*/
            var text = enabled ? "Mask\nON" : "Mask\nOFF";
            var textColor = maskControl?.IsConnected == true ? BitmapColor.White : Colors.DisabledText;
            bitmapBuilder.FillRectangle(0, 0, imageSize.GetWidth(), imageSize.GetHeight(), background);
            bitmapBuilder.DrawText(text, textColor);
            return bitmapBuilder.ToImage();
        }

//        internal static String CreateDisplayName(JonMaskControl maskControl) =>
  //          maskControl?.MaskEnabled == true ? "Mask ON" : "Mask OFF";

        private JonMaskControl MaskControl => JONImageProcessorLoupeControlPlugin.MaskControl;

        private void OnPluginReady()
        {
            this.MaskControl.StateChanged += this.OnGatewayStateChanged;
            this.MaskControl.ConnectionChanged += _ => this.ActionImageChanged();
            this.ActionImageChanged();
        }

        protected override void RunCommand(String actionParameter)
        {
            if (!this.MaskControl.IsConnected)
            {
                PluginLog.Verbose("[MaskEnabledToggleCommand] ignoring toggle because gateway is disconnected");
                this.ActionImageChanged();
                return;
            }

            try
            {
                _ = this.MaskControl.ToggleMaskEnabledAsync();
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskEnabledToggleCommand] toggle failed: {ex.Message}");
            }
        }

        private void OnGatewayStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonMaskControl.NoMaskKey))
            {
                this.ActionImageChanged();
            }
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            CreateCommandImage(this.MaskControl, imageSize);

protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) {
 return "";   
}

       // protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
        //    CreateDisplayName(this.MaskControl);
    }
}
