using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ServiceBus.Fluent;

using Serilog;
using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace PriorityQueue
{
    public class Deploy
    {
        IServiceBusNamespace _serviceBusNamespace;
        Microsoft.Azure.Management.ServiceBus.ServiceBusManagementClient _serviceBusManagementClient;
        ILogger _log;

        public Deploy(ILogger Logger)
        {
            var Deploy_ClientId = ConfigurationManager.AppSettings["Deploy_ClientId"];
            var Deploy_ClientSecret = ConfigurationManager.AppSettings["Deploy_ClientSecret"];
            var Deploy_TenantId = ConfigurationManager.AppSettings["Deploy_TenantId"];
            var Deploy_SubscriptionId = ConfigurationManager.AppSettings["Deploy_SubscriptionId"];
            var Deploy_ResourceGroupName = ConfigurationManager.AppSettings["Deploy_ResourceGroupName"];
            var Deploy_ServiceBusNamespaceName = ConfigurationManager.AppSettings["Deploy_ServiceBusNamespaceName"];

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(Deploy_ClientId, Deploy_ClientSecret,Deploy_TenantId, AzureEnvironment.AzureGlobalCloud);
            var serviceBusManager = ServiceBusManager.Authenticate(credentials, Deploy_SubscriptionId);

            _serviceBusNamespace = serviceBusManager.Namespaces.GetByResourceGroup(Deploy_ResourceGroupName, Deploy_ServiceBusNamespaceName);

            _serviceBusManagementClient = new Microsoft.Azure.Management.ServiceBus.ServiceBusManagementClient(credentials)
            {
                SubscriptionId = Deploy_SubscriptionId
            };


            _log = Logger;

        }

        public bool ExistsTopic(string TopicName)
        {
            var topics = _serviceBusNamespace.Topics.List();
            var topic = topics.FirstOrDefault(t => t.Name == TopicName.ToLower());

            return topic != null;
        }

        public bool ExistsQueue(string QueueName)
        {
            var queues = _serviceBusNamespace.Queues.List();
            var queue = queues.FirstOrDefault(t => t.Name == QueueName.ToLower());

            return queue != null;
        }
        public bool ExistsSubscription(string TopicName, string SubscriptionName)
        {
            return _serviceBusNamespace.Topics.List()?
                .FirstOrDefault(t => t.Name == TopicName)?
                .Subscriptions.List()?.FirstOrDefault(s => s.Name == SubscriptionName) 
                != null;
        }

        public void DeleteQueue(string QueueName)
        {
            if (ExistsQueue(QueueName))
                _serviceBusNamespace.Queues.DeleteByName(QueueName);
        }

        public void DeleteTopic(string TopicName)
        {
            if (ExistsTopic(TopicName))
                _serviceBusNamespace.Topics.DeleteByName(TopicName);
        }

        public void DeleteSubscription(string TopicName, string SubscriptionName)
        {
            if (ExistsTopic(TopicName) && ExistsSubscription(TopicName, SubscriptionName))
                _serviceBusNamespace.Topics.GetByName(TopicName).Subscriptions.DeleteByName(SubscriptionName);
        }

        public bool CreateQueue(string QueueName, int AutoDeleteOnIdleInDays = 365, int DefaultTimeToLive = 10, int MaxSize = 1000, bool EnableBatch = true, bool EnablePartitioning = true, bool EnableExpress = false, bool RequireDubsDetection = true, int DuplicateDetectionTimeWindow = 30, bool EnforceMessageOrdering = false)
        {
            var _r = false;

            try
            {
                _log.Information($"Creating Queue: {QueueName}.");

                // throw error if exists
                if (this.ExistsQueue(QueueName))
                {
                    throw new ApplicationException($"Queue {QueueName} already exists.");
                }

                var q = _serviceBusNamespace.Queues.Define(QueueName)
                    //This is actually a timespan which denotes how long a queue can stay alive once it is idle before it is automatically deleted.  The minimum time is 5mins.
                    .WithDeleteOnIdleDurationInMinutes(AutoDeleteOnIdleInDays * 1440)

                    //This dictates the time to live of a message in two scenarios 
                    //  1) if the message does not have its direct TimeToLive property value set or 
                    //  2) when the messages TimeToLive is greater than the queue’s DefaultMessageTimeToLive property.  
                    //  However, if the message’s TimeToLive value is lower, the message’s TimeToLive will be the time at which the message will expire.
                    .WithDefaultMessageTTL(TimeSpan.FromMinutes(DefaultTimeToLive))

                    //The total size of the queue.  The default is 1GB.  This is adversely affected by the overhead produced by the DuplicationDetectionHistoryTimeWindow.
                    .WithSizeInMB(MaxSize);

                if (!EnableBatch)
                    q.WithoutMessageBatching();

                //A partitioned queue or topic, on the other hand, is distributed across multiple nodes and messaging stores. Partitioned queues and topics not only yield a higher throughput than regular queues and topics, they also exhibit superior availability.
                if (EnablePartitioning)
                    q.WithPartitioning();
                else
                    q.WithoutPartitioning();


                //With express entities, if a message is sent to a queue or topic is, it is not immediately stored in the messaging store. Instead, the message is cached in memory. If a message remains in the queue for more than a few seconds, it is automatically written to stable storage, thus protecting it against loss due to an outage.
                if (EnableExpress)
                    q.WithExpressMessage();

                //Allows you to turn on message duplication detection.  
                //This works in conjunction with DuplicationDetectionHistoryTimeWindow.
                //You can specify a time period that the queue will retain message ID’s in order to carry out message duplication 
                //detection.  The time can be no greater than the maximum time a message can live on a queue which is 7 days.
                if (RequireDubsDetection)
                    q.WithDuplicateMessageDetection(TimeSpan.FromMinutes(DuplicateDetectionTimeWindow));

                q.Create();

                _r = true;

            }
            catch (Exception ex)
            {
                _log.Error($"Error while creating queue: {ex.Message}");
            }

            return _r;
        }
        public bool CreateTopic(string TopicName, int AutoDeleteOnIdleInDays = 365, int DefaultTimeToLive = 10, int MaxSize = 1000, bool EnableBatch = true, bool EnablePartitioning = true, bool EnableExpress = false, bool RequireDubsDetection = true, int DuplicateDetectionTimeWindow = 30, bool EnforceMessageOrdering = false)
        {
            var _r = false;

            try
            {
                _log.Information($"Creating topic: {TopicName}.");

                // throw error if exists
                if (this.ExistsTopic(TopicName))
                {
                    throw new ApplicationException($"Topic {TopicName} already exists.");
                }


                var q = _serviceBusNamespace.Topics.Define(TopicName)
                    //This is actually a timespan which denotes how long a queue can stay alive once it is idle before it is automatically deleted.  The minimum time is 5mins.
                    .WithDeleteOnIdleDurationInMinutes(AutoDeleteOnIdleInDays * 1440)

                    //This dictates the time to live of a message in two scenarios 
                    //  1) if the message does not have its direct TimeToLive property value set or 
                    //  2) when the messages TimeToLive is greater than the queue’s DefaultMessageTimeToLive property.  
                    //  However, if the message’s TimeToLive value is lower, the message’s TimeToLive will be the time at which the message will expire.
                    .WithDefaultMessageTTL(TimeSpan.FromMinutes(DefaultTimeToLive))

                    //The total size of the queue.  The default is 1GB.  This is adversely affected by the overhead produced by the DuplicationDetectionHistoryTimeWindow.
                    .WithSizeInMB(MaxSize);

                if (!EnableBatch)
                    q.WithoutMessageBatching();

                //A partitioned queue or topic, on the other hand, is distributed across multiple nodes and messaging stores. Partitioned queues and topics not only yield a higher throughput than regular queues and topics, they also exhibit superior availability.
                if (EnablePartitioning)
                    q.WithPartitioning();
                else
                    q.WithoutPartitioning();


                //With express entities, if a message is sent to a queue or topic is, it is not immediately stored in the messaging store. Instead, the message is cached in memory. If a message remains in the queue for more than a few seconds, it is automatically written to stable storage, thus protecting it against loss due to an outage.
                if (EnableExpress)
                    q.WithExpressMessage();

                //Allows you to turn on message duplication detection.  
                //This works in conjunction with DuplicationDetectionHistoryTimeWindow.
                //You can specify a time period that the queue will retain message ID’s in order to carry out message duplication 
                //detection.  The time can be no greater than the maximum time a message can live on a queue which is 7 days.
                if (RequireDubsDetection)
                    q.WithDuplicateMessageDetection(TimeSpan.FromMinutes(DuplicateDetectionTimeWindow));

                q.Create();

                _r = true;
            }
            catch (Exception ex)
            {
                _log.Error($"Error while creating topic: {ex.Message}");
            }

            return _r;
        }
        public bool CreateSubscription(String TopicName, string SubscriptionName, int AutoDeleteOnIdleInDays = 365, int DefaultTimeToLive = 10, bool EnableBatch = true, int LockDuration = 30, int MaxDeliveryCount = 10, bool EnableDLOnFilterEvalErrors = true, bool EnableDLOnMessageExpiration = false, bool RequiresSession = false)
        {
            var _r = false;

            try
            {
                _log.Information($"Creating subscription: {SubscriptionName}.");

                // throw error if exists
                if (this.ExistsSubscription(TopicName, SubscriptionName))
                {
                    throw new ApplicationException($"Subscription {SubscriptionName} already exists.");
                }

                if (ExistsTopic(TopicName))
                {
                    var q = _serviceBusNamespace.Topics.GetByName(TopicName).Subscriptions.Define(SubscriptionName)
                        //This is actually a timespan which denotes how long a queue can stay alive once it is idle before it is automatically deleted.  The minimum time is 5mins.
                        .WithDeleteOnIdleDurationInMinutes(AutoDeleteOnIdleInDays * 1440)

                        //This dictates the time to live of a message in two scenarios 
                        //  1) if the message does not have its direct TimeToLive property value set or 
                        //  2) when the messages TimeToLive is greater than the queue’s DefaultMessageTimeToLive property.  
                        //  However, if the message’s TimeToLive value is lower, the message’s TimeToLive will be the time at which the message will expire.
                        .WithDefaultMessageTTL(TimeSpan.FromMinutes(DefaultTimeToLive))
                        .WithMessageLockDurationInSeconds(LockDuration);

                    if (!EnableDLOnMessageExpiration)
                        q.WithExpiredMessageMovedToDeadLetterSubscription();

                    if (RequiresSession)
                        q.WithSession();

                    if (!EnableBatch)
                        q.WithoutMessageBatching();                   

                    q.Create();

                    _r = true;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error while creating subscription: {ex.Message}");
            }

            return _r;
        }

        public bool CreateFilter(String TopicName, string SubscriptionName, string RuleName, string SqlFilter)
        {
            var _r = false;

            try
            {
                _log.Information($"Creating rule {RuleName}: for subscription: {SubscriptionName}.");

                _serviceBusManagementClient.Rules.CreateOrUpdateWithHttpMessagesAsync(_serviceBusNamespace.ResourceGroupName, _serviceBusNamespace.Name, TopicName, SubscriptionName, RuleName, new Microsoft.Azure.Management.ServiceBus.Models.Rule() { SqlFilter = new Microsoft.Azure.Management.ServiceBus.Models.SqlFilter(SqlFilter) });
            }
            catch (Exception ex)
            {
                _log.Error($"Error while creating rule: {ex.Message}");
            }

            return _r;
        }





    }
}
