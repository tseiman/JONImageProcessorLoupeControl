namespace Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.SharedState;

    internal sealed class MultiWheelFnState : IDisposable
    {
        private static readonly TimeSpan WatchRetryDelay = TimeSpan.FromSeconds(2);
        private readonly LoupedeckSharedStateClient _client = new();
        private readonly CancellationTokenSource _lifetime = new();
        private Boolean _isDisposed;

        public MultiWheelFnState()
        {
            if (this._client.TryGetMultiWheelKeepActive(out var value))
            {
                this.IsEnabled = value;
            }

            _ = Task.Run(this.WatchAsync);
        }

        public event Action Changed;

        public Boolean IsEnabled { get; private set; }

        public void Dispose()
        {
            if (this._isDisposed)
            {
                return;
            }

            this._isDisposed = true;
            this._lifetime.Cancel();
            this._lifetime.Dispose();
        }

        private async Task WatchAsync()
        {
            while (!this._lifetime.IsCancellationRequested)
            {
                try
                {
                    await foreach (var keepActive in this._client.WatchMultiWheelKeepActiveAsync(this._lifetime.Token).ConfigureAwait(false))
                    {
                        this.SetEnabled(keepActive);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    this.SetEnabled(false);
                    try
                    {
                        await Task.Delay(WatchRetryDelay, this._lifetime.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private void SetEnabled(Boolean enabled)
        {
            if (this.IsEnabled == enabled)
            {
                return;
            }

            this.IsEnabled = enabled;
            this.Changed?.Invoke();
        }
    }
}
