using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TicketHubFunction
{
    public class Function1
    {
        [Function("Function1")]
        public void Run(
            [QueueTrigger("tickethub", Connection = "AzureWebJobsStorage")] string rawMessage,
            FunctionContext context)
        {
            var log = context.GetLogger<Function1>();
            log.LogWarning(" MINIMAL FUNCTION TRIGGERED with message: " + rawMessage);
        }
    }
}