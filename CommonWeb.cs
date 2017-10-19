using System;
using System.IO;
using System.Net.Http;

static class CommonWeb
{
    static bool IsAzureEnvironment => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));

    public static string GetWorkingDirectory(HttpRequestMessage req)
    {
        if (IsAzureEnvironment)
        {
            string localPath = req.RequestUri.LocalPath;
            string functionName = localPath.Substring(localPath.LastIndexOf('/') + 1);
            return Path.Combine(@"d:\home\site\wwwroot", functionName);
        }
        else
        {
            return Directory.GetCurrentDirectory();
        }

    }
}

