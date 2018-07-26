using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text;

using Serilog;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Timers;

namespace ExternalConfigurationStore
{
    public class GlobalSettings
    {
        string _connectionString = "DefaultEndpointsProtocol=https;AccountName=cloudpatternsamples;AccountKey=c4FTWjmIMRKIc68hPgSy6GQpfoqkrMsXj37CBYyweip3Id15N80vlMBTIBHk9qwoGyIdxKHbzsjbMIRd1x2M9w==;TableEndpoint=https://cloudpatternsamples.table.cosmosdb.azure.com:443/;";

        public GlobalSettings()        {
            this.AppSettings = new ExpandoObject();
            this.UserSettings = new ExpandoObject();
        }
        private static ILogger _log;
        private bool _isInitialized;
        private string _appName;
        private string _userName;
        private dynamic AppSettings;
        private dynamic UserSettings { get; set; }
        private static Timer _timer;

        public GlobalSettings With(ILogger Logger, TimeSpan ValidationTimeSpan)
        {
            _log = Logger;
            
            //load settings form settings store
            CreateSettingsStoreIfNotExists().GetAwaiter().GetResult();

            _timer = new Timer(ValidationTimeSpan.TotalMilliseconds);
            _timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
            _timer.Start();


            _isInitialized = true;

            return this;
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ForApplication(_appName);
        }

        public dynamic ForApplication(string ApplicationName)
        {
            if (!_isInitialized)
            {
                throw new ApplicationException("Global Settings functionality has not been initialized. Call With() first.");
            }

            if (!string.IsNullOrEmpty(ApplicationName))
            {
                LoadAppSettingsFromGlobalSettingsStore(ApplicationName).GetAwaiter().GetResult();
                _appName = ApplicationName;

                ((INotifyPropertyChanged)this.AppSettings).PropertyChanged += delegate (object o, PropertyChangedEventArgs e)
                {
                    _log.Information($"Global setting for app {_appName} and setting {e.PropertyName} has changed. Saving settings.");
                    SaveAppSettingsToGlobalSettingsStore().GetAwaiter().GetResult();
                };
            }

            return AppSettings;
        }

        //public dynamic ForUser(string UserId)
        //{
        //    if (!_isInitialized)
        //    {
        //        throw new ApplicationException("Global Settings functionality has not been initialized. Call With() first.");
        //    }

        //    if (string.IsNullOrEmpty(_appName))
        //    {
        //        throw new ApplicationException("App name is invalid. Call  ForApplication() first.");
        //    }

        //    if (!string.IsNullOrEmpty(UserId))
        //    {
        //        LoadUserSettingsFromGlobalSettingsStore(_appName, UserId).GetAwaiter().GetResult();
        //        _userName = UserId;

        //        ((INotifyPropertyChanged)this.AppSettings).PropertyChanged += delegate (object o, PropertyChangedEventArgs e)
        //        {
        //            _log.Information($"Global user setting {_appName}/{_userName} and setting {e.PropertyName} has changed. Saving settings.");
        //            SaveUserSettingsToGlobalSettingsStore().GetAwaiter().GetResult();
        //        };
        //    }

        //    return UserSettings;
        //}

        public async Task CreateSettingsStoreIfNotExists()
        {
            var table = CloudStorageAccount.Parse(_connectionString).CreateCloudTableClient().GetTableReference("globalsettings");

            if (await table.CreateIfNotExistsAsync())
            {
                _log.Information($"Created global settings table: {table.Name}");
            }
            else
            {
                _log.Information($"Global settings table: {table.Name} already exists.");
            }
        }

        private async Task LoadAppSettingsFromGlobalSettingsStore(string appName)
        {
            if (!_isInitialized)
            {
                throw new ApplicationException("Global Settings functionality has not been initialized.");
            }
 
            var table = CloudStorageAccount.Parse(_connectionString).CreateCloudTableClient().GetTableReference("globalsettings");
            
            var retrievedResult = await table.ExecuteAsync(TableOperation.Retrieve<GlobalSettingsEntry>(appName, "~Default~"));

            if(retrievedResult.Result != null)
                this.AppSettings = JsonConvert.DeserializeObject<ExpandoObject>(((GlobalSettingsEntry)retrievedResult.Result).Entry);
        }

