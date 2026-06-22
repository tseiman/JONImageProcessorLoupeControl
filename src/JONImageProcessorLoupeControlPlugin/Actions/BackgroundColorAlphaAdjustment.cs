namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;

    public sealed class BackgroundColorAlphaAdjustment : BackgroundColorChannelAdjustmentBase
    {
        public BackgroundColorAlphaAdjustment()
            : base(BackgroundColorChannel.Alpha, "Background Color A", "A")
        {
        }
    }
}
