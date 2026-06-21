namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class BackgroundColorDynamicFolder : PluginDynamicFolder
    {
        private const String CommitColorCommand = "commit-color";
        private const String RedAdjustment = "red";
        private const String GreenAdjustment = "green";
        private const String BlueAdjustment = "blue";
        private const String AlphaAdjustment = "alpha";
        private const Int32 ColorStep = 1;

        private readonly Object _pollLock = new();
        private JonBackgroundControl _backgroundControl;
        private BackgroundRgba _draftColor = new(0, 255, 0, 255);
        private Timer _folderPollTimer;
        private Boolean _isPollingActive;

        public BackgroundColorDynamicFolder()
        {
            this.DisplayName = "Background Color";
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
            this.AttachBackgroundControl();
            return DynamicFolderVisuals.CreateColorImage(this._backgroundControl?.OverlayRgba ?? this._draftColor, imageSize, this._backgroundControl?.IsConnected == true, "Color");
        }

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateCommandName(CommitColorCommand),
                PluginDynamicFolder.NavigateUpActionName
            };
        }

        public override IEnumerable<String> GetEncoderRotateActionNames(DeviceType deviceType)
        {
            this.EnsureActiveFolderState();
            return new[]
            {
                this.CreateAdjustmentName(RedAdjustment),
                this.CreateAdjustmentName(GreenAdjustment),
                this.CreateAdjustmentName(BlueAdjustment),
                this.CreateAdjustmentName(AlphaAdjustment)
            };
        }

        public override void RunCommand(String actionParameter)
        {
            if (actionParameter == CommitColorCommand && this._backgroundControl?.IsConnected == true)
            {
                _ = this.CommitColorAsync();
            }
        }

        public override void ApplyAdjustment(String actionParameter, Int32 diff)
        {
            if (diff == 0)
            {
                return;
            }

            var delta = diff * ColorStep;
            this._draftColor = actionParameter switch
            {
                RedAdjustment => this._draftColor.With(r: this._draftColor.R + delta),
                GreenAdjustment => this._draftColor.With(g: this._draftColor.G + delta),
                BlueAdjustment => this._draftColor.With(b: this._draftColor.B + delta),
                AlphaAdjustment => this._draftColor.With(a: this._draftColor.A + delta),
                _ => this._draftColor
            };
            this.RefreshAllActions();
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize) =>
            actionParameter == CommitColorCommand ? "Apply Color" : null;

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize) =>
            actionParameter == CommitColorCommand
                ? DynamicFolderVisuals.CreateColorImage(this._draftColor, imageSize, this._backgroundControl?.IsConnected == true)
                : null;

        public override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                RedAdjustment => "R",
                GreenAdjustment => "G",
                BlueAdjustment => "B",
                AlphaAdjustment => "A",
                _ => null
            };
        }

        public override String GetAdjustmentValue(String actionParameter)
        {
            return actionParameter switch
            {
                RedAdjustment => this._draftColor.R.ToString(),
                GreenAdjustment => this._draftColor.G.ToString(),
                BlueAdjustment => this._draftColor.B.ToString(),
                AlphaAdjustment => this._draftColor.A.ToString(),
                _ => null
            };
        }

        public override BitmapImage GetAdjustmentImage(String actionParameter, PluginImageSize imageSize) =>
            DynamicFolderVisuals.CreateTextImage(this.GetAdjustmentDisplayName(actionParameter, imageSize), this._backgroundControl?.IsConnected == true, imageSize);

        private async System.Threading.Tasks.Task CommitColorAsync()
        {
            try
            {
                await this._backgroundControl.SetOverlayRgbaAsync(this._draftColor).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"[BackgroundColorDynamicFolder] color update failed: {ex.Message}");
            }
            finally
            {
                this.RefreshAllActions();
            }
        }

        private void OnBackgroundStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonBackgroundControl.OverlayColorKey) || e.Values.ContainsKey(JonBackgroundControl.OverlayAlphaKey))
            {
                this._draftColor = this._backgroundControl?.OverlayRgba ?? this._draftColor;
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
            this.AdjustmentValueChanged(RedAdjustment);
            this.AdjustmentValueChanged(GreenAdjustment);
            this.AdjustmentValueChanged(BlueAdjustment);
            this.AdjustmentValueChanged(AlphaAdjustment);
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
            this._draftColor = this._backgroundControl?.OverlayRgba ?? this._draftColor;
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
    }
}
