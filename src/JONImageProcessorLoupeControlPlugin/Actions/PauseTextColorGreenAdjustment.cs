namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class PauseTextColorGreenAdjustment : PauseTextColorChannelAdjustmentBase
    {
        public PauseTextColorGreenAdjustment()
            : base(BackgroundColorChannel.Green, "Pause Text Color G", "G")
        {
        }
    }
}
