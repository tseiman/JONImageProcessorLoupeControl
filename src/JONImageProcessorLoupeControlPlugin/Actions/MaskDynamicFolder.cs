namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class MaskDynamicFolder : PluginDynamicFolder
    {
        private const String ToggleMaskCommand = "\u200B";
        private const String CommitMorphologyCommand = "commit-morphology";
        private const String NoopThresholdCommand = "noop-threshold";
        private const String NoopSmoothingCommand = "noop-smoothing";
        private const String ThresholdAdjustment = "threshold";
        private const String SmoothingAdjustment = "smoothing";
        private const String MorphologyAdjustment = "morphology";
        private const Double ThresholdStep = 0.001;
        private const Double SmoothingStep = 0.01;
        private const Int32 MorphologyDraftHoldSeconds = 10;
        private static readonly String[] MorphologyOptions = ["off", "light", "strong"];

        private readonly Object _pollLock = new();
        private JonMaskControl _maskControl;
        private String _draftMorphology = "light";
        private DateTime _lastMorphologyDraftChangeUtc = DateTime.MinValue;
        private Timer _folderPollTimer;
        private Boolean _isPollingActive;

        public MaskDynamicFolder()
        {
            this.DisplayName = "Mask";
            this.GroupName = "Mask";
        }

        public override Boolean Load()
        {
            JONImageProcessorLoupeControlPlugin.PluginReady += this.OnPluginReady;
            return true;
        }

        public override Boolean Unload()
        {
            JONImageProcessorLoupeControlPlugin.PluginReady -= this.OnPluginReady;
            this.StopFolderPolling();
            this.DetachMaskControl();
            return true;
        }

        public override Boolean Activate()
        {
            this.AttachMaskControl();
            this.StartFolderPolling();
            return true;
        }

        public override Boolean Deactivate()
        {
            this.StopFolderPolling();
            return true;
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType deviceType) =>
            PluginDynamicFolderNavigation.None;

        public override BitmapImage GetButtonImage(PluginImageSize imageSize)
        {
            this.AttachMaskControl();

            var connected = this._maskControl?.IsConnected == true;
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, connected ? BitmapColor.Black : Colors.DisabledBackground);
            ButtonVisuals.DrawText(bitmapBuilder, "Mask", connected ? BitmapColor.White : Colors.DisabledText);
            return bitmapBuilder.ToImage();
        }

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateCommandName(ToggleMaskCommand),
                PluginDynamicFolder.NavigateUpActionName
            };
        }

        public override IEnumerable<String> GetEncoderRotateActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateAdjustmentName(ThresholdAdjustment),
                this.CreateAdjustmentName(SmoothingAdjustment),
                this.CreateAdjustmentName(MorphologyAdjustment)
            };
        }

        public override IEnumerable<String> GetEncoderPressActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateCommandName(NoopThresholdCommand),
                this.CreateCommandName(NoopSmoothingCommand),
                this.CreateCommandName(CommitMorphologyCommand)
            };
        }

        public override void RunCommand(String actionParameter)
        {
            if (actionParameter == ToggleMaskCommand)
            {
                if (this._maskControl?.IsConnected != true)
                {
                    this.RefreshAllActions();
                    return;
                }

                _ = this.ToggleMaskAsync();
                return;
            }

            if (actionParameter == CommitMorphologyCommand)
            {
                if (this._maskControl?.IsConnected == true)
                {
                    _ = this.CommitMorphologyAsync();
                }
            }
        }

        public override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0)
            {
                return;
            }

            if (actionParameter == MorphologyAdjustment)
            {
                this.MoveDraftMorphology(diff);
                return;
            }

            if (this._maskControl?.IsConnected != true)
            {
                return;
            }

            if (actionParameter == ThresholdAdjustment)
            {
                var next = JonMaskControl.ClampUnit(this._maskControl.Threshold + (diff * ThresholdStep));
                _ = this.SetThresholdAsync(next);
                return;
            }

            if (actionParameter == SmoothingAdjustment)
            {
                var next = JonMaskControl.ClampUnit(this._maskControl.Smoothing + (diff * SmoothingStep));
                _ = this.SetSmoothingAsync(next);
            }
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                ToggleMaskCommand => this._maskControl?.MaskEnabled == true ? "Mask ON" : "Mask OFF",
                CommitMorphologyCommand => $"Set {Title(this._draftMorphology)}",
                _ => null
            };
        }

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == ToggleMaskCommand)
            {
                return CreateFolderToggleImage(this._maskControl, imageSize);
            }

            if (actionParameter == CommitMorphologyCommand)
            {
                using var bitmapBuilder = new BitmapBuilder(imageSize);
                var connected = this._maskControl?.IsConnected == true;
                ButtonVisuals.FillBackground(bitmapBuilder, imageSize, connected ? BitmapColor.Black : Colors.DisabledBackground);
                ButtonVisuals.DrawText(bitmapBuilder, $"Set\n{Title(this._draftMorphology)}", connected ? BitmapColor.White : Colors.DisabledText);
                return bitmapBuilder.ToImage();
            }

            return null;
        }

        public override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                ThresholdAdjustment => "Thresh.",
                SmoothingAdjustment => "Smth.",
                MorphologyAdjustment => "Morph.",
                _ => null
            };
        }

        public override String GetAdjustmentValue(String actionParameter)
        {
            return actionParameter switch
            {
                ThresholdAdjustment => FormatUnitValue(this._maskControl?.Threshold ?? 0),
                SmoothingAdjustment => FormatUnitValue(this._maskControl?.Smoothing ?? 0),
                MorphologyAdjustment => Title(this._draftMorphology),
                _ => null
            };
        }

        public override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var connected = this._maskControl?.IsConnected == true;
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, connected ? BitmapColor.Black : Colors.DisabledBackground);
            ButtonVisuals.DrawText(bitmapBuilder, this.GetAdjustmentDisplayName(actionParameter, imageSize), connected ? BitmapColor.White : Colors.DisabledText);
            return bitmapBuilder.ToImage();
        }

        private async System.Threading.Tasks.Task ToggleMaskAsync()
        {
            try
            {
                await this._maskControl.ToggleMaskEnabledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskDynamicFolder] mask toggle failed: {ex.Message}");
            }
            finally
            {
                this.RefreshAllActions();
            }
        }

        private async System.Threading.Tasks.Task SetThresholdAsync(Double value)
        {
            try
            {
                await this._maskControl.SetThresholdAsync(value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskDynamicFolder] threshold update failed: {ex.Message}");
            }
            finally
            {
                this.AdjustmentValueChanged(ThresholdAdjustment);
            }
        }

        private async System.Threading.Tasks.Task SetSmoothingAsync(Double value)
        {
            try
            {
                await this._maskControl.SetSmoothingAsync(value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskDynamicFolder] smoothing update failed: {ex.Message}");
            }
            finally
            {
                this.AdjustmentValueChanged(SmoothingAdjustment);
            }
        }

        private async System.Threading.Tasks.Task CommitMorphologyAsync()
        {
            try
            {
                await this._maskControl.SetMorphologyAsync(this._draftMorphology).ConfigureAwait(false);
                this._draftMorphology = this._maskControl.Morphology;
                this._lastMorphologyDraftChangeUtc = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[MaskDynamicFolder] morphology update failed: {ex.Message}");
            }
            finally
            {
                this.RefreshAllActions();
            }
        }

        private void MoveDraftMorphology(Int32 diff)
        {
            var currentIndex = Array.IndexOf(MorphologyOptions, JonMaskControl.NormalizeMorphology(this._draftMorphology));
            if (currentIndex < 0)
            {
                currentIndex = 1;
            }

            var nextIndex = Math.Clamp(currentIndex + Math.Sign(diff), 0, MorphologyOptions.Length - 1);
            if (nextIndex == currentIndex)
            {
                return;
            }

            this._draftMorphology = MorphologyOptions[nextIndex];
            this._lastMorphologyDraftChangeUtc = DateTime.UtcNow;
            this.RefreshAllActions();
        }

        private void OnMaskStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonMaskControl.MorphologyKey))
            {
                var gatewayMorphology = this._maskControl?.Morphology ?? this._draftMorphology;
                if (gatewayMorphology.Equals(this._draftMorphology, StringComparison.Ordinal))
                {
                    this._lastMorphologyDraftChangeUtc = DateTime.MinValue;
                }
                else if (!this.IsMorphologyDraftPinned())
                {
                    this._draftMorphology = gatewayMorphology;
                    this._lastMorphologyDraftChangeUtc = DateTime.MinValue;
                }
            }

            if (e.Values.ContainsKey(JonMaskControl.NoMaskKey)
                || e.Values.ContainsKey(JonMaskControl.ThresholdKey)
                || e.Values.ContainsKey(JonMaskControl.SmoothingKey)
                || e.Values.ContainsKey(JonMaskControl.MorphologyKey))
            {
                this.RefreshAllActions();
            }
        }

        private void OnMaskConnectionChanged(Boolean connected)
        {
            this.RefreshAllActions();
        }

        private void RefreshAllActions()
        {
            this.ButtonActionNamesChanged();
            this.EncoderActionNamesChanged();
            this.AdjustmentValueChanged(ThresholdAdjustment);
            this.AdjustmentValueChanged(SmoothingAdjustment);
            this.AdjustmentValueChanged(MorphologyAdjustment);
        }

        private void OnPluginReady()
        {
            this.AttachMaskControl();
        }

        private void AttachMaskControl()
        {
            if (ReferenceEquals(this._maskControl, JONImageProcessorLoupeControlPlugin.MaskControl))
            {
                return;
            }

            this.DetachMaskControl();
            this._maskControl = JONImageProcessorLoupeControlPlugin.MaskControl;
            this._draftMorphology = this._maskControl?.Morphology ?? "light";
            this._lastMorphologyDraftChangeUtc = DateTime.MinValue;
            if (this._maskControl == null)
            {
                return;
            }

            this._maskControl.StateChanged += this.OnMaskStateChanged;
            this._maskControl.ConnectionChanged += this.OnMaskConnectionChanged;
            this.RefreshAllActions();
        }

        private void DetachMaskControl()
        {
            if (this._maskControl == null)
            {
                return;
            }

            this._maskControl.StateChanged -= this.OnMaskStateChanged;
            this._maskControl.ConnectionChanged -= this.OnMaskConnectionChanged;
            this._maskControl = null;
        }

        private void EnsureActiveFolderState()
        {
            this.AttachMaskControl();
            this.StartFolderPolling();
        }

        private void StartFolderPolling()
        {
            lock (this._pollLock)
            {
                if (this._isPollingActive)
                {
                    return;
                }

                this._isPollingActive = true;
                this._folderPollTimer = new Timer(_ => _ = this.RefreshMaskStateAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
            }
        }

        private void StopFolderPolling()
        {
            lock (this._pollLock)
            {
                this._isPollingActive = false;
                this._folderPollTimer?.Dispose();
                this._folderPollTimer = null;
            }
        }

        private async System.Threading.Tasks.Task RefreshMaskStateAsync()
        {
            try
            {
                if (this._maskControl?.IsConnected == true)
                {
                    await this._maskControl.RefreshAsync().ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }

        private Boolean IsMorphologyDraftPinned()
        {
            return this._lastMorphologyDraftChangeUtc != DateTime.MinValue
                && (DateTime.UtcNow - this._lastMorphologyDraftChangeUtc).TotalSeconds < MorphologyDraftHoldSeconds;
        }

        private static String FormatUnitValue(Double value) =>
            value.ToString("0.000", CultureInfo.InvariantCulture);

        private static BitmapImage CreateFolderToggleImage(JonMaskControl maskControl, PluginImageSize imageSize)
        {
            var connected = maskControl?.IsConnected == true;
            var enabled = maskControl?.MaskEnabled == true;
            var background = connected ? BitmapColor.Black : Colors.DisabledBackground;
            var status = !connected
                ? Colors.DisabledBackground
                : enabled ? Colors.Green : Colors.Red;

            using var bitmapBuilder = new BitmapBuilder(imageSize);
            var width = imageSize.GetWidth();
            var height = imageSize.GetHeight();
            var markerHeight = Math.Max(6, height / 4);
            var markerInset = Math.Max(2, width / 8);

            bitmapBuilder.FillRectangle(0, 0, width, height, background);
            bitmapBuilder.FillRectangle(markerInset, 0, width - (markerInset * 2), markerHeight, status);
            return bitmapBuilder.ToImage();
        }

        private static String Title(String value) =>
            String.IsNullOrWhiteSpace(value) ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
    }
}
