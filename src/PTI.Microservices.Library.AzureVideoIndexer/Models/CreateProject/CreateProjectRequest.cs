using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.AzureVideoIndexer.Models.CreateProject
{

    public class CreateProjectRequest
    {
        public string name { get; set; }
        public Videosrange[] videosRanges { get; set; }
    }

    public class Videosrange
    {
        public string videoId { get; set; }
        public Range range { get; set; }
    }

    public class Range
    {
        public string start { get; set; }
        public string end { get; set; }
    }

}