        //private async Task LoadUserSettingsFromGlobalSettingsStore(string appName, string userName)
        //{
        //    if (!_isInitialized)
        //    {
        //        throw new ApplicationException("Global Settings functionality has not been initialized.");
        //    }

        //    var table = CloudStorageAccount.Parse(_connectionString).CreateCloudTableClient().GetTableReference("globalsettings");

        //    var retrievedResult = await table.ExecuteAsync(TableOperation.Retrieve<GlobalSettingsEntry>(appName, userName));

        //    if (retrievedResult.Result != null)
        //        this.UserSettings = JsonConvert.DeserializeObject<ExpandoObject>(((GlobalSettingsEntry)retrievedResult.Result).Entry);

        //}

        private async Task SaveAppSettingsToGlobalSettingsStore()
        {
            if (!_isInitialized)
            {
                throw new ApplicationException("Global Settings functionality has not been initialized.");
            }
            
            var table = CloudStorageAccount.Parse(_connectionString).CreateCloudTableClient().GetTableReference("globalsettings");
            
            var setting = new GlobalSettingsEntry(_appName, "~Default~") { Entry = JsonConvert.SerializeObject(this.AppSettings) };

            await table.ExecuteAsync(TableOperation.InsertOrReplace(setting));
        }
        //private async Task SaveUserSettingsToGlobalSettingsStore()
        //{
        //    if (!_isInitialized)
        //    {
        //        throw new ApplicationException("Global Settings functionality has not been initialized.");
        //    }

        //    var table = CloudStorageAccount.Parse(_connectionString).CreateCloudTableClient().GetTableReference("globalsettings");

        //    var setting = new GlobalSettingsEntry(_appName, _userName) { Entry = JsonConvert.SerializeObject(this.UserSettings) };

        //    await table.ExecuteAsync(TableOperation.InsertOrReplace(setting));
        //}

        public void AddAppSettingCollection(dynamic Parent, string Collection)
        {
            if (this.Contains(Parent, Collection))
            {
                _log.Information($"Global setting for app {_appName} [{Collection}] already exists.");
            }
            else
            {
                ((ExpandoObject)Parent).AddSettingsCollection(Collection, delegate (object o, PropertyChangedEventArgs e)
                {
                    _log.Information($"Global setting for app {_appName} and setting {e.PropertyName} has changed. Saving settings.");
                    SaveAppSettingsToGlobalSettingsStore().GetAwaiter().GetResult();
                });
                _log.Information($"Global setting for app {_appName} is beeing added a new collection [{Collection}] has changed. Saving settings.");
                SaveAppSettingsToGlobalSettingsStore().GetAwaiter().GetResult();
            }
        }
        public bool Contains(ExpandoObject Parent, string Value)
        {
            return Parent.Contains(Value);
        }

        public void ClearAppSettings()
        {
            var dict = (IDictionary<string, object>)this.AppSettings;
            dict.Clear();
            _log.Information($"Global setting for app {_appName} has been cleared out of all values. Saving settings.");
            SaveAppSettingsToGlobalSettingsStore().GetAwaiter().GetResult();
        }

    }

    
    public static class ExpandoObjectExtension
    {
        public static void AddSettingsCollection(this ExpandoObject Setting, string Collection, PropertyChangedEventHandler d)
        {
            try
            {
                var obj = new ExpandoObject();
                ((INotifyPropertyChanged)obj).PropertyChanged += d;
                ((IDictionary<string, Object>)Setting).Add(Collection, obj);
            }
            catch (ArgumentException ex)
            {
                if (!ex.Message.Contains("already exists")) //means it already exists, no need to handle exception
                    throw ex;

            }
        }

        public static bool Contains(this ExpandoObject Setting, string Key)
        {
            return ((IDictionary<String, object>)Setting).ContainsKey(Key);
        }
    }
}
