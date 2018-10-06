using System;
using System.Drawing;
using System.Threading;
using System.Collections.Generic;
using Serilog;
using System.Configuration;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Azure.ServiceBus;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using Console = Colorful.Console;
using Alba.CsConsoleFormat;

namespace PriorityQueue
{
    class Program
    {
        static void Main(string[] args)
        {

            Utils.ConsoleWriteHeader("Testing Priority Queues Pattern", ConsoleColor.Red);

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
            //│  CREATING PRIORITY QUEUES   │
            //└─────────────────────────────┘



            //using named parameters here to make it clear all properties needed to set up the queue. note that these are not all parameters needed for topic/subscription configuration. 
            //I am only using the ones I use/change more often depending on circumstances and usage.
            //check descriptions in the Utils/Deploy.cs class for 
            if (!deploy.ExistsTopic(_topicName))
            {
                deploy.CreateTopic(TopicName: _topicName, AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, MaxSize: 1024, EnableBatch: true, EnablePartitioning: false, EnableExpress: false, RequireDubsDetection: true, DuplicateDetectionTimeWindow: 1, EnforceMessageOrdering: false);

                deploy.CreateSubscription(TopicName: _topicName, SubscriptionName: "Priority1", AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, EnableBatch: true, LockDuration: 30, MaxDeliveryCount: 10, EnableDLOnFilterEvalErrors: true, EnableDLOnMessageExpiration: false, RequiresSession: false);
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "Priority1", RuleName: "Priority", SqlFilter: "Priority = 1");

                deploy.CreateSubscription(TopicName: _topicName, SubscriptionName: "Priority2", AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, EnableBatch: true, LockDuration: 30, MaxDeliveryCount: 10, EnableDLOnFilterEvalErrors: true, EnableDLOnMessageExpiration: false, RequiresSession: false);
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "Priority2", RuleName: "Priority", SqlFilter: "Priority = 2");

                deploy.CreateSubscription(TopicName: _topicName, SubscriptionName: "Priority3", AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, EnableBatch: true, LockDuration: 30, MaxDeliveryCount: 10, EnableDLOnFilterEvalErrors: true, EnableDLOnMessageExpiration: false, RequiresSession: false);
                deploy.CreateFilter(TopicName: _topicName, SubscriptionName: "Priority3", RuleName: "Priority", SqlFilter: "Priority = 3");
            }



            //┌────────────────────────┐
            //│  GENERATING MESSAGES   │
            //└────────────────────────┘


            //in this part will simulate multiple message generators that will send messages to the service bus in an asynchronous manner
            //note 1: the code sample below uses a series of Tasks to simulate the sending in the multi-threaded way. this is for illustration purposes only.
            //in real life scenarios, this will be accomplished by different senders, such as ent. systems and applications, devices (IoT type scenarios), or Azure Functions that would pump messages into the service bus. 
            //there is no real gains or benefits in sending messages to the queue in multiple threads from a single machine/service
            //note 2: for simplicity, the code below does not make use of async/await functionality. the service bus sdk is build with asynchronicity in mind, and you should use is it whenever possible.

            var processors = new List<Task>();




