namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using LoupedeckWebConfigLib;

    public sealed class OpenWebConfigCommand : PluginDynamicCommand
    {
        public OpenWebConfigCommand()
            : base(groupName: "Configurations", displayName: "Open Web Configuration", description: "Opens the JON Image Processor gateway configuration")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            LoupedeckWebConfig.ActivateConfig();
        }
    }
}
