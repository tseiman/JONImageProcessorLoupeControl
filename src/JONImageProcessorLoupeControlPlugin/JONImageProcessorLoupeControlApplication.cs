namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    public class JONImageProcessorLoupeControlApplication : ClientApplication
    {
        public JONImageProcessorLoupeControlApplication()
        {
        }

        protected override String GetProcessName() => "";

        protected override String GetBundleName() => "";

        public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
    }
}
