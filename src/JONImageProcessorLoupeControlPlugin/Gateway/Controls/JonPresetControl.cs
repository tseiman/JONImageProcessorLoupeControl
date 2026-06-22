namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    internal sealed class JonPresetControl
    {
        private readonly JonGatewayClient _gatewayClient;

        public JonPresetControl(JonGatewayClient gatewayClient)
        {
            this._gatewayClient = gatewayClient;
        }

        public Boolean IsConnected => this._gatewayClient.IsConnected;

        public async Task<IReadOnlyList<JonPresetSummary>> ListPresetsAsync()
        {
            var response = await this._gatewayClient.GetApiAsync("/api/presets").ConfigureAwait(false);
            var presets = new List<JonPresetSummary>();
            if (response?["presets"] is not JsonArray array)
            {
                return presets;
            }

            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var exists = obj["exists"]?.GetValue<Boolean>() ?? true;
                var id = obj["id"]?.GetValue<String>() ?? "";
                if (!exists || String.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var name = obj["name"]?.GetValue<String>();
                presets.Add(new JonPresetSummary
                {
                    Id = id,
                    Name = String.IsNullOrWhiteSpace(name) ? id : name,
                    Exists = true
                });
            }

            return presets;
        }

        public async Task ApplyPresetAsync(JonPresetSummary preset)
        {
            if (preset == null || String.IsNullOrWhiteSpace(preset.Id))
            {
                return;
            }

            var encodedId = Uri.EscapeDataString(preset.Id);
            await this._gatewayClient.PostApiAsync($"/api/presets/{encodedId}/apply").ConfigureAwait(false);
        }
    }
}
