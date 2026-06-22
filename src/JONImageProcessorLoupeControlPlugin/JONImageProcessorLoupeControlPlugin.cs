namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Reflection;
    using System.Text.Json.Nodes;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel;
    using LoupedeckWebConfigLib;

    public class JONImageProcessorLoupeControlPlugin : Plugin
    {
        private const String WebConfigSettingName = "WebConfigJson";
        private const String WebConfigPluginKey = "jon-image-processor-settings";
        private Boolean _isApplyingWebConfig;

        public override Boolean UsesApplicationApiOnly => true;

        public override Boolean HasNoApplication => true;

        internal static JonGatewayClient GatewayClient { get; private set; } = new();

        internal static JonCameraControl CameraControl { get; private set; } = new(GatewayClient);

        internal static JonPresetControl PresetControl { get; private set; } = new(GatewayClient);

        internal static JonMaskControl MaskControl { get; private set; } = new(GatewayClient);

        internal static JonBackgroundControl BackgroundControl { get; private set; } = new(GatewayClient);

        internal static BackgroundColorDraftState BackgroundColorDraftState { get; private set; } = new();

        internal static event Action PluginReady;

        public JONImageProcessorLoupeControlPlugin()
        {
            PluginLog.Init(this.Log);
            PluginLog.Info($"[JONImageProcessorLoupeControlPlugin] Starting git {GetGitVersion()} ({GetBuildConfiguration()})");
            PluginResources.Init(this.Assembly);
        }

        public override void Load()
        {
            GatewayClient = new JonGatewayClient();
            CameraControl = new JonCameraControl(GatewayClient);
            PresetControl = new JonPresetControl(GatewayClient);
            MaskControl = new JonMaskControl(GatewayClient);
            BackgroundControl = new JonBackgroundControl(GatewayClient);
            BackgroundColorDraftState = new BackgroundColorDraftState();
            this.RegisterServices();
            var configuration = this.LoadGatewayConfiguration();
            this.ConfigureWebConfig(configuration);
            GatewayClient.Start(configuration);
            PluginReady?.Invoke();
        }

        public override void Unload()
        {
            LoupedeckWebConfig.DeactivateConfig();
            if (ServiceDirectory.TryGet(ServiceDirectory.T_BlinkenLightsTimeSource, out var blinkenLightsTimeSource)
                && blinkenLightsTimeSource is IDisposable disposableBlinkenLightsTimeSource)
            {
                disposableBlinkenLightsTimeSource.Dispose();
            }

            GatewayClient.Dispose();
        }

        private void RegisterServices()
        {
            ServiceDirectory.Register(new BlinkenLightsTimeSource());
            ServiceDirectory.Register(new MultiWheelFnState());
            ServiceDirectory.Register(new MultiWheelDispatch());
            ServiceDirectory.Register(PresetControl);
            ServiceDirectory.Register(MaskControl);
            ServiceDirectory.Register(BackgroundControl);
            ServiceDirectory.Register(BackgroundColorDraftState);
            ServiceDirectory.Register(new PresetScrollAdjustment(PresetControl));
            ServiceDirectory.Register(new BackgroundAssetScrollAdjustment(BackgroundControl));
            BackgroundColorDraftState.Attach(BackgroundControl);
        }

        private void ConfigureWebConfig(JonGatewayConfiguration configuration)
        {
            LoupedeckWebConfig.Configure(new LoupedeckWebConfigOptions
            {
                ConfigStore = new DelegateLoupedeckConfigStore(
                    load: () => this.TryGetPluginSetting(WebConfigSettingName, out var json) ? json : null,
                    save: json => this.SetPluginSetting(WebConfigSettingName, json, false)),
                OpenBrowser = true,
                LogLifecycleMessages = true,
                Log = LoupedeckWebConfigLog.FromDelegates(
                    verbose: message => PluginLog.Verbose(message),
                    info: message => PluginLog.Info(message),
                    warning: message => PluginLog.Warning(message),
                    error: message => PluginLog.Error(message),
                    verboseException: (exception, message) => PluginLog.Verbose(exception, message),
                    infoException: (exception, message) => PluginLog.Info(exception, message),
                    warningException: (exception, message) => PluginLog.Warning(exception, message),
                    errorException: (exception, message) => PluginLog.Error(exception, message))
            });

            LoupedeckWebConfig.RegisterPlugin(new LoupedeckPluginRegistration(
                PluginId: "jon-image-processor-loupe-control",
                Title: "JON Image Processor",
                Heading: "JON Image Processor Gateway",
                Parameters:
                [
                    new ConfigParameterDefinition("gatewayBaseUrl", ConfigParameterType.String, "Gateway URL or host", JonGatewayConfiguration.DefaultGatewayBaseUrl, Required: true),
                    new ConfigParameterDefinition("apiToken", ConfigParameterType.String, "Gateway API token", "", Required: true)
                ],
                HtmlSnippet: EmbeddedTextResource.Load<JONImageProcessorLoupeControlPlugin>("Resources.PluginSettings.html"),
                ConfigurationKey: WebConfigPluginKey), this.OnWebPluginConfigurationUpdated);

            LoupedeckWebConfig.UpdatePluginConfiguration(this.CreateWebPluginConfiguration(configuration));
        }

        private JonGatewayConfiguration LoadGatewayConfiguration()
        {
            return new JonGatewayConfiguration
            {
                GatewayBaseUrl = this.TryGetPluginSetting("GatewayBaseUrl", out var gatewayBaseUrl) && !String.IsNullOrWhiteSpace(gatewayBaseUrl)
                    ? gatewayBaseUrl
                    : JonGatewayConfiguration.DefaultGatewayBaseUrl,
                ApiToken = this.TryGetPluginSetting("ApiToken", out var apiToken) ? apiToken : ""
            };
        }

        private JsonObject CreateWebPluginConfiguration(JonGatewayConfiguration configuration) => new()
        {
            ["gatewayBaseUrl"] = configuration?.GatewayBaseUrl ?? JonGatewayConfiguration.DefaultGatewayBaseUrl,
            ["apiToken"] = configuration?.ApiToken ?? ""
        };

        private void OnWebPluginConfigurationUpdated(JsonNode configuration)
        {
            if (configuration == null)
            {
                return;
            }

            var next = new JonGatewayConfiguration
            {
                GatewayBaseUrl = configuration["gatewayBaseUrl"]?.GetValue<String>() ?? JonGatewayConfiguration.DefaultGatewayBaseUrl,
                ApiToken = configuration["apiToken"]?.GetValue<String>() ?? ""
            };

            this._isApplyingWebConfig = true;
            try
            {
                this.ApplyGatewayConfiguration(next);
            }
            finally
            {
                this._isApplyingWebConfig = false;
            }
        }

        private void ApplyGatewayConfiguration(JonGatewayConfiguration configuration)
        {
            this.SetPluginSetting("GatewayBaseUrl", configuration.GatewayBaseUrl, false);
            this.SetPluginSetting("ApiToken", configuration.ApiToken, false);
            GatewayClient.ApplyConfiguration(configuration);

            if (!this._isApplyingWebConfig)
            {
                LoupedeckWebConfig.UpdatePluginConfiguration(this.CreateWebPluginConfiguration(configuration));
            }
        }

        private static String GetGitVersion()
        {
            var informationalVersion = typeof(JONImageProcessorLoupeControlPlugin).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "";

            var plusIndex = informationalVersion.LastIndexOf('+');
            var gitVersion = plusIndex >= 0 && plusIndex < informationalVersion.Length - 1
                ? informationalVersion[(plusIndex + 1)..]
                : informationalVersion;

            return gitVersion.Length > 7 ? gitVersion[..7] : gitVersion;
        }

        private static String GetBuildConfiguration()
        {
            return typeof(JONImageProcessorLoupeControlPlugin).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()
                ?.Configuration ?? "unknown";
        }
    }
}
