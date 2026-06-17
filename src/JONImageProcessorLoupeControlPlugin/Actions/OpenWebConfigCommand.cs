namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;

    using LoupedeckWebConfigLib;

    public sealed class OpenWebConfigCommand : PluginDynamicCommand
    {
        public OpenWebConfigCommand()
            : base(groupName: "Configurations", displayName: "Open Web Config", description: "Opens the JONImageProcessor gateway configuration")
        {
        }

        protected override void RunCommand(String actionParameter)
        {
            LoupedeckWebConfig.ActivateConfig();
        }
    }
}
