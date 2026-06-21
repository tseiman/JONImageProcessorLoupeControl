namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Collections.Generic;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel;

    internal static class ServiceDirectory
    {
        public static readonly Type T_BlinkenLightsTimeSource = typeof(BlinkenLightsTimeSource);
        public static readonly Type T_MultiWheelDispatch = typeof(MultiWheelDispatch);
        public static readonly Type T_MultiWheelFnState = typeof(MultiWheelFnState);
        public static readonly Type T_JonPresetControl = typeof(JonPresetControl);
        public static readonly Type T_PresetScrollAdjustment = typeof(PresetScrollAdjustment);

        private static readonly Dictionary<Type, Object> Services = new();

        public static Object Get(Type type) => Services[type];

        public static Boolean TryGet(Type type, out Object service) => Services.TryGetValue(type, out service);

        public static void Register(Object service)
        {
            if (service != null)
            {
                Services[service.GetType()] = service;
            }
        }
    }
}
