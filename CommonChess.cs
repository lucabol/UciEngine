using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

// TEST POSITIONS (fen, encoded)
// Various check and captures (with promotion): 1N1Q1r2/2P3P1/8/2k3b1/4R2P/3P1NR1/1P1B4/3K4 w - - 0 1   1N1Q1r2%2F2P3P1%2F8%2F2k3b1%2F4R2P%2F3P1NR1%2F1P1B4%2F3K4%20w%20-%20-%200%201
// Castling and promo to K: r6r/1Pq2kP1/8/8/8/8/8/R3K2R w KQ - 0 0         r6r%2F1Pq2kP1%2F8%2F8%2F8%2F8%2F8%2FR3K2R%20w%20KQ%20-%200%200
// Simple mate: 3k4/R7/8/8/8/8/8/3K3Q w - - 0 1          3k4%2FR7%2F8%2F8%2F8%2F8%2F8%2F3K3Q%20w%20-%20-%200%201
public class CandidateMove
{
    public CandidateMove() {
        BestContinuation = new List<string>();
        NextCandidates = new List<CandidateMove>();
    }

    public int Depth { get; set; }
    public string Move { get; set; }
    public string MoveAlg { get; set; }
    public bool IsCapture { get; set; }
    public bool IsCheck { get; set; }
    public bool IsMate { get; set; }
    public int MateNumberOfMoves { get; set; }
    public string CapturedPiece { get; set; }
    public List<string> BestContinuation { get; set; }
    public int Score { get; set; } // In centipeds
    public List<CandidateMove> NextCandidates { get; set; }
}

public class Position
{
    readonly public static char EmptySquare = '-';

    public string Fen { get; set; }
    public char[,] Board { get; set; }
    public char Move { get; set; }
    public string MoveNumber { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Fen: {Fen}");
        sb.AppendLine($"Move: {Move}");
        sb.AppendLine($"MoveNumber: {MoveNumber}");

        int x = 0;
        foreach (var c in Board)
        {
            sb.Append(c);
            x += 1;
            if (x % 8 == 0) sb.AppendLine();
        }
        return sb.ToString();
    }

    public Position Clone()
    {
        return new Position { Fen = this.Fen, Move = this.Move, MoveNumber = this.MoveNumber, Board = (char[,])this.Board.Clone() };
    }
}

public class ChecksCapturesAttacks
{
    public IEnumerable<CandidateMove> Checks { get; set; }
    public IEnumerable<CandidateMove> Captures { get; set; }
    public IEnumerable<CandidateMove> Attacks { get; set; }
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
        c.Move = tokens[idx + 1].Trim();
        for(int i = idx + 2; i < tokens.Length; i++)
        {
            c.BestContinuation.Add(tokens[i].Trim());
        }
        
        idx = Array.FindIndex(tokens, x => x == "score");
        if (idx == -1) return null; // skip over different info lines
        if (tokens[idx + 1] == "cp")
        {
            c.Score = int.Parse(tokens[idx + 2]);
            c.IsMate = false;
        }
        else if (tokens[idx + 1] == "mate")
        {
            c.IsMate = true;
            c.MateNumberOfMoves = int.Parse(tokens[idx + 2]);
        }
        else throw new Exception($"{tokens[idx + 1]} is an unsupported type of move");

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

    public static IEnumerable<CandidateMove> GetCandidateMoves(string engineExe, string workingDir, string fen)
    {
        var depth = 3;
        var commands = $"ucinewgame\nsetoption name MultiPV value 220\nposition fen {fen}\ngo depth {depth}";
        (var outText, var errText) = GetEngineText(engineExe, workingDir, commands);
        if (!String.IsNullOrEmpty(errText)) throw new Exception(errText);

        return EngineTextToCandidates(outText).Where(c => c.Depth == depth);
    }

    public static string MAlgToAlg(Position pos, string malg, bool isCheck, bool isTake)
    {
        var check = isCheck ? "+" : "";

        // Castling
        if (malg == "e1g1" || malg == "e8g8") return $"O-O{check}";
        if (malg == "e1c1" || malg == "e8c8") return $"O-O-O{check}";

        var board = pos.Board;
        (var x1, var y1, var x2, var y2) = MAlgToMatrix(malg);
        var piece = board[x1, y1];
        var destSquare = malg.Substring(2, 2);
        var sourceCol = malg[0];

        // Pawn moves with promotion
        var promotion = malg.Length == 5 ? $"={char.ToUpper(malg[4])}" : "";

        if ((piece == 'P' || piece == 'p') && isTake) return $"{sourceCol}x{destSquare}{promotion}{check}";
        if ((piece == 'P' || piece == 'p') && !isTake) return $"{destSquare}{promotion}{check}";

        // Normal pieces
        if (isTake) return $"{piece}x{destSquare}{check}";
        if (!isTake) return $"{piece}{destSquare}{check}";

        throw new Exception("Shouldn't get there");
    }

