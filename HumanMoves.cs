using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;


public static class HumanMoves
{
    [FunctionName("HumanMoves")]
    public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "chess/engine/{engineName}/{fen}")]HttpRequestMessage req, string engineName, string fen, TraceWriter log)
    {
        log.Info("C# HTTP trigger function processed a request.");

        // Fetching the name from the path parameter in the request URL
        return req.CreateResponse(HttpStatusCode.OK, "Hello " + fen);
    }
}

