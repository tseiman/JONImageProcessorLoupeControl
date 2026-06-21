namespace Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel
{
    using System;

    internal interface IMultiWheelDisplayable
    {
        event Action DisplayChanged;

        void RenderDisplay(BitmapBuilder bitmapBuilder);

        void Touch();
    }
}
