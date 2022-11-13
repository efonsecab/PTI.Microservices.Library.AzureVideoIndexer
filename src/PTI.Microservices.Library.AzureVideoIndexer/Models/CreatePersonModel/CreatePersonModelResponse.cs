using System;
using System.Collections.Generic;
using System.Text;

namespace PTI.Microservices.Library.Models.AzureVideoIndexerService.CreatePersonModel
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class CreatePersonModelResponse
    {
        public string id { get; set; }
        public string name { get; set; }
        public bool isDefault { get; set; }
        public int personsCount { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
