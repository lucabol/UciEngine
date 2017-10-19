using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

public static class CommonChess
{
    static readonly int requestTimeout = 10 * 1_000; // 10 secs

    private static string WaitForBestMove(StreamWriter stdIn, StreamReader stdOut, string uciText)
    {
        stdIn.WriteLine(uciText);
        var outText = new StringBuilder();
        var outLine = stdOut.ReadLine();
        while (!outLine.StartsWith("bestmove"))
        {
            outText.AppendLine(outLine);
            outLine = stdOut.ReadLine();
        }
        outText.AppendLine(outLine);
        stdIn.WriteLine("quit");
        return outText.ToString();
    }

    public static string GetEngineCommand(string engineName)
    {
        // Validate and set engine
        var engines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Stockfish",  "stockfish_8_x64.exe"}
        };
        if (!engines.TryGetValue(engineName, out string command))
            throw new Exception($"{engineName} is not a recognized engine name. Try one of "
                + String.Join(",", engines.Select(kvp => kvp.Key.ToString())));

        return command;
    }

    public static (string output, string errors) GetEngineText(string engineExe, string workingDir, string commands)
    {

        Directory.SetCurrentDirectory(workingDir);

        // Run engine
        var process = new Process();
        process.StartInfo.FileName = engineExe;
        process.StartInfo.Arguments = "";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardInput = true;
        process.Start();


        string outText = WaitForBestMove(process.StandardInput, process.StandardOutput, commands);
        process.WaitForExit(requestTimeout);

        string errText = process.StandardError.ReadToEnd();

        return (outText, errText);
    }
}

