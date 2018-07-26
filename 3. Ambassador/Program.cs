using Newtonsoft.Json.Linq;
using Serilog;
using ServiceStack;
using System;
using System.Drawing;
using System.Threading;
using Console = Colorful.Console;

namespace ExternalConfigurationStore
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing ambassador pattern");

            ServicesPerformanceMonitor.Log = new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger();
            ServicesPerformanceMonitor.ValidationTimeSpan = TimeSpan.FromMinutes(1);

            ServicesPerformanceMonitor.Register(new OpenLibraryService(ServicesPerformanceMonitor.Log, "OpenLibraryService-Main", ServiceDegradationWeight.High, TimeSpan.FromSeconds(10)), TimeSpan.FromMilliseconds(300));
            ServicesPerformanceMonitor.Register(new OpenLibraryService(ServicesPerformanceMonitor.Log, "OpenLibraryService-Backup", ServiceDegradationWeight.Medium, TimeSpan.FromSeconds(10)), TimeSpan.FromMilliseconds(300));
            ServicesPerformanceMonitor.Register(new RandomDogService(ServicesPerformanceMonitor.Log, "RandomDogService", ServiceDegradationWeight.Full, TimeSpan.FromSeconds(10)), TimeSpan.FromMilliseconds(300));

            ServicesPerformanceMonitor.Initialize();

            var service1 = (RandomDogService) ServicesPerformanceMonitor.Get("RandomDogService");
            var result1 = service1.Run();
            Console.WriteLine(result1);


            var service2 = (OpenLibraryService) ServicesPerformanceMonitor.Get("OpenLibraryService-Main");
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
                log.Information($"Services is currently in optimal state. [{100 - degradationPercentage}%]");
            else
            log.Error($"Services have degrated to {degradationState} with {degradationPercentage} % severity.");

        }
        
    }
}
