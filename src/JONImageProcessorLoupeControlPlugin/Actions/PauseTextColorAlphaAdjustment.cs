namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class PauseTextColorAlphaAdjustment : PauseTextColorChannelAdjustmentBase
    {
        public PauseTextColorAlphaAdjustment()
            : base(BackgroundColorChannel.Alpha, "Pause Text Color A", "A")
        {
        }
    }
}
