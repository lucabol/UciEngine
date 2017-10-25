using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Diagnostics;
using System.Net.Http.Formatting;
using System.Threading;
using System.Text;


public static class ProcessText
{
    // From https://stackoverflow.com/questions/45026215/how-to-check-azure-function-is-running-on-local-environment-roleenvironment-i

    [FunctionName("ProcessText")]
    public static HttpResponseMessage Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "chess/engine/{engineName}/ProcessText")]HttpRequestMessage req, string engineName, TraceWriter log)
    {
        log.Info($"Asked for engine {engineName}");
        var engineCommand = CommonChess.GetEngineCommand(engineName);
        log.Info($"Engine command is {engineCommand}");

        var workingDir = CommonWeb.GetWorkingDirectory(req);
        log.Info($"Working dir is {workingDir}");

        var uciText = req.Content.ReadAsStringAsync().Result;
        log.Info(uciText);

        (var outText, var errText) = CommonChess.GetEngineText(engineCommand, workingDir, uciText);

        return req.CreateResponse(HttpStatusCode.OK, outText, "text/plain");
    }

}

