namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls
{
    using System;
    using System.Globalization;

    internal readonly struct BackgroundRgba
    {
        public BackgroundRgba(Int32 r, Int32 g, Int32 b, Int32 a)
        {
            this.R = ClampByte(r);
            this.G = ClampByte(g);
            this.B = ClampByte(b);
            this.A = ClampByte(a);
        }

        public Int32 R { get; }
        public Int32 G { get; }
        public Int32 B { get; }
        public Int32 A { get; }

        public Int32 Brightness => (Int32)((this.R * 0.299) + (this.G * 0.587) + (this.B * 0.114));

        public BackgroundRgba With(Int32? r = null, Int32? g = null, Int32? b = null, Int32? a = null) =>
            new(r ?? this.R, g ?? this.G, b ?? this.B, a ?? this.A);

        public String ToRgbString() => $"{this.R},{this.G},{this.B}";

        public Double AlphaUnit => Math.Clamp(this.A / 255.0, 0.0, 1.0);

        public String ToHex() => $"{this.R:x2}{this.G:x2}{this.B:x2}{this.A:x2}";

        public static BackgroundRgba FromHex(String hex, BackgroundRgba fallback)
        {
            var value = (hex ?? "").Trim();
            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                value = value[1..];
            }

            if (value.Length != 8
                || !Int32.TryParse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
                || !Int32.TryParse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
                || !Int32.TryParse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)
                || !Int32.TryParse(value.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a))
            {
                return fallback;
            }

            return new BackgroundRgba(r, g, b, a);
        }

        public static BackgroundRgba From(String rgb, Double alpha)
        {
            var parts = (rgb ?? "").Split(',');
            var r = parts.Length > 0 && Int32.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedR) ? parsedR : 0;
            var g = parts.Length > 1 && Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedG) ? parsedG : 255;
            var b = parts.Length > 2 && Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedB) ? parsedB : 0;
            return new BackgroundRgba(r, g, b, (Int32)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255.0));
        }

        private static Int32 ClampByte(Int32 value) => Math.Clamp(value, 0, 255);
    }
}
