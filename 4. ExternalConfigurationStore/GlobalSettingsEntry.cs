using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace ExternalConfigurationStore
{
    public class GlobalSettingsEntry : TableEntity
    {
        public GlobalSettingsEntry(string ApplicationName, string UserId = null)
        {
            this.PartitionKey = ApplicationName;
            this.RowKey = UserId;
        }
        public GlobalSettingsEntry() { }


        public string Collection { get; set; }

        public string Entry { get; set; }        
    }
}
