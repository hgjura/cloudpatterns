using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;

namespace SchedulerAgentSupervisor
{
    public class OrderProcessor
    {

        private static readonly Lazy<List<Order>> _orders = new Lazy<List<Order>>(() => new List<Order>());

        private MessageSender sender;
        //connection string to the Azure Service Bus namespace           
        private string _connectionString = ConfigurationManager.ConnectionStrings["AzureServiceBusConnectionString"].ConnectionString;
        //name of the queue where the messages will be send and multiple consumers will read from 
        private string _topicName = ConfigurationManager.AppSettings["QueueName"];
        //will use this to make the multi-threading of the many consumer threads work
        private SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static List<Order> Orders { get => _orders.Value; }


        public event EventHandler<OrderProcessingEventArgs> OrderProcessingChanged;
        protected virtual void OnOrderProcessingChanged(OrderProcessingEventArgs e) => OrderProcessingChanged?.Invoke(this, e);


        public void AddNewOrderForProcessing(Order newOrder)
        {
            Orders.Add(newOrder);
        }

        public void AddNewOrderForProcessing(Order[] newOrders)
        {
            Orders.AddRange(newOrders);
        }

        public void StartPorcessing()
        {
            foreach (var item in Orders)
            {
                input_filter(item);
            }

            var tasks = new List<Task>();
            tasks.AddRange(output_filter_New());
            tasks.AddRange(output_filter_AllInProcessStatuses());
            tasks.AddRange(output_filter_Delivered());
            tasks.AddRange(output_filter_OrderClosed());

            Task.WaitAll(tasks.ToArray());
        }

        public OrderProcessor(string sb_connectionstring, string sb_topicname)
        {
            sender = new MessageSender(_connectionString, _topicName);           
        }

        #region Filters

        private void input_filter(Order order)
        {
            //insert new order into filter
            var m1 = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(order)))
            {
                MessageId = $"Order-{order.Id}-{DateTime.Now}-{Guid.NewGuid()}", //generating a unique id 
                Label = order.Id.ToString()
            };

