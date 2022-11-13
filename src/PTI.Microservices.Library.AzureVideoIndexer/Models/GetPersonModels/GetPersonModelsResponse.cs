using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Models.AzureVideoIndexerService.GetPersonModels
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class GetPersonModelsInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public bool isDefault { get; set; }
        public int personsCount { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