            //building the senders. will create 10 separate senders that will simulate 10 different systems/applications sending messages to the queue simultaneously.
            //each sender will create multiple messages of different priorities
            //priority 1: financial quotes from our fictional financial instrument; priority 2, fictitious trades from the same instrument; and priority 3, various customs logs to be used for audit purposes 
            for (var i = 0; i < 10; i++)
            {

                var sendTask = Task.Run(() =>
                {
                    // generate some messages and place them in the queue
                    var sender = new MessageSender(_connectionString, _topicName);


                    //the service bus Message object needs a byte[] to build itself. as such you can store bits of streams, or any serializeable object in it
                    //here, as part of example, will create an anynymous type that will store data about the price and type of a financial instrument. 
                    //will aslo create two other objects, withh different type of priority, one for a trade made on the financila intrument, and one of lower priority to log/audit the transaction
                    //it will take two serializations to store this type of object in the message: a) serailizing the object as json, using Newtonsoft serializer,
                    //and b) serializing that json result into a byte[] using UTF8 encoding. 
                    for (var i2 = 0; i2 < 10; i2++)
                    {
                        Message log;
                        _semaphore.Wait();
                        try
                        {
                            log = new Message(Encoding.UTF8.GetBytes("LOG: Getting quote."));
                            log.UserProperties.Add("Priority", 3);
                            sender.SendAsync(log).GetAwaiter().GetResult();

                            var quote = new { Type = "Quote", Name = "MSFT", Price = new Random().Next(100, 150) };

                            log = new Message(Encoding.UTF8.GetBytes("LOG: Saving quote."));
                            log.UserProperties.Add("Priority", 3);
                            sender.SendAsync(log).GetAwaiter().GetResult();

                            var m1 = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(quote)))
                            {
                                MessageId = $"Quote-MSFT-{DateTime.Now}-{Guid.NewGuid()}" //generating a unique id 
                            };

                            m1.UserProperties.Add("Priority", 1);
                            sender.SendAsync(m1).GetAwaiter().GetResult();
                            Console.WriteLine($"SENDING >>>> PRIORITY 1 - Message Id: {m1.MessageId}. Send to the queue.", Color.DarkRed);

                            log = new Message(Encoding.UTF8.GetBytes("LOG: quote saved."));
                            log.UserProperties.Add("Priority", 3);
                            sender.SendAsync(log).GetAwaiter().GetResult();



                            log = new Message(Encoding.UTF8.GetBytes("LOG: Making trade."));
                            log.UserProperties.Add("Priority", 3);
                            sender.SendAsync(log).GetAwaiter().GetResult();

                            var trade = new { Type = "Trade", Name = "MSFT", Price = new Random().Next(100, 150), Activity="Buy", Timestamp=DateTime.Now };

                            log = new Message(Encoding.UTF8.GetBytes("LOG: Saving trade."));
                            log.UserProperties.Add("Priority", 3);
                            sender.SendAsync(log).GetAwaiter().GetResult();

                            var m2 = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(trade)))
                            {
                                MessageId = $"Trade-MSFT-{DateTime.Now}-{Guid.NewGuid()}" //generating a unique id 
                            };

                            m2.UserProperties.Add("Priority", 2);
                            sender.SendAsync(m2).GetAwaiter().GetResult();
                            Console.WriteLine($"SENDING >>>> PRIORITY 2 - Message Id: {m2.MessageId}. Send to the queue.", Color.DarkSlateBlue);

                            log = new Message(Encoding.UTF8.GetBytes("LOG: trade saved."));
                            log.UserProperties.Add("Priority", 3);
                            sender.SendAsync(log).GetAwaiter().GetResult();

                        }
                        finally
                        {
                            _semaphore.Release();
                        }

                    }
                });

                processors.Add(sendTask);
            }



            //┌───────────────────────┐
            //│  CONSUMING MESSAGES   │
            //└───────────────────────┘

            //in this part will create a multiple consumers that will consume messages from the service bus.
            //will create 3 consumers to process priority 1 messages; two consumers to process priority 2 and 1 consumer for priority 3 messages.
            //note 1: for simplicity, the code below does not make use of async/await functionality. the service bus sdk is build with asynchronicity in mind, and you should use is it whenever possible.
            //in fact, a MessageReceiver class will open a tunnel to the service bus queue and will process messages in any async way. it will keep that tunnel open for 60 seconds, even though theer may not be any messages in the queue. 
            //it just waits for any incoming messages. it closes the tunnel after 60 seconds if there are not messages. 

            // simulate multiple consumers. each task is a consumer that process messages independently. 
            //it will process the message and log the result in the console, in a different color for each priority, for distinguishing with other consumers

            for (var i = 0; i < 3; i++)
            {
                var t = Task.Run(() =>
            {
                var receiver = new MessageReceiver(_connectionString, EntityNameHelper.FormatSubscriptionPath(_topicName, "Priority1"));

                while (true)
                {
                    try
                    {
                        var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                        if (message != null)
                        {
                            //this line deserailizes the message back to a dynamic type
                            var result = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(message.Body));

                            //this line simlates the processing of the message and writes the content in a string so we can print it to console 
                            var s = $"PROCESSING >>>> MessageId = {message.MessageId}, \nSequenceNumber = {message.SystemProperties.SequenceNumber}, \nEnqueuedTimeUtc = {message.SystemProperties.EnqueuedTimeUtc},\nContent: {result}\n";

                            _semaphore.Wait();
                            try
                            {
                                //this line prints the message to console. 1st line writes a multiline content of all the message (it is hard to read in console), 2nd line writes only a piece of info: the SequenceNumber (easy to read inconsole).
                                //in difference from the code above where the sender prints in console in various colors (black and white excluded), this line prints in White so the processing can be distinguished by the sending. 

                                //Console.WriteLine(s, Color.White);
                                Console.WriteLine($"PROCESSING >>>> Message Seq: {message.SystemProperties.SequenceNumber}", Color.DarkRed);

                            }
                            finally
                            {
                                _semaphore.Release();
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

            for (var i = 0; i < 2; i++)
            {
                var t = Task.Run(() =>
                {
                    var receiver = new MessageReceiver(_connectionString, EntityNameHelper.FormatSubscriptionPath(_topicName, "Priority2"));

                    while (true)
                    {
                        try
                        {
                            var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                            if (message != null)
                            {
                                //this line deserailizes the message back to a dynamic type
                                var result = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(message.Body));

                                //this line simlates the processing of the message and writes the content in a string so we can print it to console 
                                var s = $"PROCESSING >>>> MessageId = {message.MessageId}, \nSequenceNumber = {message.SystemProperties.SequenceNumber}, \nEnqueuedTimeUtc = {message.SystemProperties.EnqueuedTimeUtc},\nContent: {result}\n";

                                _semaphore.Wait();
                                try
                                {
                                    //this line prints the message to console. 1st line writes a multiline content of all the message (it is hard to read in console), 2nd line writes only a piece of info: the SequenceNumber (easy to read inconsole).
                                    //in difference from the code above where the sender prints in console in various colors (black and white excluded), this line prints in White so the processing can be distinguished by the sending. 

                                    //Console.WriteLine(s, Color.White);
                                    Console.WriteLine($"PROCESSING >>>> Message Seq: {message.SystemProperties.SequenceNumber}", Color.DarkSlateBlue);

                                }
                                finally
                                {
                                    _semaphore.Release();
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

            for (var i = 0; i < 1; i++)
            {
                var t = Task.Run(() =>
                {
                    var receiver = new MessageReceiver(_connectionString, EntityNameHelper.FormatSubscriptionPath(_topicName, "Priority3"));

                    while (true)
                    {
                        try
                        {
                            var message = receiver.ReceiveAsync().GetAwaiter().GetResult();

                            if (message != null)
                            {
                                //this line deserailizes the message back to a dynamic type
                                var result = Encoding.UTF8.GetString(message.Body);

                                //this line simlates the processing of the message and writes the content in a string so we can print it to console 
                                var s = $"PROCESSING >>>> MessageId = {message.MessageId}, \nSequenceNumber = {message.SystemProperties.SequenceNumber}, \nEnqueuedTimeUtc = {message.SystemProperties.EnqueuedTimeUtc},\nContent: {result}\n";

                                _semaphore.Wait();
                                try
                                {
                                    //this line prints the message to console. 1st line writes a multiline content of all the message (it is hard to read in console), 2nd line writes only a piece of info: the SequenceNumber (easy to read inconsole).
                                    //in difference from the code above where the sender prints in console in various colors (black and white excluded), this line prints in White so the processing can be distinguished by the sending. 

                                    //Console.WriteLine(s, Color.White);
                                    Console.WriteLine($"PROCESSING >>>> Message Seq: {message.SystemProperties.SequenceNumber}", Color.White);

                                }
                                finally
                                {
                                    _semaphore.Release();
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

            //process messages by all senders and the one consumer
            Task.WaitAll(processors.ToArray());

            Console.ReadKey();
        }


    }

    
}
