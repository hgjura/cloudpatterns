using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Serilog;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text;

namespace ExternalConfigurationStore
{
    public class RandomDogService : MonitoredService
    {
        static ILogger log;
        Policy _policy;

        Policy default_policy = Policy
              .Handle<Exception>()
              .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (result, timeSpan, retryCount, context) =>
              {
                  log.Warning($"Calling service failed. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
              });


        public RandomDogService(ILogger Log, string Name, ServiceDegradationWeight Weight, TimeSpan TimeOut, Policy RetryPolicy = null)
        {
            log = Log;
            this.FriendlyName = Name;
            this.DegradedWeight = Weight;
            this._policy = RetryPolicy ?? default_policy;
            this._timeOut = TimeOut;
        }
        public override object Run(params object[] list)
        {
            object result = null;

            result = base.RunWrapper(log, _policy, () =>
            {
                var a = $"https://random.dog/woof.json".GetJsonFromUrl();
                var b = JObject.Parse(a.ToString())?[$"url"];
                return b != null ? b.ToString() : null;
            });

            return result;
        }

        public override object Run(dynamic par)
        {
            string url = "https://random.dog/woof.json";
            object result = null;

            if (par != null)
            {
                if (par.GetType().GetProperty("url") != null)
                {
                    url = par.url.ToString();
                }

                result = base.RunWrapper(log, _policy, () =>
                {
                    var a = url.GetJsonFromUrl();
                    var b = JObject.Parse(a.ToString())?[$"url"];
                    return b != null ? b.ToString() : null;
                });
            }

            return result;
        }

        public override bool Validate()
        {
            return this.Run() != null ? true : false;
        }

        protected override void FillDegradedStateMap()
        {
            //mainly take the default values

            //chnage only two values from default
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.Medium, ServiceDegradationState.Degraded, 25);
            this.AddDegratedSettingMapItem(ServiceState.Open, ServiceDegradationWeight.Low, ServiceDegradationState.Degraded, 10);
        }


    }
}
