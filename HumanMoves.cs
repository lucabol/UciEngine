using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;


public static class HumanMoves
{
    [FunctionName("HumanMoves")]
    public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "chess/engine/{engineName}/HumanMoves")]HttpRequestMessage req, string engineName, TraceWriter log)
    {
        log.Info($"Asked for engine {engineName}");
        var engineCommand = CommonChess.GetEngineCommand(engineName);
        log.Info($"Engine command is {engineCommand}");

        var workingDir = CommonWeb.GetWorkingDirectory(req);
        log.Info($"Working dir is {workingDir}");

       string fen1 = req.GetQueryNameValuePairs()
            .FirstOrDefault(q => string.Compare(q.Key, "fen", true) == 0)
            .Value;

        var candidates = CommonChess.GetCandidateMoves(engineCommand, workingDir, WebUtility.UrlDecode(fen1));
        // Fetching the name from the path parameter in the request URL
        return req.CreateResponse(HttpStatusCode.OK, candidates);
    }
}

