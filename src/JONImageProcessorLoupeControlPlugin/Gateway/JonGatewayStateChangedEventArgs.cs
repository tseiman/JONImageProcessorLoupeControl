namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway
{
    using System;
    using System.Collections.Generic;

    internal sealed class JonGatewayStateChangedEventArgs : EventArgs
    {
        public JonGatewayStateChangedEventArgs(IReadOnlyDictionary<String, Object> values)
        {
            this.Values = values;
        }

        public IReadOnlyDictionary<String, Object> Values { get; }
    }
}
