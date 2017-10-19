using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

public class CandidateMove
{
    public CandidateMove() {
        BestContinuation = new List<string>();
        NextCandidates = new List<CandidateMove>();
    }

    public int Depth { get; set; }
    public string Move { get; set; }
    public List<string> BestContinuation { get; set; }
    public int Score { get; set; } // In centipeds
    public List<CandidateMove> NextCandidates { get; set; }
}

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

    public static CandidateMove LineToCandidate(string line)
    {
        if (String.IsNullOrEmpty(line)) return null;

        var tokens = line.Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if(tokens[0] != "info") return null; // skip over non-info

        var c = new CandidateMove();

        var idx = Array.FindIndex(tokens, x => x == "pv");
        if (idx == -1) return null; // skip over different info lines
        c.Move = tokens[idx + 1];
        for(int i = idx + 2; i < tokens.Length; i++)
        {
            c.BestContinuation.Add(tokens[i]);
        }
        
        idx = Array.FindIndex(tokens, x => x == "score");
        if (idx == -1) return null; // skip over different info lines
        c.Score = int.Parse(tokens[idx + 2]);

        idx = Array.FindIndex(tokens, x => x == "depth");
        if (idx == -1) return null; // skip over different info lines
        c.Depth = int.Parse(tokens[idx + 1]);

        return c;
    }

    public static IEnumerable<CandidateMove> EngineTextToCandidates(string text)
    {
        var lines = text.Split(new char[] { '\n' }, StringSplitOptions.None);
        return lines.Select(LineToCandidate).Where(x => x != null);
    }

    public static CandidateMove[] GetCandidateMoves(string engineExe, string workingDir, string fen)
    {
        var depth = 3;
        var commands = $"ucinewgame\nsetoption name MultiPV value 50\nposition fen {fen}\ngo depth {depth}";
        (var outText, var errText) = GetEngineText(engineExe, workingDir, commands);
        return EngineTextToCandidates(outText).Where(c => c.Depth == depth).ToArray();
    }
}