    public static ChecksCapturesAttacks GetHumanCandidateMoves(string engineExe, string workingDir, string fen)
    {
        // TODO: Manage disambinguation of moves when two pieces can go to a square or capture there
        var allMovesWithMultiplePromotions = GetCandidateMoves(engineExe, workingDir, fen);

        //remove B & R promotions
        var allMoves = allMovesWithMultiplePromotions.Where(c => c.Move.Length == 4 || (c.Move.Length == 5 && (c.Move[4] == 'q' || c.Move[4] == 'n'))).ToList();
        var pos = FenToPosition(fen);

        (var kingx, var kingy) = FindOppositeKing(pos); // Optimization not to have to calculate king position every time

        foreach (var c in allMoves)
        {
            c.IsCheck = IsCheck(pos, c.Move, kingx, kingy);

            var captured = DestinationSquare(pos, c.Move);
            if (captured != Position.EmptySquare)
            {
                c.CapturedPiece = captured.ToString().ToUpper();
                c.IsCapture = true;
            }

            c.MoveAlg = MAlgToAlg(pos, c.Move, c.IsCheck, c.IsCapture);
        }

        var checks = allMoves.Where(c => c.IsCheck);
        var captures = allMoves.Where(c => c.IsCapture);

        return new ChecksCapturesAttacks
        {
            Checks = checks,
            Captures = captures,
            Attacks = null
        };

    }


    public static Position FenToPosition(string fen)
    {
        var fields = fen.Split(new char[] { ' ' });
        if (fields.Length != 6) throw new Exception($"This fen doesn't have six fields {fen}");

        var rows = fields[0].Split(new char[] { '/' });
        var move = fields[1][0];
        var moveNumber = fields[5];

        int rowNum = 0;
        var matrix = new char[8, 8];

        for (var i = 0; i < 8; i++)
        {
            var row = rows[i];
            var colNum = 0;
            foreach (var c in row.ToCharArray())
            {
                var d = Char.GetNumericValue(c);
                if (d > 0)
                {
                    for (var j = 0; j < d; j++)
                    {
                        matrix[rowNum, colNum] = Position.EmptySquare;
                        colNum += 1;
                    }
                }
                else
                {
                    matrix[rowNum, colNum] = c;
                    colNum += 1;
                }
            }
            rowNum += 1;
        }
        return new Position { Board = matrix, Fen = fen, Move = move, MoveNumber = moveNumber };
    }
    static (int x1, int y1, int x2, int y2) MAlgToMatrix(string malg)
    {
        int ToInt(char c) { return 8 - ('h' - c) - 1; }
        var a = malg.ToCharArray();
        return (8 - (int)Char.GetNumericValue(a[1]), ToInt(a[0]), 8 - (int)Char.GetNumericValue(a[3]), ToInt(a[2]));
    }

    static char DestinationSquare(Position pos, string malg)
    {
        (var x1, var y1, var x2, var y2) = MAlgToMatrix(malg);
        return pos.Board[x2, y2];
    }

    // algo from https://stackoverflow.com/questions/23380770/implementing-check-in-a-chess-game
    static int[] rowDirections = { -1, -1, -1, 0, 0, 1, 1, 1 };
    static int[] colDirections = { -1, 0, 1, -1, 1, -1, 0, 1 };

    static bool[] bishopThreats = new[] { true, false, true, false, false, true, false, true };
    static bool[] rookThreats   = new[] { false, true, false, true, true, false, true, false };
    static bool[] queenThreats  = new[] { true, true, true, true, true, true, true, true };
    static bool[] kingThreats   = new[] { true, true, true, true, true, true, true, true };

    static (int, int)[] knightMoves = new[] { (+1, +2),  (+1, -2), (-1, +2), (-1, -2), (+2, -1), (+2, +1), (-2, +1), (-2, -1)};

    static Dictionary<char, bool[]> pawnThreats = new Dictionary<char, bool[]>
    {
        { 'b', new[] { true, false, true, false, false, false, false, false} },
        { 'w', new[] { false, false, false, false, false, true, false, true} }
    };

    static bool isPieceOfColor(char piece, char color)
    {
        if (!char.IsLetter(piece)) throw new Exception($"Piece {piece} is not a letter");
        if (char.IsUpper(piece) && color == 'w') return true;
        if (char.IsLower(piece) && color == 'b') return true;
        return false;
    }

    static bool isBishop(char piece)    => piece == 'b' || piece == 'B';
    static bool isRook(char piece)      => piece == 'r' || piece == 'R';
    static bool isQueen(char piece)     => piece == 'q' || piece == 'Q';
    static bool isKing(char piece)      => piece == 'k' || piece == 'K';
    static bool isPawn(char piece)      => piece == 'p' || piece == 'P';
    static bool isKnight(char piece) => piece == 'n' || piece == 'N';

    static bool outOfBound(int row, int col)
    {
        return row < 0 || row > 7 || col < 0 || col > 7;
    }

