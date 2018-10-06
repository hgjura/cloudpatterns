using System;
using System.Collections.Generic;
using System.Text;

namespace CircuitBreaker
{
    public abstract class MonitoredService
    {
        protected int _retry = 3;
        protected TimeSpan _timespan = TimeSpan.FromMinutes(10);
        protected int _failures = 0;
        protected ServiceState _servicState;

        public string FriendlyName;
        public ServiceState State { get { return this._servicState; } set { this._servicState = value; ResetDegradedState(); } }
        public ServiceDegradationState DegradedState { get; private set; }
        public ServiceDegradationWeight DegradationWeight;
        public double DegradationSeverity { get {
                if (this.State == ServiceState.Closed)
                {
                    return 0;
                }
                else if (this.State == ServiceState.Open)
                {
                    switch (this.DegradationWeight)
                    {
                        case ServiceDegradationWeight.Full:
                            return 1;
                        case ServiceDegradationWeight.High:
                            return 0.9;
                        case ServiceDegradationWeight.Medium:
                            return 0.8;
                        case ServiceDegradationWeight.Low:
                            return 0.7;
                        case ServiceDegradationWeight.None:
                            return 0;
                        default:
                            return 0;
                    }
                }
                else
                {
                    switch (this.DegradationWeight)
                    {
                        case ServiceDegradationWeight.Full:
                            return 1;
                        case ServiceDegradationWeight.High:
                            return 0.5;
                        case ServiceDegradationWeight.Medium:
                            return 0.4;
                        case ServiceDegradationWeight.Low:
                            return 0.3;
                        case ServiceDegradationWeight.None:
                            return 0;
                        default:
                            return 0;
                    }
                }
            } }
        public TimeSpan TimeoutTimeSpan { get { return (State == ServiceState.Open && LastFailedRunUtc > DateTime.MinValue) ? LastFailedRunUtc.Add(this._timespan) - DateTime.UtcNow : TimeSpan.Zero; } }
        public Exception LastException;
        public DateTime LastSuccesfullRunUtc;
        public DateTime LastFailedRunUtc;
        public event EventHandler<ServiceChangedEventArgs> ServiceStateChanged;

        protected virtual void OnServiceStateChanged(ServiceChangedEventArgs e)
        {
            ServiceStateChanged?.Invoke(this, e);
        }
        public abstract object Run(Func<object> Code);

        private void ResetDegradedState()
        {
            if (this.State == ServiceState.Closed)
            {
                this.DegradedState = ServiceDegradationState.Ok;
            }
            else if (this.State == ServiceState.Open)
            {
                switch (this.DegradationWeight)
                {
                    case ServiceDegradationWeight.Full:
                        this.DegradedState = ServiceDegradationState.Critical;
                        break;
                    case ServiceDegradationWeight.High:
                        this.DegradedState = ServiceDegradationState.Bad;
                        break;
                    case ServiceDegradationWeight.Medium:
                        this.DegradedState = ServiceDegradationState.Degraded;
                        break;
                    case ServiceDegradationWeight.Low:
                        this.DegradedState = ServiceDegradationState.Degraded;
                        break;
                    case ServiceDegradationWeight.None:
                        this.DegradedState = ServiceDegradationState.Ok;
                        break;
                    default:
                        this.DegradedState = ServiceDegradationState.Ok;
                        break;
                }
            }
            else
            {
                switch (this.DegradationWeight)
                {
                    case ServiceDegradationWeight.Full:
                        this.DegradedState = ServiceDegradationState.Bad;
                        break;
                    case ServiceDegradationWeight.High:
                        this.DegradedState = ServiceDegradationState.Degraded;
                        break;
                    case ServiceDegradationWeight.Medium:
                        this.DegradedState = ServiceDegradationState.Degraded;
                        break;
                    case ServiceDegradationWeight.Low:
                        this.DegradedState = ServiceDegradationState.Degraded;
                        break;
                    case ServiceDegradationWeight.None:
                        this.DegradedState = ServiceDegradationState.Ok;
                        break;
                    default:
                        this.DegradedState = ServiceDegradationState.Ok;
                        break;
                }
            }
        }
    }

    public enum ServiceState { Closed, HalfOpen, Open }
    public enum ServiceDegradationState { Ok, Degraded, Bad, Critical }
    public enum ServiceDegradationWeight { None, Low, Medium, High, Full }
    public class ServiceChangedEventArgs : EventArgs
    {
        public MonitoredService Service { get; set; }
    }
}
