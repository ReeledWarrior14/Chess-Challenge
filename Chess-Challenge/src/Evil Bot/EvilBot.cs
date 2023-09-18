using System;
using System.Linq;
using ChessChallenge.API;

public class EvilBot : IChessBot {
#if UCI
    public ulong nodes = 0;
#endif

    // readonly int[] weights = new int[3097];
    private static readonly int[] pieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                                  94, 281, 297, 512, 936, 0 }; // Endgame
    readonly (ulong, Move, int, int, byte)[] tt = new (ulong, Move, int, int, byte)[1048576];

    private readonly int[] psts =
        new[] {
            59445390105436474986072674560m, 70290677894333901267150682880m, 71539517137735599738519086336m, 78957476706409475571971323392m, 76477941479143404670656189696m, 78020492916263816717520067072m, 77059410983631195892660944640m, 61307098105356489251813834752m,
            77373759864583735626648317994m, 3437103645554060776222818613m, 5013542988189698109836108074m, 2865258213628105516468149820m, 5661498819074815745865228343m, 8414185094009835055136457260m, 7780689186187929908113377023m, 2486769613674807657298071274m,
            934589548775805732457284597m, 4354645360213341838043912961m, 8408178448912173986754536726m, 9647317858599793704577609753m, 9972476475626052485400971547m, 9023455558428990305557695533m, 9302688995903440861301845277m, 4030554014361651745759368192m,
            78006037809249804099646260205m, 5608292212701744542498884606m, 9021118043939758059554412800m, 11825811962956083217393723906m, 11837863313235587677091076880m, 11207998775238414808093699594m, 9337766883211775102593666830m, 4676129865778184699670239740m,
            75532551896838498151443462373m, 3131203134016898079077499641m, 8090231125077317934436125943m, 11205623443703685966919568899m, 11509049675918088175762150403m, 9025911301112313205746176509m, 6534267870125294841726636036m, 3120251651824756925472439792m,
            74280085839011331528989207781m, 324048954150360030097570806m, 4681017700776466875968718582m, 7150867317927305549636569078m, 7155688890998399537110584833m, 5600986637454890754120354040m, 1563108101768245091211217423m, 78303310575846526174794479097m,
            70256775951642154667751105509m, 76139418398446961904222530552m, 78919952506429230065925355250m, 2485617727604605227028709358m, 3105768375617668305352130555m, 1225874429600076432248013062m, 76410151742261424234463229975m, 72367527118297610444645922550m,
            64062225663112462441888793856m, 67159522168020586196575185664m, 71185268483909686702087266048m, 75814236297773358797609495296m, 69944882517184684696171572480m, 74895414840161820695659345152m, 69305332238573146615004392448m, 63422661310571918454614119936m,
        }.SelectMany(packedTable =>
        decimal.GetBits(packedTable).SelectMany(BitConverter.GetBytes)
                    // No point in only taking 12 bytes. Since we never access the last 4 anyway, we can just leave them as garbage
                    .Select((square, index) => (int)((sbyte)square * 1.461) + pieceValues[index % 12])
                .ToArray()
        ).ToArray();

#if UCI
    public Move Think(Board board, Timer timer)
    {
        return ThinkInternal(board, timer);
    }

    public Move ThinkInternal(Board board, Timer timer, int maxDepth = 50, bool report = true)
#else
    public Move Think(Board board, Timer timer)
