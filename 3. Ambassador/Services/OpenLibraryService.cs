using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Serilog;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExternalConfigurationStore
{
    public class OpenLibraryService : MonitoredService
    {
        static ILogger log;
        Policy _policy;

        Policy default_policy = Policy
              .Handle<Exception>()
              .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (result, timeSpan, retryCount, context) =>
              {
                  log.Warning($"Calling service failed. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
              });


        public OpenLibraryService(ILogger Log, string Name, ServiceDegradationWeight Weight, TimeSpan TimeOut, Policy RetryPolicy = null)
        {
            log = Log;
            this.FriendlyName = Name;
            this.DegradedWeight = Weight;
            this._policy = RetryPolicy ?? default_policy;
            this._timeOut = TimeOut;
        }

        public override object Run(params object[] list)
        {
            string isbn;
            object result = null;

            if (list?.Length > 0)
            {
                isbn = list[0].ToString();
                result = base.RunWrapper(log, _policy, () =>
                {
                    var a = $"http://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data".GetJsonFromUrl();
                    var b = JObject.Parse(a.ToString())?[$"ISBN:{isbn}"];
                    return b != null ? JsonConvert.DeserializeObject<dynamic>(b.ToString()) : null;
                });
            }

            return result;
        }

        public override object Run(dynamic par)
        {
            string url = "http://openlibrary.org/api/books?bibkeys=ISBN:{0}&format=json&jscmd=data";
            string isbn = "";
            object result = null;

            if (par != null)
            {
                if (par.GetType().GetProperty("isbn") != null)
                {
                    isbn = par.isbn.ToString();
                }

                if (par.GetType().GetProperty("url") != null)
                {
                    url = par.url.ToString();
                }

                result = base.RunWrapper(log, _policy, () =>
                {
                    var a = String.Format(url, isbn).GetJsonFromUrl();
                    var b = JObject.Parse(a.ToString())?[$"ISBN:{isbn}"];
                    return b != null ? JsonConvert.DeserializeObject<dynamic>(b.ToString()) : null;
                });
            }

            return result;
        }
        public override bool Validate()
        {
            return this.Run(new object[] { "0201558025" }) != null ? true : false;            
        }
        protected override void FillDegradedStateMap()
        {
            //mainly take the default values

            //chnage only two values from default
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.Full, ServiceDegradationState.Degraded, 100);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.High, ServiceDegradationState.Degraded, 800);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.Medium, ServiceDegradationState.Degraded, 50);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.Low, ServiceDegradationState.Ok, 30);
            this.AddDegratedSettingMapItem(ServiceState.HalfOpen, ServiceDegradationWeight.None, ServiceDegradationState.Ok, 0);
        }


    }
}
