namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class PauseTextColorBlueAdjustment : PauseTextColorChannelAdjustmentBase
    {
        public PauseTextColorBlueAdjustment()
            : base(BackgroundColorChannel.Blue, "Pause Text Color B", "B")
        {
        }
    }
}
