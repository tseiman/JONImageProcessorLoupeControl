namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
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
        private readonly Object _setQueueLock = new();
        private readonly Object _stateEventLock = new();
        private readonly HttpClient _httpClient = new();
        private readonly SemaphoreSlim _gatewayRequestLock = new(1, 1);
        private Dictionary<String, Object> _values = new(StringComparer.Ordinal);
        private Dictionary<String, IReadOnlyList<String>> _schemaEnumCache = new(StringComparer.Ordinal);
        private Dictionary<String, PendingSetRequest> _pendingSetRequests = new(StringComparer.Ordinal);
        private readonly Dictionary<String, Object> _pendingStateEvents = new(StringComparer.Ordinal);
        private JsonNode _schemaCache;
        private CancellationTokenSource _lifetime = new();
        private Timer _pollTimer;
        private Timer _stateEventTimer;
        private JonGatewayConfiguration _configuration = new();
        private Boolean _isStarted;
        private Boolean _hasReportedConnectionState;
        private Boolean _setWorkerRunning;

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
            lock (this._stateLock)
            {
                this._schemaEnumCache.Clear();
                this._schemaCache = null;
            }

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

        public Task SetValueAsync(String key, Object value)
        {
            this.ApplyState(new JsonObject
            {
                [key] = JsonSerializer.SerializeToNode(value, JsonOptions)
            });

            this.QueueSetValue(key, value);
            return Task.CompletedTask;
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

        public async Task DownloadApiFileAsync(String path, String targetPath)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("API path must not be empty", nameof(path));
            }

            if (String.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Target path must not be empty", nameof(targetPath));
            }

            await this._gatewayRequestLock.WaitAsync(this._lifetime.Token).ConfigureAwait(false);
            try
            {
                var requestUri = new Uri(this._configuration.HttpBaseUri, path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}");
                using var httpRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);
                this.ApplyAuthHeaders(httpRequest);
                using var response = await this._httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, this._lifetime.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"HTTP {(Int32)response.StatusCode}");
                }

                var directory = Path.GetDirectoryName(targetPath);
                if (!String.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await using var responseStream = await response.Content.ReadAsStreamAsync(this._lifetime.Token).ConfigureAwait(false);
                await using var fileStream = File.Create(targetPath);
                await responseStream.CopyToAsync(fileStream, this._lifetime.Token).ConfigureAwait(false);
            }
            finally
            {
                this._gatewayRequestLock.Release();
            }
        }

        public async Task<IReadOnlyList<String>> GetSchemaEnumOptionsAsync(String key, IReadOnlyList<String> fallback)
        {
            if (String.IsNullOrWhiteSpace(key) || !this.IsConnected)
            {
                return fallback;
            }

            lock (this._stateLock)
            {
                if (this._schemaEnumCache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            JsonNode schema;
            lock (this._stateLock)
            {
                schema = this._schemaCache;
            }

            if (schema == null)
            {
                schema = await this.GetApiAsync("/api/schema").ConfigureAwait(false);
                lock (this._stateLock)
                {
                    this._schemaCache = schema;
                }
            }

            var options = ExtractSchemaEnumOptions(schema, key, fallback);
            lock (this._stateLock)
            {
                this._schemaEnumCache[key] = options;
            }

            return options;
        }

        private static IReadOnlyList<String> ExtractSchemaEnumOptions(JsonNode schema, String key, IReadOnlyList<String> fallback)
        {
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
            if (!await this._gatewayRequestLock.WaitAsync(0).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                var response = await this.SendIpcCoreAsync(new JsonObject { ["cmd"] = "list" }).ConfigureAwait(false);
                this.ApplyState(response);
                this.SetConnected(true);
            }
            catch (Exception ex)
            {
                this.SetConnected(false, DescribeConnectivityFailure(ex));
            }
            finally
            {
                this._gatewayRequestLock.Release();
            }
        }

        private async Task<JsonNode> SendIpcAsync(JsonObject request)
        {
            return await this.EnqueueGatewayRequestAsync(() => this.SendIpcCoreAsync(request)).ConfigureAwait(false);
        }

        private async Task<JsonNode> SendIpcCoreAsync(JsonObject request)
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

        private void QueueSetValue(String key, Object value)
        {
            lock (this._setQueueLock)
            {
                this._pendingSetRequests[key] = new PendingSetRequest(key, value);

                if (!this._setWorkerRunning)
                {
                    this._setWorkerRunning = true;
                    _ = Task.Run(this.ProcessSetQueueAsync);
                }
            }
        }

        private async Task ProcessSetQueueAsync()
        {
            while (true)
            {
                KeyValuePair<String, PendingSetRequest>[] pending;
                lock (this._setQueueLock)
                {
                    if (this._pendingSetRequests.Count == 0)
                    {
                        this._setWorkerRunning = false;
                        return;
                    }

                    pending = this._pendingSetRequests.ToArray();
                    this._pendingSetRequests.Clear();
                }

                foreach (var entry in pending)
                {
                    var request = entry.Value;
                    try
                    {
                        await this.EnqueueGatewayRequestAsync(() => this.SendIpcCoreAsync(new JsonObject
                        {
                            ["cmd"] = "set",
                            ["key"] = request.Key,
                            ["value"] = JsonSerializer.SerializeToNode(request.Value, JsonOptions)
                        })).ConfigureAwait(false);
                        this.SetConnected(true);
                    }
                    catch (Exception ex)
                    {
                        this.ReportSetFailure(ex);
                    }
                }
            }
        }

        private async Task<JsonNode> SendApiAsync(HttpMethod method, String path, JsonNode content)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("API path must not be empty", nameof(path));
            }

            return await this.EnqueueGatewayRequestAsync(async () =>
            {
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
            }).ConfigureAwait(false);
        }

        private async Task<JsonNode> EnqueueGatewayRequestAsync(Func<Task<JsonNode>> request)
        {
            await this._gatewayRequestLock.WaitAsync(this._lifetime.Token).ConfigureAwait(false);
            try
            {
                return await request().ConfigureAwait(false);
            }
            finally
            {
                this._gatewayRequestLock.Release();
            }
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

            this.QueueStateChanged(flat);
        }

        private void QueueStateChanged(IReadOnlyDictionary<String, Object> values)
        {
            lock (this._stateEventLock)
            {
                foreach (var entry in values)
                {
                    this._pendingStateEvents[entry.Key] = entry.Value;
                }

                this._stateEventTimer ??= new Timer(_ => this.FlushStateChanged(), null, Timeout.Infinite, Timeout.Infinite);
                this._stateEventTimer.Change(TimeSpan.FromMilliseconds(50), Timeout.InfiniteTimeSpan);
            }
        }

        private void FlushStateChanged()
        {
            Dictionary<String, Object> values;
            lock (this._stateEventLock)
            {
                if (this._pendingStateEvents.Count == 0)
                {
                    return;
                }

                values = new Dictionary<String, Object>(this._pendingStateEvents, StringComparer.Ordinal);
                this._pendingStateEvents.Clear();
            }

            this.RaiseStateChanged(new JonGatewayStateChangedEventArgs(values));
        }

        private void RaiseStateChanged(JonGatewayStateChangedEventArgs args)
        {
            var handlers = this.StateChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (EventHandler<JonGatewayStateChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    PluginLog.Warning($"[JonGatewayClient] state subscriber failed: {ex.Message}");
                }
            }
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
            var changed = false;
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

                changed = true;
            }

            if (changed)
            {
                this.RaiseConnectionChanged(connected);
            }
        }

        private void RaiseConnectionChanged(Boolean connected)
        {
            var handlers = this.ConnectionChanged;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<Boolean> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(connected);
                }
                catch (Exception ex)
                {
                    PluginLog.Warning($"[JonGatewayClient] connection subscriber failed: {ex.Message}");
                }
            }
        }

        private void ReportSetFailure(Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                return;
            }

            PluginLog.Warning($"[JonGatewayClient] gateway set failed: {DescribeConnectivityFailure(ex)}");
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
            this._stateEventTimer?.Dispose();
            this._lifetime.Cancel();
            this._lifetime.Dispose();
            this._gatewayRequestLock.Dispose();
            this._httpClient.Dispose();
        }

        private sealed class PendingSetRequest
        {
            public PendingSetRequest(String key, Object value)
            {
                this.Key = key;
                this.Value = value;
            }

            public String Key { get; }

            public Object Value { get; }
        }
    }
}
