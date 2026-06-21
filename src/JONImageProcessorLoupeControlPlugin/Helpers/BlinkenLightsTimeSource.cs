namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Timers;

    internal sealed class BlinkenLightsTimeSource : IDisposable
    {
        private readonly List<IBlinkenLightsReceiver> _receivers = new();
        private readonly Timer _timer = new(1000);
        private Boolean _blinkState;

        public BlinkenLightsTimeSource()
        {
            this._timer.Elapsed += this.OnTimedEvent;
            this._timer.AutoReset = true;
            this._timer.Enabled = true;
        }

        public void RegisterBlinkenLightReceiver(IBlinkenLightsReceiver receiver)
        {
            if (receiver != null && !this._receivers.Contains(receiver))
            {
                this._receivers.Add(receiver);
            }
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            this._blinkState = !this._blinkState;
            foreach (var receiver in this._receivers.ToArray())
            {
                receiver.ReceiveTimeThick(this._blinkState);
            }
        }

        public void Dispose()
        {
            this._timer.Dispose();
        }
    }
}
