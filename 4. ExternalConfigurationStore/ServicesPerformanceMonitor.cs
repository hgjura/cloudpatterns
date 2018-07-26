using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace ExternalConfigurationStore
{
    public static class ServicesPerformanceMonitor
    {
        private static Dictionary<string, Tuple<MonitoredService, TimeSpan>> _servicesMap = new Dictionary<string, Tuple<MonitoredService, TimeSpan>>();
        private static Timer _timer;

        public static ServiceDegradationState ServicesDegradedState { get; set; }
        public static int ServicesDegradedPercentage { get; set; }
        public static TimeSpan ValidationTimeSpan { get; set; }
        public static ILogger Log { get; set; }

        


        public static void Register(MonitoredService Service, TimeSpan DefaultLatencyBenchmark)
        {
            if (!_servicesMap.ContainsKey(Service.FriendlyName))
            {
                _servicesMap.Add(Service.FriendlyName, new Tuple<MonitoredService, TimeSpan>(Service, DefaultLatencyBenchmark));

                Service.ServiceStateChanged += ServiceStatusChanged;

                Log.Information($"Service {Service.FriendlyName} has been added to monitoring.");

                CalculateDegratedSettings();
            }
            else
                throw new ArgumentException("Service already exists.");
        }

        public static void Remove(MonitoredService Service)
        {
            if (_servicesMap.ContainsKey(Service.FriendlyName))
            {
                _servicesMap.Remove(Service.FriendlyName);

                Service.ServiceStateChanged -= ServiceStatusChanged;

                Log.Information($"Service {Service.FriendlyName} has been removed from monitoring.");

                CalculateDegratedSettings();
            }
        }

        public static MonitoredService Get(string ServiceName)
        {
            return _servicesMap.ContainsKey(ServiceName) ? _servicesMap[ServiceName].Item1 : null;
        }
        public static void Initialize()
        {
            _timer = new Timer(ValidationTimeSpan.TotalMilliseconds);
            _timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            _timer.Start();
        }

        private static void CalculateDegratedSettings()
        {
            int  percent = 0;
            ServicesDegradedState = ServiceDegradationState.Ok;

            foreach (var se in _servicesMap)
            {
                if (se.Value.Item1.State == ServiceState.Closed)
                {
                    // in this state service is in good state; 
                    // calculate any degradation due to latency
                    if (se.Value.Item1.LastLatency > se.Value.Item2 && se.Value.Item1.LastLatency < se.Value.Item2 * 1.20)
                    {
                        if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Full || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.High)
                        { }
                        else if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Medium || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Low)
                        { }

                    }
                    else if (se.Value.Item1.LastLatency >= se.Value.Item2 * 1.20 && se.Value.Item1.LastLatency < se.Value.Item2 * 1.50)
                    {
                        if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Full || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.High)
                        {
                            percent += 20 / _servicesMap.Count;
                        }
                        else if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Medium || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Low)
                        {
                            percent += 10 / _servicesMap.Count;
                        }
                    }
                    else if (se.Value.Item1.LastLatency >= se.Value.Item2 * 1.50 && se.Value.Item1.LastLatency < se.Value.Item2 * 2)
                    {
                        if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Full || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.High)
                        {
                            percent += 30 / _servicesMap.Count;
                        }
                        else if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Medium || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Low)
                        {
                            percent += 15 / _servicesMap.Count;
                        }
                    }
                    else if (se.Value.Item1.LastLatency >= se.Value.Item2 * 2)
                    {
                        if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Full || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.High)
                        {
                            percent += 50 / _servicesMap.Count;
                        }
                        else if (se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Medium || se.Value.Item1.DegradedWeight == ServiceDegradationWeight.Low)
                        {
                            percent += 25 / _servicesMap.Count;
                        }
                    }
                }
                else
                {
                    // in this state service is not in good state; 
                    // calculate degradation

                    percent += se.Value.Item1.DegradedPercentage / _servicesMap.Count;
                }              
            }

            if (percent >= 90)
                ServicesDegradedState = ServiceDegradationState.Critical;
            else if (percent >= 50)
                ServicesDegradedState = ServiceDegradationState.Bad;
            else if (percent >= 10)
                ServicesDegradedState = ServiceDegradationState.Degraded;
            else
                ServicesDegradedState = ServiceDegradationState.Ok;

            ServicesDegradedPercentage = percent;
        }

        private static void ServiceStatusChanged(object sender, ServiceChangedEventArgs e)
        {
            if (e.OldState != e.NewState)
            {
                Log.Information($"Service {e.Service.FriendlyName} has changed state to {e.Service.State}.");
                Log.Information($"Service {e.Service.FriendlyName} degrated at {e.Service.DegradedState} at {e.Service.DegradedPercentage} % severity.");
            }

            CalculateDegratedSettings();
        }
        
        private static void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();
            foreach (var se in _servicesMap)
            {
                Log.Information($"Service {se.Value.Item1.FriendlyName} is being validated.");
                se.Value.Item1.Validate();
            }
            _timer.Start();
        }
    }
}
