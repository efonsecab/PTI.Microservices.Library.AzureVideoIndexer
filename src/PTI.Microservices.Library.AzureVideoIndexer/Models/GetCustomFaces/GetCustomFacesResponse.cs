using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Models.AzureVideoIndexerService.GetCustomFaces
{

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class GetCustomFacesResponse
    {
        public int totalCount { get; set; }
        public Result[] results { get; set; }
        public int pageSize { get; set; }
        public int skip { get; set; }
    }

    public class Result
    {
        public string id { get; set; }
        public string state { get; set; }
        public string sourceType { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
