using Serilog;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Console = Colorful.Console;

namespace Retry
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing retry pattern");
            Console.WriteLine();


            var log = new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger();

            var service = new WeatherService(log, "WeatherService", ServiceDegradationWeight.Full, TimeSpan.FromSeconds(10));


            var result = service.Run(()=> Utils.SimulateServiceCall(()=> { return new object(); }, ServiceRunStatus.Failure));

            WriteLogToConsole(log, service.FriendlyName, service.State, service.DegradedState, service.DegradedPercentage, service.TimeoutTimeSpan);
            WriteStatusToConsole(service.State, service.DegradedState, service.DegradedPercentage);


            service.Run(() => Utils.SimulateServiceCall(() => { return new object(); }, ServiceRunStatus.Failure));


            WriteLogToConsole(log, service.FriendlyName, service.State, service.DegradedState, service.DegradedPercentage, service.TimeoutTimeSpan);
            WriteStatusToConsole(service.State, service.DegradedState, service.DegradedPercentage);

            service.Run(() => Utils.SimulateServiceCall(() => { return new object(); }, ServiceRunStatus.Failure));
            service.Run(() => Utils.SimulateServiceCall(() => { return new object(); }, ServiceRunStatus.Failure));
            service.Run(() => Utils.SimulateServiceCall(() => { return new object(); }, ServiceRunStatus.Failure));

            WriteLogToConsole(log, service.FriendlyName, service.State, service.DegradedState, service.DegradedPercentage, service.TimeoutTimeSpan);
            WriteStatusToConsole(service.State, service.DegradedState, service.DegradedPercentage);

            Thread.Sleep(TimeSpan.FromSeconds(10));

            service.Run(() => Utils.SimulateServiceCall(() => { return new object(); }, ServiceRunStatus.Ok));

            WriteLogToConsole(log, service.FriendlyName, service.State, service.DegradedState, service.DegradedPercentage, service.TimeoutTimeSpan);
            WriteStatusToConsole(service.State, service.DegradedState, service.DegradedPercentage);

            service.Run(() => Utils.SimulateServiceCall(() => { return new object(); }, ServiceRunStatus.Ok));

            WriteLogToConsole(log, service.FriendlyName, service.State, service.DegradedState, service.DegradedPercentage, service.TimeoutTimeSpan);
            WriteStatusToConsole(service.State, service.DegradedState, service.DegradedPercentage);

            Console.ReadKey();

        }

        private static void WriteStatusToConsole(ServiceState state, ServiceDegradationState degradationState, int degradationPercentage)
        {
            if (state == ServiceState.Open)
            {
                Console.WriteAscii($"{degradationPercentage}% {degradationState}", Color.Red);
            }
            else if (state == ServiceState.HalfOpen)
            {
                Console.WriteAscii($"{degradationPercentage}% {degradationState}", Color.Yellow);
            }
            else
            {
                Console.WriteAscii($"100% {degradationState}", Color.Green);
            }
        }

        private static void WriteLogToConsole(ILogger log, string FriendlyName, ServiceState state, ServiceDegradationState degradationState, int degradationPercentage, TimeSpan span)
        {
            if (state == ServiceState.Open)
                log.Error($"Service {FriendlyName} has degrated to {degradationState} with {degradationPercentage} % severity. Cannot be called for another {span.TotalMinutes}  minutes.");
            else if (state == ServiceState.HalfOpen)
                log.Error($"Service {FriendlyName} has degrated to {degradationState} with {degradationPercentage} % severity. Try again.");
            else
                log.Information($"Service {FriendlyName} is currently in optimal state.");
        }
    }
}
