using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PTI.Microservices.Library.Models.AzureVideoIndexerService.UploadVideo
{
    /// <summary>
    /// Video metadata information
    /// </summary>
    public class VideoMetadataAttribute
    {
        /// <summary>
        /// MEtadata key attribute
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// MEtadata key value
        /// </summary>
        public string Value { get; set; }
    }
}
