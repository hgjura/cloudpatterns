using System;
using System.Collections.Generic;
using System.Text;

namespace CircuitBreaker
{
    public static class Utils
    {
        public static object SimulateServiceCall(Func<object> call)
        {
            var a = call();

            if ((ServiceRunStatus)a == ServiceRunStatus.Failure)
            {
                throw new Exception("Service failed.");
            }

            if ((ServiceRunStatus)a == ServiceRunStatus.Timeout)
            {
                throw new Exception("Service timeout.");
            }

            return a;
        }
    }

    public enum ServiceRunStatus { Ok, Failure, Timeout };
}
