namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class BackgroundColorBlueAdjustment : BackgroundColorChannelAdjustmentBase
    {
        public BackgroundColorBlueAdjustment()
            : base(BackgroundColorChannel.Blue, "Background Color B", "B")
        {
        }
    }
}
