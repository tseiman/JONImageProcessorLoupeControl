namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class PauseTextColorRedAdjustment : PauseTextColorChannelAdjustmentBase
    {
        public PauseTextColorRedAdjustment()
            : base(BackgroundColorChannel.Red, "Pause Text Color R", "R")
        {
        }
    }
}
