using Serilog;
using System;
using System.Configuration;
using System.Threading;
using Alba.CsConsoleFormat;
using Alba.CsConsoleFormat.Fluent;
using System.Drawing;
using System.Collections.Generic;

namespace SchedulerAgentSupervisor
{
    class Program
    {
        static void Main(string[] args)
        {
            Utils.ConsoleWriteHeader("Testing Pipes And Filters Pattern", ConsoleColor.Red);

            //connection string to the Azure Service Bus namespace           
            string _connectionString = ConfigurationManager.ConnectionStrings["AzureServiceBusConnectionString"].ConnectionString;

            //name of the queue where the messages will be send and multiple consumers will read from 
            string _topicName = ConfigurationManager.AppSettings["QueueName"];

            //log where will log all activities and will simulate writing and consuming of messages
            //by default, will use a ColoredConsole log from https://serilog.net
            //can change this to any type of log supported by serilog sinks. also, it supported by native logging of Azure Function and Application Insights
            var _log = new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger();

            //will use this to make the multi-threading of the many consumer threads work
            var _semaphore = new SemaphoreSlim(1, 1);


            //starting creation of the queue if it doesn't exists
            //queues and topics/subscriptions of Azure Service Bus require many parameters to be set up correctly
            //avoid doing the set up manually; use and automated process instead, like a json ARM template, a powershell or azure cli script; 
            //or build some c# helper file/class as done here
            var deploy = new Deploy(_log);

            //┌─────────────────────────────┐
            //│  CREATING FILTER TOPICS     │
            //└─────────────────────────────┘

            //using named parameters here to make it clear all properties needed to set up the queue. note that these are not all parameters needed for topic/subscription configuration. 
            //I am only using the ones I use/change more often depending on circumstances and usage.
            //check descriptions in the Utils/Deploy.cs class for 
            if (!deploy.ExistsTopic(_topicName))
            {
                deploy.CreateTopic(TopicName: _topicName, AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, MaxSize: 1024, EnableBatch: true, EnablePartitioning: false, EnableExpress: false, RequireDubsDetection: true, DuplicateDetectionTimeWindow: 1, EnforceMessageOrdering: false);

                deploy.CreateSubscription(TopicName: _topicName, SubscriptionName: "NewOrders", AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, EnableBatch: true, LockDuration: 30, MaxDeliveryCount: 10, EnableDLOnFilterEvalErrors: true, EnableDLOnMessageExpiration: false, RequiresSession: false);
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "NewOrders", RuleName: "OrderStatus", SqlFilter: "OrderStatus = 'New'");

                deploy.CreateSubscription(TopicName: _topicName, SubscriptionName: "InProgressOrders", AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, EnableBatch: true, LockDuration: 30, MaxDeliveryCount: 10, EnableDLOnFilterEvalErrors: true, EnableDLOnMessageExpiration: false, RequiresSession: false);
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "PaymentPending", SqlFilter: "OrderStatus = 'PaymentPending'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "PaymentReceived", SqlFilter: "OrderStatus = 'PaymentReceived'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "VerificationPending", SqlFilter: "OrderStatus = 'VerificationPending'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "VerificationInProcess", SqlFilter: "OrderStatus = 'VerificationInProcess'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "CheckInProgress", SqlFilter: "OrderStatus = 'CheckInProgress'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "Checked", SqlFilter: "OrderStatus = 'Checked'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "OrderReceiving", SqlFilter: "OrderStatus = 'OrderReceiving'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "OrderBeingProcessed", SqlFilter: "OrderStatus = 'OrderBeingProcessed'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "Shipped", SqlFilter: "OrderStatus = 'Shipped'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "InDelivery", SqlFilter: "OrderStatus = 'InDelivery'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "RefundBeingProcessed", SqlFilter: "OrderStatus = 'RefundBeingProcessed'");
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "InProgressOrders", RuleName: "Refunded", SqlFilter: "OrderStatus = 'Refunded'");

                deploy.CreateSubscription(TopicName: _topicName, SubscriptionName: "DeliveredOrders", AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, EnableBatch: true, LockDuration: 30, MaxDeliveryCount: 10, EnableDLOnFilterEvalErrors: true, EnableDLOnMessageExpiration: false, RequiresSession: false);
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "DeliveredOrders", RuleName: "OrderStatusDelivered", SqlFilter: "OrderStatus = 'Delivered'");

                deploy.CreateSubscription(TopicName: _topicName, SubscriptionName: "ClosedOrders", AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, EnableBatch: true, LockDuration: 30, MaxDeliveryCount: 10, EnableDLOnFilterEvalErrors: true, EnableDLOnMessageExpiration: false, RequiresSession: false);
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "ClosedOrders", RuleName: "OrderStatusClosed", SqlFilter: "OrderStatus = 'Closed'");
            }


            var orders_tobe_processed = new List<Order>();

            for (int i = 0; i < 10; i++)
            {
                orders_tobe_processed.Add(new Order() { Id = i, Status = OrderStatus.New });
            }



            var op = new OrderProcessor(_connectionString, _topicName);
            op.OrderProcessingChanged += Op_OrderProcessingChanged;

            op.AddNewOrderForProcessing(orders_tobe_processed.ToArray());

            op.StartPorcessing();

            Console.ReadKey();
        }

        private static void Op_OrderProcessingChanged(object sender, OrderProcessingEventArgs e)
        {
            //Console.WriteLine($">>>{e.Order.Id} - {e.OldStatus} - {e.NewStatus} <<<");
        }
    }
}
