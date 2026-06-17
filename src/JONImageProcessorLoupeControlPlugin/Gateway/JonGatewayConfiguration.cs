namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway
{
    using System;

    internal sealed class JonGatewayConfiguration
    {
        public const String DefaultGatewayBaseUrl = "http://127.0.0.1:8080";

        public String GatewayBaseUrl { get; set; } = DefaultGatewayBaseUrl;

        public String ApiToken { get; set; } = "";

        public Uri HttpBaseUri => new(this.NormalizedGatewayBaseUrl);

        public Uri WebSocketUri
        {
            get
            {
                var builder = new UriBuilder(this.NormalizedGatewayBaseUrl)
                {
                    Scheme = this.HttpBaseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
                    Path = "/api/ws",
                    Query = $"token={Uri.EscapeDataString(this.ApiToken ?? "")}"
                };
                return builder.Uri;
            }
        }

        public String NormalizedGatewayBaseUrl
        {
            get
            {
                var text = (this.GatewayBaseUrl ?? "").Trim();
                if (String.IsNullOrWhiteSpace(text))
                {
                    text = DefaultGatewayBaseUrl;
                }

                if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    text = $"http://{text}";
                }

                return text.TrimEnd('/');
            }
        }
    }
}
