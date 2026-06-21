namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers
{
    using System;

    internal interface IBlinkenLightsReceiver
    {
        void ReceiveTimeThick(Boolean blinkState);
    }
}
