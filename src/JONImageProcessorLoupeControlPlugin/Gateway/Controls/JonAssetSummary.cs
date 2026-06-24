namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls
{
    using System;

    internal sealed class JonAssetSummary
    {
        public String Id { get; init; }

        public String Name { get; init; }

        public String Type { get; init; }

        public String Description { get; init; }

        public String Mtime { get; init; }

        public String LocalPath { get; set; }
    }
}