#endif
    {
        Move bestMoveRoot = default;
        var killers = new Move[128];
        var history = new int[4096];
        int iterDepth = 1;
#if UCI
        nodes = 0;
        for (iterDepth = 1; iterDepth <= maxDepth && timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 30;)
#else
        while (timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 30)
#endif
        {
#if UCI
            int score =
#endif
            Search(-30000, 30000, iterDepth++, 0);
#if UCI
            if (report && timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 30)
            {
                ulong time = (ulong)timer.MillisecondsElapsedThisTurn;
                ulong nps = nodes * 1000 / Math.Max(time, 1);
                Console.WriteLine(
                    $"info depth {iterDepth} score cp {score} time {time} nodes {nodes} nps {nps}"
                );
            }
#endif
        }

        return bestMoveRoot;

        int Search(int alpha, int beta, int depth, int ply) {
            bool inCheck = board.IsInCheck();

            // Check extensions
            if (inCheck)
                depth++;

            bool qs = depth <= 0;
#if UCI
            nodes++;
#endif
            ulong key = board.ZobristKey;
            var (ttKey, ttMove, ttDepth, score, ttFlag) = tt[key % 1048576];
            int bestScore = -30000;

            // Check for draw by repetition
            if (ply > 0
                && board.IsRepeatedPosition())
                return 0;

            // Stand Pat
            if (qs
                && (bestScore = alpha = Math.Max(alpha, Evaluate())) >= beta)
                return alpha;

            // TT Cutoffs
            if (beta - alpha == 1
                && ttKey == key
                && ttDepth >= depth
                && (score >= beta ? ttFlag > 0 : ttFlag < 2))
                return score;

            // Reverse Futility Pruning
            if (!qs
                && !inCheck
                && depth <= 8
                && Evaluate() >= beta + 120 * depth)
                return beta;

            // Generate moves
            var moves = board.GetLegalMoves(qs);

            // Checkmate/Stalemate
            if (moves.Length == 0)
                return qs ? alpha : inCheck ? ply - 30_000 : 0;

            // Score moves
            int moveIdx = 0;
            var scores = new int[moves.Length];
            foreach (Move move in moves)
                scores[moveIdx++] = -(
                    move == ttMove
                        ? 900_000_000
                        : move.IsCapture
                            ? 100_000_000 * (int)move.CapturePieceType - (int)move.MovePieceType
                            : move == killers[ply]
                                ? 80_000_000
                                : history[move.RawValue & 4095]
                );

            Array.Sort(scores, moves);

            ttMove = default;
            moveIdx = ttFlag = 0;

            foreach (Move move in moves) {
                if (timer.MillisecondsElapsedThisTurn >= timer.MillisecondsRemaining / 15)
                    return 30000;

                board.MakeMove(move);

                // Principal Variation Search + Late Move Reductions
                if (moveIdx++ == 0
                    || qs
                    || depth < 2
                    || move.IsCapture
                    || (score = -Search(-alpha - 1, -alpha, depth - 2 - moveIdx / 16, ply + 1)) > alpha)
                    score = -Search(-beta, -alpha, depth - 1, ply + 1);

                board.UndoMove(move);

                if (score > bestScore) {
                    bestScore = score;
                    ttMove = move;
                    if (score > alpha) {
                        alpha = score;
                        ttFlag = 1;

                        if (ply == 0)
                            bestMoveRoot = move;

                        if (alpha >= beta) {
                            // Quiet cutoffs update tables
                            if (!move.IsCapture) {
                                killers[ply] = move;
                                history[move.RawValue & 4095] += depth;
                            }

                            ttFlag++;

                            break;
                        }
                    }
                }
            }

            tt[key % 1048576] = (key, ttMove, depth, bestScore, ttFlag);

            return bestScore;
        }

        // ComPresSTO, credit to Tyrant
        int Evaluate() {
            int middlegame = 0, endgame = 0, gamephase = 0, sideToMove = 2, piece, square;
            for (; --sideToMove >= 0; middlegame = -middlegame, endgame = -endgame)
                for (piece = 6; --piece >= 0;)
                    for (ulong mask = board.GetPieceBitboard((PieceType)piece + 1, sideToMove > 0); mask != 0;) {
                        // Gamephase, middlegame -> endgame
                        // Multiply, then shift, then mask out 4 bits for value (0-16)
                        gamephase += 0x00042110 >> piece * 4 & 0x0F;

                        // Material and square evaluation
                        square = BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ 56 * sideToMove;
                        middlegame += psts[square * 16 + piece];
                        endgame += psts[square * 16 + piece + 6];

                        // Bishop pair bonus
                        if (piece == 2 && mask != 0) {
                            middlegame += 23;
                            endgame += 62;
                        }

                        // Doubled pawns penalty (brought to my attention by Y3737)
                        if (piece == 0 && (0x101010101010101UL << (square & 7) & mask) > 0) {
                            middlegame -= 15;
                            endgame -= 15;
                        }

                        // Semi-open file bonus for rooks (+14.6 elo alone)
                        /*
                        if (piece == 3 && (0x101010101010101UL << (square & 7) & board.GetPieceBitboard(PieceType.Pawn, sideToMove > 0)) == 0)
                        {
                            middlegame += 13;
                            endgame += 10;
                        }
                        */

                        // Mobility bonus (+15 elo alone)
                        /*
                        if (piece >= 2 && piece <= 4)
                        {
                            int bonus = BitboardHelper.GetNumberOfSetBits(
                                BitboardHelper.GetPieceAttacks((PieceType)piece + 1, new Square(square ^ 56 * sideToMove), board, sideToMove > 0));
                            middlegame += bonus;
                            endgame += bonus * 2;
                        }
                        */
                    }
            return (middlegame * gamephase + endgame * (24 - gamephase)) / (board.IsWhiteToMove ? 24 : -24)
            // Tempo bonus to help with aspiration windows
                + 16;
        }
    }
}