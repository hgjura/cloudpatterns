using Serilog;
using System;

namespace CircuitBreaker
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing circuit breaker pattern");
            Console.WriteLine();


            var log = new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger();

            var auths = new AuthService(log, "AzureADAuthService", ServiceDegradationWeight.Full, TimeSpan.FromMinutes(10), 3);


            var result = auths.Run(() => { return ServiceRunStatus.Failure; });
            auths.Run(() => { return ServiceRunStatus.Failure; });
            auths.Run(() => { return ServiceRunStatus.Failure; });
            auths.Run(() => { return ServiceRunStatus.Failure; });

            if (auths.State == ServiceState.Open)            
                log.Error($"Service {auths.FriendlyName} has degrated to {auths.DegradedState} with {auths.DegradationSeverity * 100} % severity. Cannot be called for another {auths.TimeoutTimeSpan.TotalMinutes}  minutes.");
            else if (auths.State == ServiceState.HalfOpen)
                log.Error($"Service {auths.FriendlyName} has degrated to {auths.DegradedState} with {auths.DegradationSeverity * 100} % severity. Try again.");
            else
                log.Information($"Service {auths.FriendlyName} is currently in optimal state.");


        }
    }


   
}
