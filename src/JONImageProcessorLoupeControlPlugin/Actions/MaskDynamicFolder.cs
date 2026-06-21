namespace Loupedeck.JONImageProcessorLoupeControlPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Gateway.Controls;
    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    public sealed class MaskDynamicFolder : PluginDynamicFolder
    {
        private const String ToggleMaskCommand = "toggle-mask";
        private const String CommitMorphologyCommand = "commit-morphology";
        private const String NoopThresholdCommand = "noop-threshold";
        private const String NoopSmoothingCommand = "noop-smoothing";
        private const String ThresholdAdjustment = "threshold";
        private const String SmoothingAdjustment = "smoothing";
        private const String MorphologyAdjustment = "morphology";
        private const Double ThresholdStep = 0.001;
        private const Double SmoothingStep = 0.01;
        private static readonly String[] MorphologyOptions = ["off", "light", "strong"];

        private JonMaskControl _maskControl;
        private String _draftMorphology = "light";

        public MaskDynamicFolder()
        {
            this.DisplayName = "Mask";
            this.GroupName = "JON Image Processor";
        }

        public override void Load()
        {
            this._maskControl = JONImageProcessorLoupeControlPlugin.MaskControl;
            this._draftMorphology = this._maskControl?.Morphology ?? "light";
            if (this._maskControl != null)
            {
                this._maskControl.StateChanged += this.OnMaskStateChanged;
                this._maskControl.ConnectionChanged += this.OnMaskConnectionChanged;
            }
        }

        public override void Unload()
        {
            if (this._maskControl != null)
            {
                this._maskControl.StateChanged -= this.OnMaskStateChanged;
                this._maskControl.ConnectionChanged -= this.OnMaskConnectionChanged;
            }
        }

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType deviceType) =>
            PluginDynamicFolderNavigation.ButtonArea;

        public override IEnumerable<String> GetButtonPressActionNames(DeviceType deviceType)
        {
            return new[]
            {
                PluginDynamicFolder.NavigateUpActionName,
                this.CreateCommandName(ToggleMaskCommand)
            };
        }

        public override IEnumerable<String> GetEncoderRotateActionNames(DeviceType deviceType)
        {
            return new[]
            {
                this.CreateAdjustmentName(ThresholdAdjustment),
                this.CreateAdjustmentName(SmoothingAdjustment),
                this.CreateAdjustmentName(MorphologyAdjustment)
            };
        }

        public override IEnumerable<String> GetEncoderPressActionNames(DeviceType deviceType)
        {
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
                    this.ButtonActionNamesChanged();
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

        public override String GetButtonDisplayName(PluginImageSize imageSize) => "Mask";

        protected override BitmapImage GetButtonImage(PluginImageSize imageSize)
        {
            using var bitmapBuilder = new BitmapBuilder(imageSize);
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, BitmapColor.Black);
            ButtonVisuals.DrawText(bitmapBuilder, "Mask", BitmapColor.White);
            return bitmapBuilder.ToImage();
        }

        public override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                ToggleMaskCommand => this._maskControl?.MaskEnabled == true ? "Mask\nON" : "Mask\nOFF",
                CommitMorphologyCommand => $"Set\n{Title(this._draftMorphology)}",
                _ => null
            };
        }

        public override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == ToggleMaskCommand)
            {
                using var bitmapBuilder = new BitmapBuilder(imageSize);
                var connected = this._maskControl?.IsConnected == true;
                var enabled = this._maskControl?.MaskEnabled == true;
                var background = !connected ? Colors.DisabledBackground : enabled ? Colors.Green : BitmapColor.Black;
                var textColor = connected ? BitmapColor.White : Colors.DisabledText;
                ButtonVisuals.FillBackground(bitmapBuilder, imageSize, background);
                ButtonVisuals.DrawText(bitmapBuilder, enabled ? "Mask\nON" : "Mask\nOFF", textColor);
                return bitmapBuilder.ToImage();
            }

            if (actionParameter == CommitMorphologyCommand)
            {
                using var bitmapBuilder = new BitmapBuilder(imageSize);
                ButtonVisuals.FillBackground(bitmapBuilder, imageSize, BitmapColor.Black);
                ButtonVisuals.DrawText(bitmapBuilder, $"Set\n{Title(this._draftMorphology)}", BitmapColor.White);
                return bitmapBuilder.ToImage();
            }

            return null;
        }

        public override String GetAdjustmentDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            return actionParameter switch
            {
                ThresholdAdjustment => "Threshold",
                SmoothingAdjustment => "Smoothing",
                MorphologyAdjustment => "Morphology",
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
            ButtonVisuals.FillBackground(bitmapBuilder, imageSize, BitmapColor.Black);
            ButtonVisuals.DrawText(bitmapBuilder, this.GetAdjustmentDisplayName(actionParameter, imageSize), BitmapColor.White);
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
            this.RefreshAllActions();
        }

        private void OnMaskStateChanged(Object sender, JonGatewayStateChangedEventArgs e)
        {
            if (e.Values.ContainsKey(JonMaskControl.MorphologyKey))
            {
                this._draftMorphology = this._maskControl?.Morphology ?? this._draftMorphology;
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

        private static String FormatUnitValue(Double value) =>
            value.ToString("0.000", CultureInfo.InvariantCulture);

        private static String Title(String value) =>
            String.IsNullOrWhiteSpace(value) ? "" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value);
    }
}
