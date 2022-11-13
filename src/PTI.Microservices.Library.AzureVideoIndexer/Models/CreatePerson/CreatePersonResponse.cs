using System;
using System.Collections.Generic;
using System.Text;

namespace PTI.Microservices.Library.Models.AzureVideoIndexerService.CreatePerson
{

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class CreatePersonResponse
    {
        public string id { get; set; }
        public string name { get; set; }
        public DateTime lastModified { get; set; }
        public string lastModifierName { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
