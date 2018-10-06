using Serilog;
using System;

namespace CircuitBreaker
{
    public class AuthService : MonitoredService
    {
        public AuthService(ILogger log, string Name, ServiceDegradationWeight Weight, TimeSpan TimeOut, int Retry = 3)
        {
            this.FriendlyName = Name;
            this.DegradationWeight = Weight;
            this._retry = Retry;
            this._timespan = TimeOut;
        }
        public override object Run(Func<object> Code)
        {
            object return_value = null;

            switch (this.State)
                    {
                        case ServiceState.Closed:
                            {
                                try
                                {
                                    //run service code

                                    return_value = Utils.SimulateServiceCall(Code);

                                    //

                                    //set the state = Closed values
                                    this._failures = 0;
                                    LastSuccesfullRunUtc = DateTime.UtcNow;
                                    LastFailedRunUtc = DateTime.MinValue;
                                    LastException = null;
                                }
                                catch (Exception ex)
                                {
                                    return_value = null;

                                    //set the state != Closed values
                                    this._failures++;
                                    LastSuccesfullRunUtc = DateTime.MinValue;
                                    LastFailedRunUtc = DateTime.UtcNow;
                                    LastException = ex;

                                    //set the state to HalfOpen
                                    this.State = ServiceState.HalfOpen;
                                    OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                                }
                            }
                            break;
                        case ServiceState.HalfOpen:
                            {
                                try
                                {
                                    //run service code

                                    return_value = Utils.SimulateServiceCall(Code);

                                    //


                                    //set the state = Closed values
                                    this._failures = 0;
                                    LastSuccesfullRunUtc = DateTime.UtcNow;
                                    LastFailedRunUtc = DateTime.MinValue;
                                    LastException = null;

                                    //set the state to closed
                                    this.State = ServiceState.Closed;
                                    OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });

                                }
                                catch (Exception ex)
                                {
                                    return_value = null;

                                    //set the state != Closed values
                                    this._failures++;
                                    LastSuccesfullRunUtc = DateTime.MinValue;
                                    LastFailedRunUtc = DateTime.UtcNow;
                                    LastException = ex;

                                    //set the state to Open
                                    if (this._failures >= this._retry)
                                    {
                                        this.State = ServiceState.Open;
                                        OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                                    }
                                }
                            }
                            break;
                        case ServiceState.Open:
                            {
                                if (this.LastFailedRunUtc.Add(this._timespan) > DateTime.UtcNow)
                                {
                                    //service still in timeout mode
                                    return_value = null;
                                }
                                else
                                {

                                    try
                                    {
                                        //run service code

                                        return_value = Utils.SimulateServiceCall(Code);

                                        //


                                        //set the state = Closed values
                                        this._failures = 0;
                                        LastSuccesfullRunUtc = DateTime.UtcNow;
                                        LastFailedRunUtc = DateTime.MinValue;
                                        LastException = null;

                                        //set the state to closed
                                        this.State = ServiceState.Closed;
                                        OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });

                                    }
                                    catch (Exception ex)
                                    {
                                        return_value = null;

                                        //set the state != Closed values
                                        this._failures = 0;
                                        LastSuccesfullRunUtc = DateTime.MinValue;
                                        LastFailedRunUtc = DateTime.UtcNow;
                                        LastException = ex;

                                        //set the state to HalfOpen
                                        this.State = ServiceState.HalfOpen;
                                        OnServiceStateChanged(new ServiceChangedEventArgs() { Service = this });
                                    }

                                }
                            }
                            break;
                    }

            return return_value;           
        }
    }
}
