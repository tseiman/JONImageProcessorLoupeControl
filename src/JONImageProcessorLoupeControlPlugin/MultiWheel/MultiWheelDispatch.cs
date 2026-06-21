namespace Loupedeck.JONImageProcessorLoupeControlPlugin.MultiWheel
{
    using System;
    using System.Collections.Generic;

    using Loupedeck.JONImageProcessorLoupeControlPlugin.Helpers;

    internal sealed class MultiWheelDispatch
    {
        private sealed class DispatcherData
        {
            public DispatcherData(Type dispatcherType, IMultiWheelDispatchable dispatchable, IMultiWheelAdjustment adjustment)
            {
                this.DispatcherType = dispatcherType;
                this.Dispatchable = dispatchable;
                this.Adjustment = adjustment;
            }

            public Type DispatcherType { get; }

            public IMultiWheelDispatchable Dispatchable { get; }

            public IMultiWheelAdjustment Adjustment { get; }
        }

        private readonly Dictionary<Type, DispatcherData> _dispatchables = new();
        private DispatcherData _activeDispatcher;

        public event Action DisplayChanged;

        public Boolean HasActiveDispatcher => this._activeDispatcher != null;

        public IMultiWheelDisplayable ActiveDisplay => this._activeDispatcher?.Adjustment as IMultiWheelDisplayable;

        public IMultiWheelAdjustment ActiveAdjustment => this._activeDispatcher?.Adjustment;

        public void RegisterDispatchable(IMultiWheelDispatchable dispatchable, IMultiWheelAdjustment adjustment)
        {
            if (dispatchable == null || adjustment == null)
            {
                return;
            }

            if (adjustment is IMultiWheelDisplayable displayable)
            {
                displayable.DisplayChanged += () => this.DisplayChanged?.Invoke();
            }

            this._dispatchables[dispatchable.GetType()] = new DispatcherData(dispatchable.GetType(), dispatchable, adjustment);
        }

        public void InformActive(IMultiWheelDispatchable dispatchable)
        {
            if (dispatchable == null || !this._dispatchables.TryGetValue(dispatchable.GetType(), out var dispatcher))
            {
                PluginLog.Warning("[MultiWheelDispatch] requested dispatcher is not registered");
                return;
            }

            this._activeDispatcher = dispatcher;
            this.DisplayChanged?.Invoke();

            foreach (var entry in this._dispatchables)
            {
                if (entry.Key != dispatchable.GetType())
                {
                    entry.Value.Dispatchable.Disengage();
                }
            }
        }

        public void InformInActive(IMultiWheelDispatchable dispatchable)
        {
            if (this._activeDispatcher != null && dispatchable?.GetType() == this._activeDispatcher.DispatcherType)
            {
                this._activeDispatcher = null;
                this.DisplayChanged?.Invoke();
            }
        }

        public void ApplyAdjustment(Int32 diff)
        {
            if (diff == 0 || this._activeDispatcher == null)
            {
                return;
            }

            this._activeDispatcher.Adjustment.ApplyAdjustment(diff);
            this.DisplayChanged?.Invoke();
        }

        public void InformUploadCompleted()
        {
            if (this._activeDispatcher?.Dispatchable is IMultiWheelUploadCompletedHandler handler)
            {
                handler.UploadCompleted();
            }
        }
    }
}
