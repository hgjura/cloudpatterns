using System;
using System.Collections.Generic;
using System.Text;

namespace SchedulerAgentSupervisor
{
    public class Order
    {
        public int Id { get; set; }
        public OrderStatus Status { get; set; }
    }
}
