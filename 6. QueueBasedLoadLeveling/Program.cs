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

namespace QueueBasedLoadLeveleing
{
    class Program
    {
        static void Main(string[] args)
        {

            Utils.ConsoleWriteHeader("Testing Queue-based Load Leveling Pattern", ConsoleColor.Red);

            //connection string to the Azure Service Bus namespace           
            string _connectionString = ConfigurationManager.ConnectionStrings["AzureServiceBusConnectionString"].ConnectionString;

            //name of the queue where the messages wil be send and multiple consumers will read from 
            string _queueName = ConfigurationManager.AppSettings["QueueName"];

            //log where will log all activities and will simulate writing and consuming of messages
            //by default, will use a ColoredConsole log from https://serilog.net
            //can change this to anytype of log supported by serilog sinks. also, it supported by native logging of Azure Function and Application Insights
            var _log = new LoggerConfiguration().WriteTo.ColoredConsole().CreateLogger();
            
            //will use this to make the multithreading of the many consumer threads work
            var _semaphore = new SemaphoreSlim(1, 1);


            //starting creation of the queue if it doesnt exists
            //queues and topics/subscriptions of Azure Service Bus require many parameters to be set up correctly
            //avoid doing the set up manullay; use and automated process instead, like a json ARM template, a powershell or azure cli script; 
            //or build some c# helper file/class as done here
            var deploy = new Deploy(_log);

            //using named parameters here to make it clear all properties needed to set up teh queue. note that these are not all parameters needed for queue configuration. 
            //I am only using the ones I use/change more often depending on cirumstances and usage.
            //check descriptions in the Utils/Deploy.cs class for 
            if (!deploy.ExistsQueue(_queueName))
                deploy.CreateQueue(QueueName: _queueName, AutoDeleteOnIdleInDays: 365, DefaultTimeToLive: 10, MaxSize: 1024, EnableBatch: true, EnablePartitioning: false, EnableExpress: false, RequireDubsDetection: true, DuplicateDetectionTimeWindow: 1, EnforceMessageOrdering: false);


            //┌────────────────────────┐
            //│  GENERATING MESSAGES   │
            //└────────────────────────┘

            //in this part will simulate multiple message generators that will send messages to the service bus in an asynchrounous manner
            //note 1: the code sample below uses a series of Tasks to sumulate the sending in the multithreaded way. this is for illustration purposes only.
            //in real life scenarios, this will be accomplished by different senders, such as ent. systems and applications, devices (IoT type scenarios), or Azure Functions that would pump messages into the service bus. 
            //there is no real gains or benefits in sending messages to the queue in multiple threads from a single machine/service
            //note 2: for simplicity, the code below does not make use of async/await functionality. the service bus sdk is build with asynchronicity in mind, and you should use is it whenever possible.

            var senders = new List<Task>();

            //building the senders. will create 10 separate senders that will simulate 10 differend systems/applications sending messages to the queue simultaniously.
            for (var i = 0; i < 10; i++)
            {
                //this tries to get a different color for each thread that get spawn
                var c = Utils.GetColor(i);

                var sendTask = Task.Run(() =>
                {
                    // generate some messages and place them in the queue
                    var sender = new MessageSender(_connectionString, _queueName);

                    //the service bus Message object needs a byte[] to build itself. as such you can store bits of streams, or any serializeable object in it
                    //here, as part of example, will create an anynymous type that will store data about the price and type of a financial instrument
                    //it will take two serializations to store this type of object in the message: a) serailizing the object as json, using Newtonsoft serializer,
                    //and b) serializing that json result into a byte[] using UTF8 encoding. 
                    for (var i2 = 0; i2 < 10; i2++)
                    {

                        var MessageId = $"Quote-MSFT-{DateTime.Now}-{Guid.NewGuid()}"; //generating a unique id 
                        var m = new Message(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { Type = "Quote", Name = "MSFT", Price = new Random().Next(100, 150) })))
                        {
                            MessageId = MessageId
                        };

                        sender.SendAsync(m).GetAwaiter().GetResult();

                        _semaphore.Wait();
                        try
                        {
                            //this line prints the message to console. 1st line writes a multiline content of all the message (it is hard to read in console), 2nd line writes only a piece of info: the MessageId we gave to the message (easy to read inconsole).

                            //Console.WriteLine(s, Color.White);
                            Console.WriteLine($"SENDING >>>> Message Id: {MessageId}. Send to the queue.", c);

                        }
                        finally
                        {
                            _semaphore.Release();
                        }


                    }
                });

                senders.Add(sendTask);
            }


   
            //┌───────────────────────┐
            //│  CONSUMING MESSAGES   │
            //└───────────────────────┘

            //in this part will create a single consumer that will consume messages from the service bus in a load leveled fashion. regardsless of how many messages you send in the queue, 
            //and how many senders you generate (could be in the millions, in an IoT scenario) the consumer, or processor, will process all of them in a leveled pace.
            //note 1: for simplicity, the code below does not make use of async/await functionality. the service bus sdk is build with asynchronicity in mind, and you should use is it whenever possible.
            //in fact, a MessageReceiver class will open a tunnel to the service bus queue and will process messages in any async way. it will keep that tunnel open for 60 seconds, even though theer may not be any messages in the queue. 
            //it just waits for any incomeing messages. it closes the tunnel after 60 seconds if there are not messages. 

            // simluate multiple consumers. each task is a consumer that process messages independently. 
            //it will process the message and log the result in the console, in a different color, for distinguishing with other consumers
            var t = Task.Run(() =>
            {
                var receiver = new MessageReceiver(_connectionString, _queueName);

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
            senders.Add(t);

            //process messages by all senders and the one consumer
            Task.WaitAll(senders.ToArray());

            Console.ReadKey();
        }


    }

    
}
