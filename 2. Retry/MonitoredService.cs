﻿using System;
using System.Collections.Generic;

namespace Retry
{
    public abstract class MonitoredService
    {
        protected readonly object _monitor;
        protected TimeSpan _timeOut;
        protected ServiceState _servicState;
        protected Dictionary<Tuple<ServiceState, ServiceDegradationWeight>, Tuple<ServiceDegradationState, int>> _degradedSettingsMap;

        public MonitoredService()
        {
            _monitor = new object();
            _timeOut = TimeSpan.Zero;
            _servicState = ServiceState.Closed;
            _degradedSettingsMap = new Dictionary<Tuple<ServiceState, ServiceDegradationWeight>, Tuple<ServiceDegradationState, int>>();

            /* 
             * In this section fill the map with various degradation settings. Note that child classes would need to override a function to explicitly set these values. 
             * Or you can override the function, and leave it empty to take the default values. 
             * The values below are set as an illustration only. You will need to set these values accordingly for each service. 
             * The values for ServiceState.Closed do not need to be there as they should always return Ok and 0% degradation when the service is at Closed state.
             * They are sed here as default values, in order to not have the lookup of the dictionary fail or if this condition is not set in overriden functio to not make it fail accidentally.
             */

            this.AddDegratedSettingMapItem(ServiceState.Closed, ServiceDegradationWeight.Full, ServiceDegradationState.Ok, 0);
            this.AddDegratedSettingMapItem(ServiceState.Closed, ServiceDegradationWeight.High, ServiceDegradationState.Ok, 0);
            this.AddDegratedSettingMapItem(ServiceState.Closed, ServiceDegradationWeight.Medium, ServiceDegradationState.Ok, 0);
            this.AddDegratedSettingMapItem(ServiceState.Closed, ServiceDegradationWeight.Low, ServiceDegradationState.Ok, 0);
            this.AddDegratedSettingMapItem(ServiceState.Closed, ServiceDegradationWeight.None, ServiceDegradationState.Ok, 0);

            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.Full, ServiceDegradationState.Bad, 100);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.High, ServiceDegradationState.Degraded, 50);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.Medium, ServiceDegradationState.Degraded, 40);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.Low, ServiceDegradationState.Ok, 30);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.None, ServiceDegradationState.Ok, 0);

            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.Full, ServiceDegradationState.Critical, 100);
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.High, ServiceDegradationState.Bad, 90);
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.Medium, ServiceDegradationState.Degraded, 80);
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.Low, ServiceDegradationState.Degraded, 70);
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.None, ServiceDegradationState.Ok, 0);
        }

        public virtual string FriendlyName { get; set; }
        public virtual ServiceState State { get { return this._servicState; } set { this._servicState = value; this.DegradedState = this.GetDegratedStateMapItem(value, this.DegradedWeight); this.DegradedPercentage = this.GetDegratedPercentageSeverityMapItem(value, this.DegradedWeight); } }
        public virtual ServiceDegradationState DegradedState { get; private set; }
        public virtual ServiceDegradationWeight DegradedWeight { get; set; }
        public virtual int DegradedPercentage { get; set; }
        public virtual TimeSpan TimeoutTimeSpan { get { return (State == ServiceState.Open && LastFailedRunUtc.HasValue && LastFailedRunUtc.Value > DateTime.MinValue) ? LastFailedRunUtc.Value.Add(this._timeOut) - DateTime.UtcNow : TimeSpan.Zero; } }
        public virtual Exception LastException { get; set; }
        public virtual DateTime? LastSuccesfullRunUtc { get; set; }
        public virtual DateTime? LastFailedRunUtc { get; set; }
        public virtual event EventHandler<ServiceChangedEventArgs> ServiceStateChanged;
        protected virtual void OnServiceStateChanged(ServiceChangedEventArgs e) => ServiceStateChanged?.Invoke(this, e);

        protected virtual void AddDegratedSettingMapItem(ServiceState State, ServiceDegradationWeight Weight, ServiceDegradationState DegradationState, int DegradationPercentage)
        {
            _degradedSettingsMap.Add(new Tuple<ServiceState, ServiceDegradationWeight>(State, Weight), new Tuple<ServiceDegradationState, int>(DegradationState, DegradationPercentage));
        }

        protected virtual ServiceDegradationState GetDegratedStateMapItem(ServiceState State, ServiceDegradationWeight Weight) => _degradedSettingsMap[new Tuple<ServiceState, ServiceDegradationWeight>(State, Weight)].Item1;

        protected virtual int GetDegratedPercentageSeverityMapItem(ServiceState State, ServiceDegradationWeight Weight) => _degradedSettingsMap[new Tuple<ServiceState, ServiceDegradationWeight>(State, Weight)].Item2;

        public virtual void NextState(ServiceState CurrentState, Exception ex = null)
        {
            switch (CurrentState)
            {
                case ServiceState.Closed:
                    {
                        if (ex == null)
                        {
                            //set the state to Close
                            LastSuccesfullRunUtc = DateTime.UtcNow;
                            LastFailedRunUtc = null;
                            LastException = null;
                        }
                        else
                        {
                            //set the state to HalfOpen
                            LastSuccesfullRunUtc = null;
                            LastFailedRunUtc = DateTime.UtcNow;
                            LastException = ex;
                            this.State = ServiceState.HalfOpen;
                            OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                        }
                    }
                    break;
                case ServiceState.HalfOpen:
                    {
                        if (ex == null)
                        {
                            //set the state to Close
                            LastSuccesfullRunUtc = DateTime.UtcNow;
                            LastFailedRunUtc = null;
                            LastException = null;
                            this.State = ServiceState.Closed;
                            OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                        }
                        else
                        {
                            //set the state to Open
                            LastSuccesfullRunUtc = null;
                            LastFailedRunUtc = DateTime.UtcNow;
                            LastException = ex;
                            this.State = ServiceState.Open;
                            OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                        }
                    }
                    break;
                case ServiceState.Open:
                    {
                        if (ex == null)
                        {
                            //set the state to Closed
                            LastSuccesfullRunUtc = DateTime.UtcNow;
                            LastFailedRunUtc = null;
                            LastException = null;
                            this.State = ServiceState.Closed;
                            OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                        }
                        else
                        {
                            if (ex is ServiceInTimeoutException)
                            {
                                //do nothing; skip over until timeout expires
                            }
                            else
                            {
                                //set the state to HalfOpen
                                LastSuccesfullRunUtc = null;
                                LastFailedRunUtc = DateTime.UtcNow;
                                LastException = ex;
                                this.State = ServiceState.HalfOpen;
                                OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                            }
                        }
                    }
                    break;
            }
        }

        public abstract object Run(Func<object> Code);
        
        protected abstract void FillDegradedSateMap();
    }

    public enum ServiceState { Closed, HalfOpen, Open }
    public enum ServiceDegradationState { Ok, Degraded, Bad, Critical }
    public enum ServiceDegradationWeight { None, Low, Medium, High, Full }
    public class ServiceChangedEventArgs : EventArgs
    {
        public MonitoredService Service { get; set; }
    }
    public class ServiceInTimeoutException : TimeoutException
    {
        public ServiceInTimeoutException(TimeSpan RemainingTimeSpan, TimeSpan ElapsedTimeSpan, string message, Exception innerException = null) : base (message, innerException)
        {
            this.RemainingTimeSpan = RemainingTimeSpan;
            this.ElapsedTimeSpan = ElapsedTimeSpan;
        }
        public TimeSpan RemainingTimeSpan { get; private set; }
        public TimeSpan ElapsedTimeSpan { get; private set; }
    }
}
