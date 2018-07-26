using Polly;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace Retry
{
    public class WeatherService : MonitoredService
    {
        static ILogger log;
        Policy _policy;

        Policy default_policy = Policy
              .Handle<Exception>()
              .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (result, timeSpan, retryCount, context) =>
              {
                  log.Warning($"Calling service failed. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
              });


        public WeatherService(ILogger Log, string Name, ServiceDegradationWeight Weight, TimeSpan TimeOut, Policy RetryPolicy = null)
        {
            log = Log;
            this.FriendlyName = Name;
            this.DegradedWeight = Weight;
            this._policy = RetryPolicy ?? default_policy;
            this._timeOut = TimeOut;
        }
        public override object Run(Func<object> Code)
        {
            object return_value = null;

            lock (_monitor)
            {
                try
                {
                    if (TimeoutTimeSpan > TimeSpan.Zero)
                    {
                        //service still in timeout mode
                        return_value = null;
                        log.Warning($"Calling service failed. Service in Open state with timeout not expired. Waiting {this.TimeoutTimeSpan} before service is available again.");

                        throw new ServiceInTimeoutException(this.TimeoutTimeSpan, this._timeOut - this.TimeoutTimeSpan, $"Calling service failed. Service in Open state with timeout not expired.");
                    }
                    else
                    {
                        return_value = _policy.Execute(() => Code());
                    }

                    this.NextState(State);
                }
                catch(Exception ex)
                {
                    return_value = null;
                    this.NextState(State, ex);
                }

                return return_value;

            }
        }

        protected override void FillDegradedSateMap()
        {
            //mainly take the default values

            //chnage only two values from default
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.Medium, ServiceDegradationState.Degraded, 25);
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.Low, ServiceDegradationState.Degraded, 10);
        }


    }
}