    static bool IsThreatenedSquare(Position pos, int threatenedRow, int threatenedCol)
    {
        // TODO: Knights
        var board = pos.Board;
        var thretenedColor = pos.Move == 'w' ? 'b' : 'w'; // Having moved forward by one move the attacking color got inverted. Hmmm, perhaps a sign the way I structured sucks.

        // Check linear pieces
        for (int direction = 0; direction < rowDirections.Length; direction++)
        {
            // RESET OUR COORDINATES TO PROCESS CURRENT LINE OF ATTACK. 
            // INCREMENT VALUES ARE SAME AS DIRECTION ARRAY VALUES
            int row = threatenedRow;
            int col = threatenedCol;
            int rowIncrement = rowDirections[direction];
            int colIncrement = colDirections[direction];

            // RADIATE OUTWARDS STARTING FROM ORIGIN UNTIL WE HIT A PIECE OR ARE OUT OF BOUNDS
            for (int step = 0; step < 8; step++)
            {
                row = row + rowIncrement;
                col = col + colIncrement;

                // IF WE ARE OUT OF BOUNDS, WE STOP RADIATING OUTWARDS FOR 
                // THIS RAY AND TRY THE NEXT ONE
                if (outOfBound(row, col))
                {
                    break;
                }
                else
                {
                    // LOOK AT CURRENT SQUARE AND SEE IF IT IS OCCUPIED BY A PIECE
                    var piece = board[row, col];
                    if (piece != Position.EmptySquare)
                    {
                        // RADIATING OUTWARDS MUST STOP SINCE WE HIT A PIECE, ONLY
                        // QUESTION IS WHAT DID WE HIT? FRIEND OR FOE?  
                        if (isPieceOfColor(piece, thretenedColor))
                        {
                            // WE ARE FACING AN OPPONENT, DOES IT HAVE THE CAPABILITY
                            // TO ATTACK US GIVEN THE DIRECTIONAL LINE OF ATTACK  
                            // WE ARE CURRENTLY ANALYZING     
                            if (isBishop(piece) && bishopThreats[direction]) return true;
                            else if (isRook(piece) && rookThreats[direction]) return true;
                            else if (isQueen(piece) && queenThreats[direction]) return true;
                            else {
                                if (step == 0)
                                {
                                    // PAWNS AND KINGS DONT HAVE THE REACH OF OTHER SLIDING
                                    // PIECES; THEY CAN ONLY ATTACK SQUARES THAT ARE CLOSEST
                                    // TO ORIGIN  
                                    if (isPawn(piece) && pawnThreats[thretenedColor][direction]) return true;
                                    if (isKing(piece) && kingThreats[direction]) return true;
                                }
                            }
                        }
                        break; // ENCOUNTERED A FRIENDLY PIECE STOPPING THE PATH
                    }
                    // MOVE TO NEXT STEP AS WE ARE NOT OUT OF BOUNDS
                }
            }
        }

        // Check knights
        for (int i = 0; i < knightMoves.Length; i++)
        {
            (var xstep, var ystep) = knightMoves[i];
            int x = threatenedRow + xstep,
                y = threatenedCol + ystep;

            if (!outOfBound(x, y))
            {
                var piece = board[x, y];
                if (piece != Position.EmptySquare && isPieceOfColor(piece, thretenedColor) && isKnight(piece)) return true;
            }
        }

        return false;
    }

    static Position MakeMove(Position pos, string malg)
    {
        (var x1, var y1, var x2, var y2) = MAlgToMatrix(malg);
        var res = pos.Clone();
        var board = res.Board;

        var resultingPiece = malg.Length == 5 ?
                                pos.Move == 'w' ? char.ToUpper(malg[4]) : malg[4]
                                : pos.Board[x1, y1]; // promotion

        board[x1, y1] = Position.EmptySquare;
        board[x2, y2] = resultingPiece;
        res.Move = pos.Move == 'w' ? 'b' : 'w';
        res.MoveNumber = pos.MoveNumber + 1;

        // Castling
        if (malg == "e1g1") { board[7, 7] = Position.EmptySquare; board[7, 5] = 'R'; }
        if (malg == "e1c1") { board[7, 0] = Position.EmptySquare; board[7, 3] = 'R'; }
        if (malg == "e8g8") { board[1, 7] = Position.EmptySquare; board[1, 5] = 'r'; }
        if (malg == "e8c8") { board[1, 0] = Position.EmptySquare; board[1, 5] = 'r'; }

        return res;
    }

    static (int x, int y) FindOppositeKing(Position pos)
    {
        var king = pos.Move == 'w' ? 'k' : 'K';
        var board = pos.Board;

        for (var i = 0; i < 8; i++)
            for (var j = 0; j < 8; j++)
                if (board[i, j] == king) return (i, j);

        throw new Exception($"Position {pos.ToString()} has no opposite King?");
    }

    static bool IsCheck(Position pos, string malg, int kingx, int kingy)
    {
        var p = MakeMove(pos, malg);
        return IsThreatenedSquare(p, kingx, kingy);
    }
}

