namespace Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel
{
    using System;

    internal sealed class MultiWheelFnState
    {
        public event Action Changed;

        public Boolean IsEnabled { get; private set; }

        public void Toggle()
        {
            this.IsEnabled = !this.IsEnabled;
            this.Changed?.Invoke();
        }

        public void Disable()
        {
            if (!this.IsEnabled)
            {
                return;
            }

            this.IsEnabled = false;
            this.Changed?.Invoke();
        }
    }
}
