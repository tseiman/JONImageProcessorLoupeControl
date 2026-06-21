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
        private readonly Object _connectionLock = new();
        private readonly HttpClient _httpClient = new();
        private Dictionary<String, Object> _values = new(StringComparer.Ordinal);
        private CancellationTokenSource _lifetime = new();
        private Timer _pollTimer;
        private JonGatewayConfiguration _configuration = new();
        private Boolean _isStarted;
        private Boolean _hasReportedConnectionState;

        public JonGatewayClient()
        {
            this._httpClient.Timeout = TimeSpan.FromMilliseconds(900);
        }

        public event EventHandler<JonGatewayStateChangedEventArgs> StateChanged;

        public event Action<Boolean> ConnectionChanged;

        public Boolean IsConnected { get; private set; }

        public void Start(JonGatewayConfiguration configuration)
        {
            this._isStarted = true;
            this._configuration = configuration ?? new JonGatewayConfiguration();
            this.RestartWebSocket();
            _ = this.PollAsync();
            this._pollTimer = new Timer(_ => _ = this.PollAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        public void ApplyConfiguration(JonGatewayConfiguration configuration)
        {
            this._configuration = configuration ?? new JonGatewayConfiguration();
            lock (this._connectionLock)
            {
                this._hasReportedConnectionState = false;
            }

            if (this._isStarted)
            {
                this.RestartWebSocket();
                _ = this.PollAsync();
            }
        }

        public async Task SetValueAsync(String key, Object value)
        {
            await this.SendIpcAsync(new JsonObject
            {
                ["cmd"] = "set",
                ["key"] = key,
                ["value"] = JsonSerializer.SerializeToNode(value, JsonOptions)
            }).ConfigureAwait(false);

            this.ApplyState(new JsonObject
            {
                [key] = JsonSerializer.SerializeToNode(value, JsonOptions)
            });

            await this.RefreshAsync().ConfigureAwait(false);
        }

        public Task RefreshAsync()
        {
            return this.PollAsync();
        }

        public Task<JsonNode> GetApiAsync(String path)
        {
            return this.SendApiAsync(HttpMethod.Get, path, null);
        }

        public Task<JsonNode> PostApiAsync(String path, JsonNode content = null)
        {
            return this.SendApiAsync(HttpMethod.Post, path, content);
        }

        public async Task<IReadOnlyList<String>> GetSchemaEnumOptionsAsync(String key, IReadOnlyList<String> fallback)
        {
            if (String.IsNullOrWhiteSpace(key) || !this.IsConnected)
            {
                return fallback;
            }

            var schema = await this.GetApiAsync("/api/schema").ConfigureAwait(false);
            if (schema?["config"]?["api"]?["commands"]?["set"]?["items"]?[key]?["enum"] is not JsonArray array)
            {
                return fallback;
            }

            var result = array
                .Select(item => item?.GetValue<String>())
                .Where(value => !String.IsNullOrWhiteSpace(value))
                .ToArray();
            return result.Length > 0 ? result : fallback;
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
                this.SetConnected(false, DescribeConnectivityFailure(ex));
            }
        }

        private async Task<JsonNode> SendIpcAsync(JsonObject request)
        {
            var requestUri = new Uri(this._configuration.HttpBaseUri, "/api/ipc");
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
            this.ApplyAuthHeaders(httpRequest);
            using var content = new StringContent(request.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
            httpRequest.Content = content;
            using var response = await this._httpClient.SendAsync(httpRequest, this._lifetime.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(this._lifetime.Token).ConfigureAwait(false);
            var json = JsonNode.Parse(text);
            if (!response.IsSuccessStatusCode || json?["ok"]?.GetValue<Boolean>() == false)
            {
                var error = json?["error"]?.GetValue<String>() ?? $"HTTP {(Int32)response.StatusCode}";
                throw new InvalidOperationException(error);
            }

            return json;
        }

        private async Task<JsonNode> SendApiAsync(HttpMethod method, String path, JsonNode content)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("API path must not be empty", nameof(path));
            }

            var requestUri = new Uri(this._configuration.HttpBaseUri, path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}");
            using var httpRequest = new HttpRequestMessage(method, requestUri);
            this.ApplyAuthHeaders(httpRequest);

            if (content != null)
            {
                httpRequest.Content = new StringContent(content.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
            }

            using var response = await this._httpClient.SendAsync(httpRequest, this._lifetime.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(this._lifetime.Token).ConfigureAwait(false);
            var json = String.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
            if (!response.IsSuccessStatusCode || json?["ok"]?.GetValue<Boolean>() == false)
            {
                var error = json?["error"]?.GetValue<String>() ?? $"HTTP {(Int32)response.StatusCode}";
                throw new InvalidOperationException(error);
            }

            return json;
        }

        private void ApplyAuthHeaders(HttpRequestMessage request)
        {
            if (String.IsNullOrWhiteSpace(this._configuration.ApiToken))
            {
                return;
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this._configuration.ApiToken);
            request.Headers.Add("X-API-Token", this._configuration.ApiToken);
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
                catch (Exception)
                {
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

        public Boolean? GetBoolean(String key)
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

        public Double? GetNumber(String key)
        {
            lock (this._stateLock)
            {
                if (!this._values.TryGetValue(key, out var value))
                {
                    return null;
                }

                return value switch
                {
                    Double number => number,
                    Single number => number,
                    Decimal number => (Double)number,
                    Int64 integer => integer,
                    Int32 integer => integer,
                    String text when Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    _ => null
                };
            }
        }

        public String GetString(String key)
        {
            lock (this._stateLock)
            {
                if (!this._values.TryGetValue(key, out var value))
                {
                    return null;
                }

                return value?.ToString();
            }
        }

        private void SetConnected(Boolean connected, String reason = null)
        {
            lock (this._connectionLock)
            {
                if (this._hasReportedConnectionState && this.IsConnected == connected)
                {
                    return;
                }

                this._hasReportedConnectionState = true;
                this.IsConnected = connected;
                if (connected)
                {
                    PluginLog.Info($"[JonGatewayClient] connected to {this._configuration.NormalizedGatewayBaseUrl}");
                }
                else
                {
                    PluginLog.Warning($"[JonGatewayClient] gateway unavailable at {this._configuration.NormalizedGatewayBaseUrl}: {reason ?? "not reachable"}");
                }

                this.ConnectionChanged?.Invoke(connected);
            }
        }

        private static String DescribeConnectivityFailure(Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return "operation canceled";
            }

            var root = ex;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            return root.Message;
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
