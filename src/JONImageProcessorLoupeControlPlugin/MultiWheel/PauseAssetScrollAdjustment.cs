namespace Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    internal sealed class PauseAssetScrollAdjustment : IMultiWheelAdjustment, IMultiWheelDisplayable
    {
        private const Int32 MultiWheelScrollTicksPerAsset = 10;
        private readonly JonPauseControl _pauseControl;
        private readonly Object _lock = new();
        private IReadOnlyList<JonAssetSummary> _assets = Array.Empty<JonAssetSummary>();
        private Int32 _currentIndex;
        private Int32 _scrollAccumulator;
        private Int32 _lastAssetStepDirection;
        private Int32 _lastScrollDirection;
        private Boolean _isLoading;

        public PauseAssetScrollAdjustment(JonPauseControl pauseControl)
        {
            this._pauseControl = pauseControl;
        }

        public event Action DisplayChanged;

        public async Task ReloadAssetsAsync()
        {
            if (this._pauseControl == null || !this._pauseControl.IsConnected)
            {
                this.SetAssets(Array.Empty<JonAssetSummary>());
                return;
            }

            lock (this._lock)
            {
                this._isLoading = true;
            }

            this.DisplayChanged?.Invoke();

            try
            {
                this.SetAssets(await this._pauseControl.ListPauseAssetsAsync().ConfigureAwait(false), this._pauseControl.Image);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseAssetScrollAdjustment] asset list failed: {ex.Message}");
                this.SetAssets(Array.Empty<JonAssetSummary>());
            }
            finally
            {
                lock (this._lock)
                {
                    this._isLoading = false;
                }

                this.DisplayChanged?.Invoke();
            }
        }

        public void ApplyAdjustment(Int32 diff)
        {
            IReadOnlyList<JonAssetSummary> assets;
            lock (this._lock)
            {
                assets = this._assets;
            }

            if (assets.Count == 0)
            {
                return;
            }

            var scrollDirection = Math.Sign(diff);
            if (scrollDirection == 0)
            {
                return;
            }

            if (this._lastAssetStepDirection != 0 && scrollDirection == -this._lastAssetStepDirection && this._scrollAccumulator == 0)
            {
                this.SelectRelativeAsset(scrollDirection);
                return;
            }

            if (this._lastScrollDirection != 0 && scrollDirection != this._lastScrollDirection)
            {
                this._scrollAccumulator = 0;
            }

            this._lastScrollDirection = scrollDirection;
            this._scrollAccumulator += diff;
            var assetStep = this._scrollAccumulator / MultiWheelScrollTicksPerAsset;
            if (assetStep == 0)
            {
                return;
            }

            this._scrollAccumulator -= assetStep * MultiWheelScrollTicksPerAsset;
            this.SelectRelativeAsset(assetStep, resetAccumulator: false);
        }

        public void RenderDisplay(BitmapBuilder bitmapBuilder)
        {
            bitmapBuilder.FillRectangle(0, 0, 512, 512, BitmapColor.Black);
            bitmapBuilder.DrawText(this.GetDisplayText(), BitmapColor.White);
        }

        public void Touch()
        {
            _ = this.ApplyCurrentAssetAsync();
        }

        private async Task ApplyCurrentAssetAsync()
        {
            var asset = this.GetCurrentAsset();
            if (asset == null)
            {
                return;
            }

            try
            {
                await this._pauseControl.SetImageAsync(asset.Id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PauseAssetScrollAdjustment] apply asset '{asset.Name}' failed: {ex.Message}");
            }
        }

        private void SelectRelativeAsset(Int32 assetStep, Boolean resetAccumulator = true)
        {
            if (assetStep == 0)
            {
                return;
            }

            lock (this._lock)
            {
                if (this._assets.Count == 0)
                {
                    return;
                }

                if (resetAccumulator)
                {
                    this._scrollAccumulator = 0;
                }

                this._lastAssetStepDirection = Math.Sign(assetStep);
                this._lastScrollDirection = this._lastAssetStepDirection;
                this._currentIndex = Math.Clamp(this._currentIndex + assetStep, 0, this._assets.Count - 1);
            }

            this.DisplayChanged?.Invoke();
        }

        private void SetAssets(IReadOnlyList<JonAssetSummary> assets, String selectedAssetId = null)
        {
            lock (this._lock)
            {
                this._assets = assets ?? Array.Empty<JonAssetSummary>();
                var selectedIndex = String.IsNullOrWhiteSpace(selectedAssetId)
                    ? -1
                    : this._assets.ToList().FindIndex(asset => asset.Id.Equals(selectedAssetId, StringComparison.OrdinalIgnoreCase)
                        || selectedAssetId.StartsWith($"{asset.Id}/", StringComparison.OrdinalIgnoreCase));
                this._currentIndex = this._assets.Count == 0 ? 0 : Math.Clamp(selectedIndex >= 0 ? selectedIndex : this._currentIndex, 0, this._assets.Count - 1);
                this._scrollAccumulator = 0;
                this._lastAssetStepDirection = 0;
                this._lastScrollDirection = 0;
            }

            this.DisplayChanged?.Invoke();
        }

        private JonAssetSummary GetCurrentAsset()
        {
            lock (this._lock)
            {
                return this._assets.Count == 0 ? null : this._assets[this._currentIndex];
            }
        }

        private String GetDisplayText()
        {
            lock (this._lock)
            {
                if (this._isLoading)
                {
                    return "Loading\nPause Assets";
                }

                if (this._assets.Count == 0)
                {
                    return "No\nPause Assets";
                }

                var asset = this._assets[this._currentIndex];
                return $"{asset.Name}\n{this._currentIndex + 1}/{this._assets.Count}";
            }
        }
    }
}
