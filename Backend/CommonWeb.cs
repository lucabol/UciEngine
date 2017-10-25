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
            return @"d:\home\site\wwwroot\engines"
        }
        else
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Engines");
        }

    }
}

