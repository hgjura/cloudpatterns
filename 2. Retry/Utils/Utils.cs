using System;
using System.Collections.Generic;
using System.Text;

namespace Retry
{
    public static class Utils
    {
        public static object SimulateServiceCall(Func<object> call, ServiceRunStatus errorToThrow = ServiceRunStatus.Ok)
        {
            var a = call();

            if (errorToThrow == ServiceRunStatus.Failure)
            {
                throw new Exception("Service failed.");
            }

            if (errorToThrow == ServiceRunStatus.Timeout)
            {
                throw new Exception("Service timeout.");
            }

            return a;
        }
    }

    public enum ServiceRunStatus { Ok, Failure, Timeout };
}
