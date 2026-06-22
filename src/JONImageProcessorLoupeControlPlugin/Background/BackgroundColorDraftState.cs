namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Background
{
    using System;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;

    internal sealed class BackgroundColorDraftState
    {
        private const Int32 DraftHoldSeconds = 10;
        private JonBackgroundControl _backgroundControl;
        private BackgroundRgba _draftColor = new(0, 255, 0, 255);
        private DateTime _lastDraftChangeUtc = DateTime.MinValue;

        public event Action Changed;

        public BackgroundRgba DraftColor => this._draftColor;

        public Boolean IsConnected => this._backgroundControl?.IsConnected == true;

        public void Attach(JonBackgroundControl backgroundControl)
        {
            if (ReferenceEquals(this._backgroundControl, backgroundControl))
            {
                return;
            }

            if (this._backgroundControl != null)
            {
                this._backgroundControl.StateChanged -= this.OnBackgroundStateChanged;
                this._backgroundControl.ConnectionChanged -= this.OnBackgroundConnectionChanged;
            }

            this._backgroundControl = backgroundControl;
            this._draftColor = this._backgroundControl?.OverlayRgba ?? this._draftColor;
            this._lastDraftChangeUtc = DateTime.MinValue;

            if (this._backgroundControl != null)
            {
                this._backgroundControl.StateChanged += this.OnBackgroundStateChanged;
                this._backgroundControl.ConnectionChanged += this.OnBackgroundConnectionChanged;
            }
        }

        public void SetChannel(BackgroundColorChannel channel, Int32 value)
        {
            this._draftColor = channel switch
            {
                BackgroundColorChannel.Red => this._draftColor.With(r: value),
                BackgroundColorChannel.Green => this._draftColor.With(g: value),
                BackgroundColorChannel.Blue => this._draftColor.With(b: value),
                BackgroundColorChannel.Alpha => this._draftColor.With(a: value),
                _ => this._draftColor
            };
            this._lastDraftChangeUtc = DateTime.UtcNow;
            this.Changed?.Invoke();
        }

        public void MoveChannel(BackgroundColorChannel channel, Int32 diff) =>
            this.SetChannel(channel, this.GetChannel(channel) + diff);

        public Int32 GetChannel(BackgroundColorChannel channel) =>
            channel switch
            {
                BackgroundColorChannel.Red => this._draftColor.R,
                BackgroundColorChannel.Green => this._draftColor.G,
                BackgroundColorChannel.Blue => this._draftColor.B,
                BackgroundColorChannel.Alpha => this._draftColor.A,
                _ => 0
            };

        public async System.Threading.Tasks.Task ApplyAsync()
        {
            if (this._backgroundControl?.IsConnected != true)
            {
                return;
            }

            await this._backgroundControl.SetOverlayRgbaAsync(this._draftColor).ConfigureAwait(false);
            this._draftColor = this._backgroundControl.OverlayRgba;
            this._lastDraftChangeUtc = DateTime.MinValue;
            this.Changed?.Invoke();
        }

        private void OnBackgroundStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (!e.Values.ContainsKey(JonBackgroundControl.OverlayColorKey) && !e.Values.ContainsKey(JonBackgroundControl.OverlayAlphaKey))
            {
                return;
            }

            this.SyncFromGatewayIfNotPinned();
        }

        private void OnBackgroundConnectionChanged(Boolean connected)
        {
            if (connected)
            {
                this.SyncFromGatewayIfNotPinned();
            }
            else
            {
                this.Changed?.Invoke();
            }
        }

        private void SyncFromGatewayIfNotPinned()
        {
            if (this.IsDraftPinned())
            {
                return;
            }

            this._draftColor = this._backgroundControl?.OverlayRgba ?? this._draftColor;
            this._lastDraftChangeUtc = DateTime.MinValue;
            this.Changed?.Invoke();
        }

        private Boolean IsDraftPinned() =>
            this._lastDraftChangeUtc != DateTime.MinValue
            && (DateTime.UtcNow - this._lastDraftChangeUtc).TotalSeconds < DraftHoldSeconds;
    }
}
