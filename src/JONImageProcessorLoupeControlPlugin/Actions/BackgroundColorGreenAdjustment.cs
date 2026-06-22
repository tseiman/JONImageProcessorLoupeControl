namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class BackgroundColorGreenAdjustment : BackgroundColorChannelAdjustmentBase
    {
        public BackgroundColorGreenAdjustment()
            : base(BackgroundColorChannel.Green, "Background Color G", "G")
        {
        }
    }
}
