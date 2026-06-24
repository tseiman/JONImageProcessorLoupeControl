namespace Loupedeck.JONImageProcessorLoupeControlPlugin.Pause
{
    using System;
    using System.Threading;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Background;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    internal sealed class PauseTextColorDraftState
    {
        private const Int32 DraftHoldSeconds = 10;
        private readonly Object _changedLock = new();
        private JonPauseControl _pauseControl;
        private BackgroundRgba _draftColor = new(0, 255, 0, 255);
        private DateTime _lastDraftChangeUtc = DateTime.MinValue;
        private Timer _changedTimer;

        public event Action Changed;

        public BackgroundRgba DraftColor => this._draftColor;

        public Boolean IsConnected => this._pauseControl?.IsConnected == true;

        public void Attach(JonPauseControl pauseControl)
        {
            if (ReferenceEquals(this._pauseControl, pauseControl))
            {
                return;
            }

            if (this._pauseControl != null)
            {
                this._pauseControl.StateChanged -= this.OnPauseStateChanged;
                this._pauseControl.ConnectionChanged -= this.OnPauseConnectionChanged;
            }

            this._pauseControl = pauseControl;
            this._draftColor = this._pauseControl?.TextColor ?? this._draftColor;
            this._lastDraftChangeUtc = DateTime.MinValue;

            if (this._pauseControl != null)
            {
                this._pauseControl.StateChanged += this.OnPauseStateChanged;
                this._pauseControl.ConnectionChanged += this.OnPauseConnectionChanged;
            }
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
            if (this._pauseControl?.IsConnected != true)
            {
                return;
            }

            await this._pauseControl.SetTextColorAsync(this._draftColor).ConfigureAwait(false);
            this._draftColor = this._pauseControl.TextColor;
            this._lastDraftChangeUtc = DateTime.MinValue;
            this.QueueChanged();
        }

        private void SetChannel(BackgroundColorChannel channel, Int32 value)
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
            this.QueueChanged();
        }

        private void OnPauseStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonPauseControl.TextColorKey))
            {
                this.SyncFromGatewayIfNotPinned();
            }
        }

        private void OnPauseConnectionChanged(Boolean connected)
        {
            if (connected)
            {
                this.SyncFromGatewayIfNotPinned();
            }
            else
            {
                this.QueueChanged();
            }
        }

        private void SyncFromGatewayIfNotPinned()
        {
            if (this.IsDraftPinned())
            {
                return;
            }

            this._draftColor = this._pauseControl?.TextColor ?? this._draftColor;
            this._lastDraftChangeUtc = DateTime.MinValue;
            this.QueueChanged();
        }

        private Boolean IsDraftPinned() =>
            this._lastDraftChangeUtc != DateTime.MinValue
            && (DateTime.UtcNow - this._lastDraftChangeUtc).TotalSeconds < DraftHoldSeconds;

        private void QueueChanged()
        {
            lock (this._changedLock)
            {
                this._changedTimer ??= new Timer(_ => this.RaiseChanged(), null, Timeout.Infinite, Timeout.Infinite);
                this._changedTimer.Change(TimeSpan.FromMilliseconds(50), Timeout.InfiniteTimeSpan);
            }
        }

        private void RaiseChanged()
        {
            var handlers = this.Changed;
            if (handlers == null)
            {
                return;
            }

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch (Exception ex)
                {
                    PluginLog.Warning($"[PauseTextColorDraftState] subscriber failed: {ex.Message}");
                }
            }
        }
    }
}
