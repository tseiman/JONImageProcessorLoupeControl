namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    internal sealed class JonGatewayClient : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly Object _stateLock = new();
        private readonly HttpClient _httpClient = new();
        private Dictionary<String, Object> _values = new(StringComparer.Ordinal);
        private CancellationTokenSource _lifetime = new();
        private Timer _pollTimer;
        private JonGatewayConfiguration _configuration = new();

        public event EventHandler<JonGatewayStateChangedEventArgs> StateChanged;

        public event Action<Boolean> ConnectionChanged;

        public Boolean IsConnected { get; private set; }

        public Boolean? CameraEnabled => this.GetBoolean("camera.enabled");

        public void Start(JonGatewayConfiguration configuration)
        {
            this.ApplyConfiguration(configuration);
            this._pollTimer = new Timer(_ => _ = this.PollAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        public void ApplyConfiguration(JonGatewayConfiguration configuration)
        {
            this._configuration = configuration ?? new JonGatewayConfiguration();
            this._httpClient.BaseAddress = this._configuration.HttpBaseUri;
            this._httpClient.DefaultRequestHeaders.Authorization = String.IsNullOrWhiteSpace(this._configuration.ApiToken)
                ? null
                : new AuthenticationHeaderValue("Bearer", this._configuration.ApiToken);
            this._httpClient.DefaultRequestHeaders.Remove("X-API-Token");
            if (!String.IsNullOrWhiteSpace(this._configuration.ApiToken))
            {
                this._httpClient.DefaultRequestHeaders.Add("X-API-Token", this._configuration.ApiToken);
            }

            this.RestartWebSocket();
            _ = this.PollAsync();
        }

        public async Task SetCameraEnabledAsync(Boolean enabled)
        {
            await this.SendIpcAsync(new JsonObject
            {
                ["cmd"] = "set",
                ["key"] = "camera.enabled",
                ["value"] = enabled
            }).ConfigureAwait(false);

            await this.PollAsync().ConfigureAwait(false);
        }

        public async Task ToggleCameraEnabledAsync()
        {
            var current = this.CameraEnabled ?? false;
            await this.SetCameraEnabledAsync(!current).ConfigureAwait(false);
        }

        private async Task PollAsync()
        {
            try
            {
                var response = await this.SendIpcAsync(new JsonObject { ["cmd"] = "list" }).ConfigureAwait(false);
                this.ApplyState(response);
                this.SetConnected(true);
            }
            catch (Exception ex)
            {
                this.SetConnected(false);
                PluginLog.Warning(ex, "[JonGatewayClient] poll failed");
            }
        }

        private async Task<JsonNode> SendIpcAsync(JsonObject request)
        {
            using var content = new StringContent(request.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
            using var response = await this._httpClient.PostAsync("/api/ipc", content, this._lifetime.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(this._lifetime.Token).ConfigureAwait(false);
            var json = JsonNode.Parse(text);
            if (!response.IsSuccessStatusCode || json?["ok"]?.GetValue<Boolean>() == false)
            {
                var error = json?["error"]?.GetValue<String>() ?? $"HTTP {(Int32)response.StatusCode}";
                throw new InvalidOperationException(error);
            }

            return json;
        }

        private void RestartWebSocket()
        {
            if (this._lifetime.IsCancellationRequested)
            {
                return;
            }

            this._lifetime.Cancel();
            this._lifetime.Dispose();
            this._lifetime = new CancellationTokenSource();
            _ = Task.Run(() => this.RunWebSocketLoopAsync(this._lifetime.Token));
        }

        private async Task RunWebSocketLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var webSocket = new ClientWebSocket();
                    await webSocket.ConnectAsync(this._configuration.WebSocketUri, token).ConfigureAwait(false);
                    this.SetConnected(true);
                    await this.ReceiveWebSocketMessagesAsync(webSocket, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    this.SetConnected(false);
                    PluginLog.Warning(ex, "[JonGatewayClient] websocket failed");
                }

                if (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), token).ConfigureAwait(false);
                }
            }
        }

        private async Task ReceiveWebSocketMessagesAsync(ClientWebSocket webSocket, CancellationToken token)
        {
            var buffer = new Byte[8192];
            var chunks = new List<Byte>();
            while (!token.IsCancellationRequested && webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                chunks.AddRange(buffer.Take(result.Count));
                if (!result.EndOfMessage)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(chunks.ToArray());
                chunks.Clear();
                this.HandleWebSocketMessage(text);
            }
        }

        private void HandleWebSocketMessage(String text)
        {
            try
            {
                var message = JsonNode.Parse(text);
                if (message?["type"]?.GetValue<String>() == "state")
                {
                    this.ApplyState(message["state"]);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "[JonGatewayClient] websocket message ignored");
            }
        }

        private void ApplyState(JsonNode state)
        {
            if (state == null)
            {
                return;
            }

            var flat = new Dictionary<String, Object>(StringComparer.Ordinal);
            FlattenState(state, "", flat);
            if (flat.Count == 0)
            {
                return;
            }

            lock (this._stateLock)
            {
                foreach (var entry in flat)
                {
                    this._values[entry.Key] = entry.Value;
                }
            }

            this.StateChanged?.Invoke(this, new JonGatewayStateChangedEventArgs(flat));
        }

        private static void FlattenState(JsonNode node, String prefix, Dictionary<String, Object> output)
        {
            if (node is JsonObject obj)
            {
                if (String.IsNullOrEmpty(prefix) && obj["values"] is JsonObject values)
                {
                    FlattenState(values, "", output);
                    return;
                }

                foreach (var item in obj)
                {
                    if (item.Key is "ok" or "key" || item.Value == null)
                    {
                        continue;
                    }

                    var nextKey = String.IsNullOrEmpty(prefix) ? item.Key : $"{prefix}.{item.Key}";
                    FlattenState(item.Value, nextKey, output);
                }

                return;
            }

            if (!String.IsNullOrEmpty(prefix))
            {
                output[prefix] = JsonValueToObject(node);
            }
        }

        private static Object JsonValueToObject(JsonNode node)
        {
            if (node is not JsonValue value)
            {
                return node.ToJsonString(JsonOptions);
            }

            if (value.TryGetValue<Boolean>(out var boolean))
            {
                return boolean;
            }

            if (value.TryGetValue<Int64>(out var integer))
            {
                return integer;
            }

            if (value.TryGetValue<Double>(out var number))
            {
                return number;
            }

            if (value.TryGetValue<String>(out var text))
            {
                return text;
            }

            return value.ToJsonString(JsonOptions);
        }

        private Boolean? GetBoolean(String key)
        {
            lock (this._stateLock)
            {
                if (!this._values.TryGetValue(key, out var value))
                {
                    return null;
                }

                return value switch
                {
                    Boolean boolean => boolean,
                    String text when Boolean.TryParse(text, out var parsed) => parsed,
                    String text when Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer) => integer != 0,
                    Int64 integer => integer != 0,
                    Int32 integer => integer != 0,
                    _ => null
                };
            }
        }

        private void SetConnected(Boolean connected)
        {
            if (this.IsConnected == connected)
            {
                return;
            }

            this.IsConnected = connected;
            this.ConnectionChanged?.Invoke(connected);
        }

        public void Dispose()
        {
            this._pollTimer?.Dispose();
            this._lifetime.Cancel();
            this._lifetime.Dispose();
            this._httpClient.Dispose();
        }
    }
}
