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
            return Path.Combine(Directory.GetCurrentDirectory(), "../", "Engines"); // current directory for azure function is a subdir, which doesn't match the local configuration. Ohh boy ...
        }
        else
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Engines");
        }

    }
}