            m1.UserProperties.Add("OrderStatus", order.Status.ToString());
            sender.SendAsync(m1).GetAwaiter().GetResult();
        }

        private List<Task> output_filter_New()
        {
            //process new orders and pass them along to the next filter 
            //next filter will be: input_filter_AllInProcessStatuses, which handles all transitory statuses, except the final statuses Closed (when an order is canceled) and Delivered (when an order is delivered)
            //I have chosen to use one filter for all transitory states of the order (when order is in progress) not for a particular reason, but simply to save some time and not to make the code overly complex
            //in a real life example there should be a filter for each transitory state
            //and, obviously, for each filter to be effective they should reside in independent resources such as Azure Functions, Worker Roles, or VMs (and not use threads as I am doing here, which is only for illustration purposes)
            // each Task/Thread should be replaced by an independent resource, like an Azure Function

            List<Task> processors = new List<Task>();

            for (var i = 0; i < 2; i++)
            {
                var t = Task.Run(() =>
                {
                    var receiver = new MessageReceiver(_connectionString, EntityNameHelper.FormatSubscriptionPath(_topicName, "NewOrders"));

                    while (true)
                    {
                        try
                        {
                            var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                            if (message != null)
                            {
                                //this line deserializes the message back to an Order type
                                var order = JsonConvert.DeserializeObject<Order>(Encoding.UTF8.GetString(message.Body));

                                _semaphore.Wait();
                                try
                                {
                                    //this line prints the message to console. 
                                    //It simulates some processing (it adds a couple of seconds of wait time), and
                                    //passes the order along to the next filter for further processing
                                    Task.Delay(0 * 1000).GetAwaiter().GetResult();

                                    Utils.ConsoleWriteOrder(order);

                                    var old_status = order.Status;

                                    order.Status = Utils.GetNextOrderStatus(order.Status);

                                    this.input_filter(order);

                                    OnOrderProcessingChanged(new OrderProcessingEventArgs() { Order = order, OldStatus = old_status, NewStatus = order.Status });

                                    receiver.CompleteAsync(message.SystemProperties.LockToken);

                                }
                                finally
                                {
                                    _semaphore.Release();
                                }

                                
                            }
                            else
                            {
                                // We have reached the end of the log.
                                break;
                            }
                        }
                        catch (ServiceBusException e)
                        {
                            if (!e.IsTransient)
                            {
                                Console.WriteLine(e.Message, Color.Red);
                                throw;
                            }
                        }
                    }
                });
                processors.Add(t);
            }

            return processors;
        }

        private List<Task> output_filter_AllInProcessStatuses()
        {
            //process new orders and pass them along to the next filter 
            //next filter will be: input_filter_AllInProcessStatuses, which handles all transitory statuses, except the final statuses Closed (when an order is canceled) and Delivered (when an order is delivered)
            //I have chosen to use one filter for all transitory states of the order (when order is in progress) not for a particular reason, but simply to save some time and not to make the code overly complex
            //in a real life example there should be a filter for each transitory state
            //and, obviously, for each filter to be effective they should reside in independent resources such as Azure Functions, Worker Roles, or VMs (and not use threads as I am doing here, which is only for illustration purposes)
            // each Task/Thread should be replaced by an independent resource, like an Azure Function

            List<Task> processors = new List<Task>();

            for (var i = 0; i < 10; i++)
            {
                var t = Task.Run(() =>
                {
                    var receiver = new MessageReceiver(_connectionString, EntityNameHelper.FormatSubscriptionPath(_topicName, "InProgressOrders"));

                    while (true)
                    {
                        try
                        {
                            var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                            if (message != null)
                            {
                                //this line deserializes the message back to an Order type
                                var order = JsonConvert.DeserializeObject<Order>(Encoding.UTF8.GetString(message.Body));

                                _semaphore.Wait();
                                try
                                {
                                    //this line prints the message to console. 
                                    //It simulates some processing (it adds a couple of seconds of wait time), and
                                    //passes the order along to the next filter for further processing
                                    Task.Delay(new Random().Next(0,3) * 1000).GetAwaiter().GetResult();

                                    Utils.ConsoleWriteOrder(order);

                                    var old_status = order.Status;

                                    order.Status = Utils.GetNextOrderStatus(order.Status);

                                    this.input_filter(order);

                                    OnOrderProcessingChanged(new OrderProcessingEventArgs() { Order = order, OldStatus = old_status, NewStatus = order.Status });

                                    receiver.CompleteAsync(message.SystemProperties.LockToken);
                                }
                                finally
                                {
                                    _semaphore.Release();
                                }

                            }
                            else
                            {
                                // We have reached the end of the log.
                                break;
                            }
                        }
                        catch (ServiceBusException e)
                        {
                            if (!e.IsTransient)
                            {
                                Console.WriteLine(e.Message, Color.Red);
                                throw;
                            }
                        }
                    }
                });
                processors.Add(t);
            }

            return processors;
        }

        private List<Task> output_filter_OrderClosed()
        {
            List<Task> processors = new List<Task>();

            for (var i = 0; i < 1; i++)
            {
                var t = Task.Run(() =>
                {
                    var receiver = new MessageReceiver(_connectionString, EntityNameHelper.FormatSubscriptionPath(_topicName, "ClosedOrders"));

                    while (true)
                    {
                        try
                        {
                            var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                            if (message != null)
                            {
                                //this line deserializes the message back to an Order type
                                var order = JsonConvert.DeserializeObject<Order>(Encoding.UTF8.GetString(message.Body));

                                _semaphore.Wait();
                                try
                                {
                                    //this line prints the message to console. 
                                    //It simulates some processing (it adds a couple of seconds of wait time), and
                                    //passes the order along to the next filter for further processing

                                    Task.Delay(0 * 1000).GetAwaiter().GetResult();

                                    Utils.ConsoleWriteOrder(order);

                                    OnOrderProcessingChanged(new OrderProcessingEventArgs() { Order = order, OldStatus = order.Status, NewStatus = order.Status });

                                    receiver.CompleteAsync(message.SystemProperties.LockToken);

                                }
                                finally
                                {
                                    _semaphore.Release();
                                }
                            }
                            else
                            {
                                // We have reached the end of the log.
                                break;
                            }
                        }
                        catch (ServiceBusException e)
                        {
                            if (!e.IsTransient)
                            {
                                Console.WriteLine(e.Message, Color.Red);
                                throw;
                            }
                        }
                    }
                });
                processors.Add(t);
            }

            return processors;
        }

        private List<Task> output_filter_Delivered()
        {
            List<Task> processors = new List<Task>();

            for (var i = 0; i < 1; i++)
            {
                var t = Task.Run(() =>
                {
                    var receiver = new MessageReceiver(_connectionString, EntityNameHelper.FormatSubscriptionPath(_topicName, "DeliveredOrders"));

                    while (true)
                    {
                        try
                        {
                            var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                            if (message != null)
                            {
                                //this line deserializes the message back to an Order type
                                var order = JsonConvert.DeserializeObject<Order>(Encoding.UTF8.GetString(message.Body));

                                _semaphore.Wait();
                                try
                                {
                                    //this line prints the message to console. 
                                    //It simulates some processing (it adds a couple of seconds of wait time), and
                                    //passes the order along to the next filter for further processing
                                    Task.Delay(0 * 1000).GetAwaiter().GetResult();

                                    Utils.ConsoleWriteOrder(order);                                    

                                    OnOrderProcessingChanged(new OrderProcessingEventArgs() { Order = order, OldStatus = order.Status, NewStatus = order.Status });

                                    receiver.CompleteAsync(message.SystemProperties.LockToken);

                                }
                                finally
                                {
                                    _semaphore.Release();
                                }
                            }
                            else
                            {
                                // We have reached the end of the log.
                                break;
                            }
                        }
                        catch (ServiceBusException e)
                        {
                            if (!e.IsTransient)
                            {
                                Console.WriteLine(e.Message, Color.Red);
                                throw;
                            }
                        }
                    }
                });
                processors.Add(t);
            }

            return processors;
        }

        #endregion

    }

    public class OrderProcessingEventArgs : EventArgs
    {
        public Order Order { get; set; }
        public OrderStatus OldStatus { get; set; }
        public OrderStatus NewStatus { get; set; }
    }
}
