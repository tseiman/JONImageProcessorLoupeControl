namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class BackgroundColorRedAdjustment : BackgroundColorChannelAdjustmentBase
    {
        public BackgroundColorRedAdjustment()
            : base(BackgroundColorChannel.Red, "Background Color R", "R")
        {
        }
    }
}
