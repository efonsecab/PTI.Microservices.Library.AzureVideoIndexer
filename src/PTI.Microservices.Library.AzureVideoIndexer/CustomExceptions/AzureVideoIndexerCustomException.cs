using System;
using System.Collections.Generic;
using System.Text;

namespace PTI.Microservices.Library.CustomExceptions
{
    /// <summary>
    /// AzuRepresents an azure video indexer exception
    /// </summary>
    public class AzureVideoIndexerCustomException: Exception
    {
        /// <summary>
        /// 
        /// </summary>
        public TimeSpan? RetryAfter { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="retryAfter"></param>
        public AzureVideoIndexerCustomException(string message, TimeSpan? retryAfter): base(message)
        {
            this.RetryAfter = retryAfter;
        }
    }
}
