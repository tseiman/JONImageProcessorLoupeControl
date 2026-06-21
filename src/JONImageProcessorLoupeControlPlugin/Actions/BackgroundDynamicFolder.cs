namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class BackgroundDynamicFolder : PluginDynamicFolder
    {
        private const String EnableCommandPrefix = "background-enable";
        private const String LoopCommandPrefix = "background-loop";
        private const String CommitModeCommand = "commit-mode";
        private const String NoopBlurCommand = "noop-blur";
        private const String ModeAdjustment = "mode";
        private const String BlurAdjustment = "blur";
        private const Int32 ModeDraftHoldSeconds = 10;
        private const Int32 BlurStep = 1;
        private static readonly String[] DefaultModeOptions = ["none", "color", "blur", "image"];

        private readonly Object _pollLock = new();
        private JonBackgroundControl _backgroundControl;
        private IReadOnlyList<String> _modeOptions = DefaultModeOptions;
        private String _draftMode = "none";
        private DateTime _lastModeDraftChangeUtc = DateTime.MinValue;
        private Timer _folderPollTimer;
        private Boolean _isPollingActive;

        public BackgroundDynamicFolder()
        {
            this.DisplayName = "Background";
            this.GroupName = "Background";
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
            this.DetachBackgroundControl();
            return true;
        }

        public override Boolean Activate()
        {
            this.AttachBackgroundControl();
            this.StartFolderPolling();
            _ = this.RefreshModeOptionsAsync();
            return true;
        }

        public override Boolean Deactivate()
        {
            this.StopFolderPolling();
            return true;
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType deviceType) =>
            PluginDynamicFolderNavigation.None;

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateCommandName(this.GetEnableCommandParameter()),
                this.CreateCommandName(this.GetLoopCommandParameter()),
                PluginDynamicFolder.NavigateUpActionName
            };
        }

        public override IEnumerable<String> GetEncoderRotateActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateAdjustmentName(ModeAdjustment),
                this.CreateAdjustmentName(BlurAdjustment)
            };
        }

        public override IEnumerable<String> GetEncoderPressActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateCommandName(CommitModeCommand),
                this.CreateCommandName(NoopBlurCommand)
            };
        }

        public override void RunCommand(String actionParameter)
        {
            if (this._backgroundControl?.IsConnected != true)
            {
                this.RefreshAllActions();
                return;
            }

            if (IsEnableCommand(actionParameter))
            {
                _ = this.ToggleBackgroundEnabledAsync();
                return;
            }

            if (IsLoopCommand(actionParameter))
            {
                _ = this.ToggleLoopIfVideoAsync();
                return;
            }

            if (actionParameter == CommitModeCommand)
            {
                _ = this.CommitModeAsync();
            }
        }

        public override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0)
            {
                return;
            }

            if (actionParameter == ModeAdjustment)
            {
                this.MoveDraftMode(diff);
                return;
            }

            if (this._backgroundControl?.IsConnected != true)
            {
                return;
            }

            if (actionParameter == BlurAdjustment)
            {
                var next = Math.Clamp(this._backgroundControl.BlurStrength + (diff * BlurStep), 1, 100);
                _ = this.SetBlurStrengthAsync(next);
            }
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                _ when IsEnableCommand(actionParameter) => "Enable Background",
                _ when IsLoopCommand(actionParameter) => "Loop Video",
                CommitModeCommand => $"Set {Title(this._draftMode)}",
                _ => null
            };
        }

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            var connected = this._backgroundControl?.IsConnected == true;
            if (IsEnableCommand(actionParameter))
            {
                return DynamicFolderVisuals.CreateToggleImage(connected, this._backgroundControl?.BackgroundEnabled == true, imageSize);
            }

            if (IsLoopCommand(actionParameter))
            {
                return DynamicFolderVisuals.CreateToggleImage(connected, this._backgroundControl?.LoopIfVideo == true, imageSize);
            }

            if (actionParameter == CommitModeCommand)
            {
                return DynamicFolderVisuals.CreateTextImage($"Set\n{Title(this._draftMode)}", connected, imageSize);
            }

            return null;
        }

        public override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                ModeAdjustment => "Mode",
                BlurAdjustment => "Blur",
                _ => null
            };
        }

        public override String GetAdjustmentValue(String actionParameter)
        {
            return actionParameter switch
            {
                ModeAdjustment => Title(this._draftMode),
                BlurAdjustment => this._backgroundControl?.BlurStrength.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
        }

        public override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage(this.GetAdjustmentDisplayName(actionParameter, imageSize), this._backgroundControl?.IsConnected == true, imageSize);

        private async System.Threading.Tasks.Task ToggleBackgroundEnabledAsync()
        {
            try
            {
                await this._backgroundControl.ToggleBackgroundEnabledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundDynamicFolder] enable toggle failed: {ex.Message}");
            }
            finally
            {
                this.RefreshAllActions();
            }
        }

        private async System.Threading.Tasks.Task ToggleLoopIfVideoAsync()
        {
            try
            {
                await this._backgroundControl.ToggleLoopIfVideoAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundDynamicFolder] loop toggle failed: {ex.Message}");
            }
            finally
            {
                this.RefreshAllActions();
            }
        }

        private async System.Threading.Tasks.Task CommitModeAsync()
        {
            try
            {
                await this._backgroundControl.SetEffectAsync(this._draftMode).ConfigureAwait(false);
                this._draftMode = this._backgroundControl.Effect;
                this._lastModeDraftChangeUtc = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundDynamicFolder] mode update failed: {ex.Message}");
            }
            finally
            {
                this.RefreshAllActions();
            }
        }

        private async System.Threading.Tasks.Task SetBlurStrengthAsync(Int32 value)
        {
            try
            {
                await this._backgroundControl.SetBlurStrengthAsync(value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundDynamicFolder] blur update failed: {ex.Message}");
            }
            finally
            {
                this.AdjustmentValueChanged(BlurAdjustment);
            }
        }

        private void MoveDraftMode(Int32 diff)
        {
            var options = this._modeOptions?.Count > 0 ? this._modeOptions : DefaultModeOptions;
            var currentIndex = IndexOfOption(options, this._draftMode);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = Math.Clamp(currentIndex + Math.Sign(diff), 0, options.Count - 1);
            if (nextIndex == currentIndex)
            {
                return;
            }

            this._draftMode = options[nextIndex];
            this._lastModeDraftChangeUtc = DateTime.UtcNow;
            this.RefreshAllActions();
        }

        private void OnBackgroundStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonBackgroundControl.EffectKey))
            {
                var gatewayMode = this._backgroundControl?.Effect ?? this._draftMode;
                if (gatewayMode.Equals(this._draftMode, StringComparison.Ordinal))
                {
                    this._lastModeDraftChangeUtc = DateTime.MinValue;
                }
                else if (!this.IsModeDraftPinned())
                {
                    this._draftMode = gatewayMode;
                    this._lastModeDraftChangeUtc = DateTime.MinValue;
                }
            }

            if (e.Values.ContainsKey(JonBackgroundControl.NoOverlayKey)
                || e.Values.ContainsKey(JonBackgroundControl.EffectKey)
                || e.Values.ContainsKey(JonBackgroundControl.BlurStrengthKey)
                || e.Values.ContainsKey(JonBackgroundControl.ImageKey)
                || e.Values.ContainsKey(JonBackgroundControl.LoopIfVideoKey))
            {
                this.RefreshAllActions();
            }
        }

        private void OnBackgroundConnectionChanged(Boolean connected)
        {
            this.RefreshAllActions();
        }

        private void RefreshAllActions()
        {
            this.ButtonActionNamesChanged();
            this.EncoderActionNamesChanged();
            this.AdjustmentValueChanged(ModeAdjustment);
            this.AdjustmentValueChanged(BlurAdjustment);
        }

        private void OnPluginReady()
        {
            this.AttachBackgroundControl();
        }

        private void AttachBackgroundControl()
        {
            if (ReferenceEquals(this._backgroundControl, JONImageProcessorLoupeControlPlugin.BackgroundControl))
            {
                return;
            }

            this.DetachBackgroundControl();
            this._backgroundControl = JONImageProcessorLoupeControlPlugin.BackgroundControl;
            this._draftMode = this._backgroundControl?.Effect ?? "none";
            this._lastModeDraftChangeUtc = DateTime.MinValue;
            if (this._backgroundControl == null)
            {
                return;
            }

            this._backgroundControl.StateChanged += this.OnBackgroundStateChanged;
            this._backgroundControl.ConnectionChanged += this.OnBackgroundConnectionChanged;
            this.RefreshAllActions();
        }

        private void DetachBackgroundControl()
        {
            if (this._backgroundControl == null)
            {
                return;
            }

            this._backgroundControl.StateChanged -= this.OnBackgroundStateChanged;
            this._backgroundControl.ConnectionChanged -= this.OnBackgroundConnectionChanged;
            this._backgroundControl = null;
        }

        private void EnsureActiveFolderState()
        {
            this.AttachBackgroundControl();
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
                this._folderPollTimer = new Timer(_ => _ = this.RefreshBackgroundStateAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
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

        private async System.Threading.Tasks.Task RefreshBackgroundStateAsync()
        {
            try
            {
                if (this._backgroundControl?.IsConnected == true)
                {
                    await this._backgroundControl.RefreshAsync().ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
            }
        }

        private async System.Threading.Tasks.Task RefreshModeOptionsAsync()
        {
            try
            {
                if (this._backgroundControl?.IsConnected == true)
                {
                    this._modeOptions = await this._backgroundControl.GetEffectOptionsAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundDynamicFolder] mode schema load failed: {ex.Message}");
            }
        }

        private Boolean IsModeDraftPinned() =>
            this._lastModeDraftChangeUtc != DateTime.MinValue
            && (DateTime.UtcNow - this._lastModeDraftChangeUtc).TotalSeconds < ModeDraftHoldSeconds;

        private String GetEnableCommandParameter()
        {
            var state = this._backgroundControl?.IsConnected != true
                ? "disabled"
                : this._backgroundControl.BackgroundEnabled == true ? "on" : "off";
            return $"{EnableCommandPrefix}-{state}";
        }

        private String GetLoopCommandParameter()
        {
            var state = this._backgroundControl?.IsConnected != true
                ? "disabled"
                : this._backgroundControl.LoopIfVideo == true ? "on" : "off";
            return $"{LoopCommandPrefix}-{state}";
        }

        private static Boolean IsEnableCommand(String actionParameter) =>
            actionParameter?.StartsWith(EnableCommandPrefix, StringComparison.Ordinal) == true;

        private static Boolean IsLoopCommand(String actionParameter) =>
            actionParameter?.StartsWith(LoopCommandPrefix, StringComparison.Ordinal) == true;

        private static Int32 IndexOfOption(IReadOnlyList<String> options, String value)
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i].Equals(value, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static String Title(String value) =>
            String.IsNullOrWhiteSpace(value) ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
    }
}
