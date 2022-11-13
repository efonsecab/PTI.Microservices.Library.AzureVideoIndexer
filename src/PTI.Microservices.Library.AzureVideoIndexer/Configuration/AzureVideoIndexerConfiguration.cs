using System;
using System.Collections.Generic;
using System.Text;

namespace PTI.Microservices.Library.Configuration
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class AzureVideoIndexerConfiguration
    {
        public string Key { get; set; }
        public string Location { get; set; }
        public string AccountId { get; set; }
        public string ProjectId { get; set; }
        public string BaseAPIUrl { get; set; } = "https://api.videoindexer.ai/";
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
