using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alba.CsConsoleFormat;
using Alba.CsConsoleFormat.Fluent;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;
using Newtonsoft.Json;
using Serilog;

namespace PipesAndFilters
{
    public static class Utils
    {
        #region Console helper functions
        static int[] cColors = {
                        0x000080, //DarkBlue = 1
                        0x008000, //DarkGreen = 2
                        0x008080, //DarkCyan = 3
                        0x800000, //DarkRed = 4
                        0x800080, //DarkMagenta = 5
                        0x808000, //DarkYellow = 6

                        0x808080, //DarkGray = 8
                        0x0000FF, //Blue = 9
                        0x00FF00, //Green = 10
                        0x00FFFF, //Cyan = 11
                        0xFF0000, //Red = 12
                        0xFF00FF, //Magenta = 13
                        0xFFFF00, //Yellow = 14
                    };
        public static Color RandomColor()
        {
            return Color.FromArgb(cColors[new Random().Next(0, cColors.Length-1)]);
        }
        public static Color GetColor(int X)
        {
            return X >= cColors.Length ? Color.FromArgb(cColors[cColors.Length % X]) : Color.FromArgb(cColors[X]);
        }
        
        public static void ConsoleWriteHeader(string Header, ConsoleColor Color)
        {
            var b = new Border
            {
                Stroke = LineThickness.Single,
                Align = Align.Left
            };
            b.Children.Add(new Span($" {Header} ") { Color = Color });
            ConsoleRenderer.RenderDocument(new Document(b));
        }
        public static void ConsoleWriteOrder(Order order)
        {
            Colors.WriteLine($"Order: {order.Id}".White(), new Alba.CsConsoleFormat.Span($" -- Status: {order.Status}.") { Color = (ConsoleColor)(int)order.Status });
        }
        #endregion

        public static Task GetInputFilterTasks(Order[] orders, MessageSender sender)
        {
            var t = Task.Run(() =>
            {
                foreach (var order in orders)
                {
                    var m1 = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(order)))
                    {
                        MessageId = $"Order-{order.Id}-{DateTime.Now}-{Guid.NewGuid()}", //generating a unique id 
                        Label = order.Id.ToString()
                    };

                    m1.UserProperties.Add("OrderStatus", order.Status.ToString());
                    sender.SendAsync(m1);
                }
            });

            return t;
        }

        public static Task[] GetOutputFilterTasks(string sb_connectionstring, string sb_topicname, string sb_subscriptionname, int simulatedthreadsnumber, int sumulatedthreaddelayinseconds, SemaphoreSlim semaphore, MessageSender sender)
        {
            List<Task> processors = new List<Task>();
            
            for (var i = 0; i < simulatedthreadsnumber; i++)
            {
                var t = Task.Run(() =>
                {
                    var receiver = new MessageReceiver(sb_connectionstring, EntityNameHelper.FormatSubscriptionPath(sb_topicname, sb_subscriptionname));

                    while (true)
                    {
                        try
                        {
                            var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                            if (message != null)
                            {
                                //this line deserializes the message back to an Order type
                                var order = JsonConvert.DeserializeObject<Order>(Encoding.UTF8.GetString(message.Body));

                                semaphore.Wait();
                                try
                                {
                                    //this line prints the message to console. 
                                    //It simulates some processing (it adds a couple of seconds of wait time), and
                                    //passes the order along to the next filter for further processing
                                    Task.Delay(sumulatedthreaddelayinseconds * 1000).GetAwaiter().GetResult();

                                    Utils.ConsoleWriteOrder(order);

                                    if (order.Status != OrderStatus.Delivered && order.Status != OrderStatus.Closed)
                                    {
                                        order.Status = Utils.GetNextOrderStatus(order.Status);

                                        //insert order to the next filter
                                        Utils.GetInputFilterTasks(new Order[] { order }, sender).GetAwaiter().GetResult();
                                    }

                                }
                                finally
                                {
                                    semaphore.Release();
                                }

                                receiver.CompleteAsync(message.SystemProperties.LockToken);
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

            return processors.ToArray();
        }


        public static OrderStatus GetNextOrderStatus(OrderStatus cuurentstatus)
        {
            var ret = OrderStatus.Uknnown;

            switch (cuurentstatus)
            {
                case OrderStatus.New:
                    ret = OrderStatus.PaymentPending;
                    break;
                case OrderStatus.PaymentPending:
                    ret = OrderStatus.PaymentReceived;
                    break;
                case OrderStatus.PaymentReceived:
                    ret = OrderStatus.VerificationPending;
                    break;
                case OrderStatus.VerificationPending:
                    ret = OrderStatus.VerificationInProcess;
                    break;
                case OrderStatus.VerificationInProcess:
                    ret = OrderStatus.CheckInProgress;
                    break;
                case OrderStatus.CheckInProgress:
                    ret = OrderStatus.Checked;
                    break;
                case OrderStatus.Checked:
                    ret = OrderStatus.OrderReceiving;
                    break;
                case OrderStatus.OrderReceiving:
                    ret = OrderStatus.OrderBeingProcessed;
                    break;
                case OrderStatus.OrderBeingProcessed:
                    ret = OrderStatus.Shipped;
                    break;
                case OrderStatus.Shipped:
                    ret = OrderStatus.InDelivery;
                    break;
                case OrderStatus.InDelivery:
                    ret = OrderStatus.Delivered;
                    break;


                case OrderStatus.RefundBeingProcessed:
                    ret = OrderStatus.Refunded;
                    break;
                case OrderStatus.Refunded:
                    ret = OrderStatus.Closed;
                    break;
                default:
                    ret = cuurentstatus;
                    break;
            }

            return ret;
        }
    }
}
