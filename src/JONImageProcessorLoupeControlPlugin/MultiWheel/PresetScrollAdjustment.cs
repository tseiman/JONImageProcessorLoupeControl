namespace Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    internal sealed class PresetScrollAdjustment : IMultiWheelAdjustment, IMultiWheelDisplayable
    {
        private const Int32 MultiWheelScrollTicksPerPreset = 10;
        private readonly JonPresetControl _presetControl;
        private readonly Object _lock = new();
        private IReadOnlyList<JonPresetSummary> _presets = Array.Empty<JonPresetSummary>();
        private Int32 _currentIndex;
        private Int32 _scrollAccumulator;
        private Int32 _lastPresetStepDirection;
        private Int32 _lastScrollDirection;
        private Boolean _isLoading;

        public event Action DisplayChanged;

        public PresetScrollAdjustment(JonPresetControl presetControl)
        {
            this._presetControl = presetControl;
        }

        public async Task ReloadPresetsAsync()
        {
            if (this._presetControl == null || !this._presetControl.IsConnected)
            {
                this.SetPresets(Array.Empty<JonPresetSummary>());
                return;
            }

            lock (this._lock)
            {
                this._isLoading = true;
            }

            this.DisplayChanged?.Invoke();

            try
            {
                this.SetPresets(await this._presetControl.ListPresetsAsync().ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PresetScrollAdjustment] preset list failed: {ex.Message}");
                this.SetPresets(Array.Empty<JonPresetSummary>());
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
            IReadOnlyList<JonPresetSummary> presets;
            lock (this._lock)
            {
                presets = this._presets;
            }

            if (presets.Count == 0)
            {
                return;
            }

            var scrollDirection = Math.Sign(diff);
            if (scrollDirection == 0)
            {
                return;
            }

            if (this._lastPresetStepDirection != 0 &&
                scrollDirection == -this._lastPresetStepDirection &&
                this._scrollAccumulator == 0)
            {
                this.SelectRelativePreset(scrollDirection);
                return;
            }

            if (this._lastScrollDirection != 0 && scrollDirection != this._lastScrollDirection)
            {
                this._scrollAccumulator = 0;
            }

            this._lastScrollDirection = scrollDirection;
            this._scrollAccumulator += diff;
            var presetStep = this._scrollAccumulator / MultiWheelScrollTicksPerPreset;
            if (presetStep == 0)
            {
                return;
            }

            this._scrollAccumulator -= presetStep * MultiWheelScrollTicksPerPreset;
            this.SelectRelativePreset(presetStep, resetAccumulator: false);
        }

        public void RenderDisplay(BitmapBuilder bitmapBuilder)
        {
            bitmapBuilder.FillRectangle(0, 0, 512, 512, BitmapColor.Black);
            var text = this.GetDisplayText();
            bitmapBuilder.DrawText(text, BitmapColor.White);
        }

        public void Touch()
        {
            _ = this.ApplyCurrentPresetAsync();
        }

        private async Task ApplyCurrentPresetAsync()
        {
            var preset = this.GetCurrentPreset();
            if (preset == null)
            {
                return;
            }

            try
            {
                await this._presetControl.ApplyPresetAsync(preset).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[PresetScrollAdjustment] apply preset '{preset.Name}' failed: {ex.Message}");
            }
        }

        private void SelectRelativePreset(Int32 presetStep, Boolean resetAccumulator = true)
        {
            if (presetStep == 0)
            {
                return;
            }

            lock (this._lock)
            {
                if (this._presets.Count == 0)
                {
                    return;
                }

                if (resetAccumulator)
                {
                    this._scrollAccumulator = 0;
                }

                this._lastPresetStepDirection = Math.Sign(presetStep);
                this._lastScrollDirection = this._lastPresetStepDirection;
                this._currentIndex = Math.Clamp(this._currentIndex + presetStep, 0, this._presets.Count - 1);
            }

            this.DisplayChanged?.Invoke();
        }

        private void SetPresets(IReadOnlyList<JonPresetSummary> presets)
        {
            lock (this._lock)
            {
                this._presets = presets ?? Array.Empty<JonPresetSummary>();
                this._currentIndex = this._presets.Count == 0 ? 0 : Math.Clamp(this._currentIndex, 0, this._presets.Count - 1);
                this._scrollAccumulator = 0;
                this._lastPresetStepDirection = 0;
                this._lastScrollDirection = 0;
            }

            this.DisplayChanged?.Invoke();
        }

        private JonPresetSummary GetCurrentPreset()
        {
            lock (this._lock)
            {
                if (this._presets.Count == 0)
                {
                    return null;
                }

                return this._presets[this._currentIndex];
            }
        }

        private String GetDisplayText()
        {
            lock (this._lock)
            {
                if (this._isLoading)
                {
                    return "Loading\nPresets";
                }

                if (this._presets.Count == 0)
                {
                    return "No\nPresets";
                }

                var preset = this._presets[this._currentIndex];
                return $"{preset.Name}\n{this._currentIndex + 1}/{this._presets.Count}";
            }
        }
    }
}
