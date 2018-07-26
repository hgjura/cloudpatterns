using Newtonsoft.Json.Linq;
using Serilog;
using ServiceStack;
using System;
using System.Drawing;
using System.Threading;
using Console = Colorful.Console;
using System.Dynamic;
using System.Collections.Generic;
using System.ComponentModel;

namespace ExternalConfigurationStore
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing External Configuration Store pattern");


            var gs = new GlobalSettings()
                .With(
                    new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger(), 
                    TimeSpan.FromMinutes(10)
                );

            var AppSettings = gs.ForApplication("ExternalConfigurationStore");

            //var UserSettings = new GlobalSettings().With(
            //    new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger())
            //    .ForUser("hgjura@live.com");

            //if (!((ExpandoObject)AppSettings).Contains("ConnectionStrings"))

            

            #region Settings setup -- only once

            //gs.ClearAppSettings();

            //Connectionstrings node
            gs.AddAppSettingCollection(AppSettings, "ConnectionStrings");
            //Azure connectionstrings
            gs.AddAppSettingCollection(AppSettings.ConnectionStrings, "Azure");

            //Services node
            gs.AddAppSettingCollection(AppSettings, "Services");
            //RandomDogService
            gs.AddAppSettingCollection(AppSettings.Services, "RandomDog");

            AppSettings.Services.RandomDog.ServiceUrl = $"https://random.dog/woof.json";
            AppSettings.Services.RandomDog.TimeoutPeriodInMinutes = 10;
            AppSettings.Services.RandomDog.DefaultLatencyBenchmarkInMs = 300;
            AppSettings.Services.RandomDog.Retries = 3;

            //OpenLibraryService
            gs.AddAppSettingCollection(AppSettings.Services, "OpenLibraryService");

            AppSettings.Services.OpenLibraryService.TimeoutPeriodInMinutes = 5;
            AppSettings.Services.OpenLibraryService.DefaultLatencyBenchmarkInMs = 300;
            AppSettings.Services.OpenLibraryService.Retries = 5;

            Console.WriteLine(AppSettings.Services.RandomDog.ServiceUrl, Color.Green);
            Console.WriteLine(AppSettings.Services.RandomDog.TimeoutPeriodInMinutes, Color.Green);
            Console.WriteLine(AppSettings.Services.RandomDog.DefaultLatencyBenchmarkInMs, Color.Green);
            Console.WriteLine(AppSettings.Services.RandomDog.Retries, Color.Green);

            Console.WriteLine(AppSettings.Services.OpenLibraryService.TimeoutPeriodInMinutes, Color.Yellow);
            Console.WriteLine(AppSettings.Services.OpenLibraryService.DefaultLatencyBenchmarkInMs, Color.Yellow);
            Console.WriteLine(AppSettings.Services.OpenLibraryService.Retries, Color.Yellow);

            #endregion

            #region Service Monitoring

            ServicesPerformanceMonitor.Log = new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger();
            ServicesPerformanceMonitor.ValidationTimeSpan = TimeSpan.FromMinutes(1);

            ServicesPerformanceMonitor.Register(new OpenLibraryService(ServicesPerformanceMonitor.Log, 
                "OpenLibraryService-Main", 
                ServiceDegradationWeight.High, 
                TimeSpan.FromMinutes(AppSettings.Services.OpenLibraryService.TimeoutPeriodInMinutes)), 
                TimeSpan.FromMilliseconds(AppSettings.Services.OpenLibraryService.DefaultLatencyBenchmarkInMs));

            ServicesPerformanceMonitor.Register(new OpenLibraryService(ServicesPerformanceMonitor.Log, 
                "OpenLibraryService-Backup", 
                ServiceDegradationWeight.Medium, 
                TimeSpan.FromMinutes(AppSettings.Services.OpenLibraryService.TimeoutPeriodInMinutes)), 
                TimeSpan.FromMilliseconds(AppSettings.Services.OpenLibraryService.DefaultLatencyBenchmarkInMs));

            ServicesPerformanceMonitor.Register(new RandomDogService(ServicesPerformanceMonitor.Log, 
                "RandomDogService", 
                ServiceDegradationWeight.Full, 
                TimeSpan.FromMinutes(AppSettings.Services.RandomDog.TimeoutPeriodInMinutes)), 
                TimeSpan.FromMilliseconds(AppSettings.Services.RandomDog.DefaultLatencyBenchmarkInMs));

            ServicesPerformanceMonitor.Initialize();

            var service1 = (RandomDogService)ServicesPerformanceMonitor.Get("RandomDogService");
            var result1 = service1.Run();
            Console.WriteLine(result1);


            var service2 = (OpenLibraryService)ServicesPerformanceMonitor.Get("OpenLibraryService-Main");
            dynamic result;

            result = service2.Run(new object[] { "0201558025" });
            if (result != null)
                Console.WriteLine($"{Environment.NewLine}[{result["publishers"][0]["name"]}] {result["authors"][0]["name"]} - {result["title"]}{Environment.NewLine}", Color.GreenYellow);
            WriteLogToConsole(ServicesPerformanceMonitor.Log, ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);
            WriteStatusToConsole(ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);

            result = service2.Run(new { isbn = "0385472579" });
            if (result != null)
                Console.WriteLine($"{Environment.NewLine}[{result["publishers"][0]["name"]}] {result["authors"][0]["name"]} - {result["title"]}{Environment.NewLine}", Color.GreenYellow);
            WriteLogToConsole(ServicesPerformanceMonitor.Log, ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);
            WriteStatusToConsole(ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);

            result = service2.Run(new { isbn = "0385472579", url = "http://none" });
            if (result != null)
                Console.WriteLine($"{Environment.NewLine}[{result["publishers"][0]["name"]}] {result["authors"][0]["name"]} - {result["title"]}{Environment.NewLine}", Color.GreenYellow);
            WriteLogToConsole(ServicesPerformanceMonitor.Log, ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);
            WriteStatusToConsole(ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);

            Thread.Sleep(TimeSpan.FromSeconds(10));

            result = service2.Run(new { isbn = "0866119817" });
            if (result != null)
                Console.WriteLine($"{Environment.NewLine}[{result["publishers"][0]["name"]}] {result["authors"][0]["name"]} - {result["title"]}{Environment.NewLine}", Color.GreenYellow);
            WriteLogToConsole(ServicesPerformanceMonitor.Log, ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);
            WriteStatusToConsole(ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);



            Thread.Sleep(TimeSpan.FromMinutes(2));

            WriteLogToConsole(ServicesPerformanceMonitor.Log, ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);
            WriteStatusToConsole(ServicesPerformanceMonitor.ServicesDegradedState, ServicesPerformanceMonitor.ServicesDegradedPercentage);

            #endregion


            Console.ReadKey();
        }


        private static void WriteStatusToConsole(ServiceDegradationState degradationState, int degradationPercentage)
        {
            if (degradationState == ServiceDegradationState.Critical)
            {
                Console.WriteAscii($"{degradationPercentage}% {degradationState}", Color.Red);
            }
            else if (degradationState == ServiceDegradationState.Bad || degradationState == ServiceDegradationState.Degraded)
            {
                Console.WriteAscii($"{degradationPercentage}% {degradationState}", Color.Yellow);
            }
            else
            {
                Console.WriteAscii($"{100 - degradationPercentage}% {degradationState}", Color.Green);
            }
        }

        private static void WriteLogToConsole(ILogger log, ServiceDegradationState degradationState, int degradationPercentage)
        {
            if (degradationState == ServiceDegradationState.Ok)
                log.Information($"Services are currently in optimal state. [{100 - degradationPercentage}%]");
            else
                log.Error($"Services have degrated to {degradationState} with {degradationPercentage} % severity.");

        }
    }
}
