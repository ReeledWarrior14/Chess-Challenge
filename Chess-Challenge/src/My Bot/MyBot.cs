// #define TESTING

using System;
using System.Linq;
using ChessChallenge.API;

public class MyBot : IChessBot {

    // Piece values: pawn, knight, bishop, rook, queen, king
    private static readonly int[] pieceValues = { 82, 337, 365, 477, 1025, 0, // Middlegame
                                                  94, 281, 297, 512, 936, 0 }, // Endgame
                                                                               // 80, 336, 368, 480, 1024 // 0521233064

    // Compressed Piece-Square tables used for evaluation, ComPresSTO
    psts =
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

    private Move bestMoveRoot;

#if TESTING
    private static int nodes;
#endif

    // Transposition tables - stores the best move in a given position so that it can be looked up later (without having to redo a search)
    // Creating the transposition table (2^24 entries)
    private readonly (ulong, Move, int, int, int)[] transpositionTable = new (ulong, Move, int, int, int)[0x400000];

    // Killer Move array
    private readonly Move[] killerMoves = new Move[1024];

    // Main method, finds and returns the best move in any given position
    public Move Think(Board board, Timer timer) {
#if TESTING
        Console.WriteLine();
        nodes = 0;
#endif

        // History Heuristics
        // Side to move, start square, end square
        var historyHeuristics = new int[2, 7, 64];

        int searchMaxTime = timer.MillisecondsRemaining / 30,
            depth = 2, alpha = -999999, beta = 999999, eval;

        // Iterative deepening loop
        for (; ; ) {
            eval = PVS(depth, 0, alpha, beta, true);

            // Out of time -> soft bound exceeded
            if (timer.MillisecondsElapsedThisTurn > searchMaxTime / 3)
                return bestMoveRoot;

            // Gradual widening
            // Fell outside window, retry with wider window search
            if (eval <= alpha)
                alpha -= 62;
            else if (eval >= beta)
                beta += 62;
            else {
#if TESTING
                string evalWithMate = eval.ToString();
                if (Math.Abs(eval) > 50000) {
                    evalWithMate = eval < 0 ? "-" : "";
                    evalWithMate += $"M{Math.Ceiling((99998 - Math.Abs((double)eval)) / 2)}";
                }

                Console.WriteLine("Info: depth: {0, 2} || eval: {1, 6} || nodes: {2, 9} || nps: {3, 8} || time: {4, 5}ms || best move: {5}{6}",
                    depth,
                    evalWithMate,
                    nodes,
                    1000 * nodes / (timer.MillisecondsElapsedThisTurn + 1),
                    timer.MillisecondsElapsedThisTurn,
                    bestMoveRoot.StartSquare.Name,
                    bestMoveRoot.TargetSquare.Name);
#endif

                // Set up window for next search
                alpha = eval - 17;
                beta = eval + 17;
                depth++;
            }
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
                        // /*
                        if (piece == 3 && (0x101010101010101UL << (square & 7) & board.GetPieceBitboard(PieceType.Pawn, sideToMove > 0)) == 0) {
                            middlegame += 13;
                            endgame += 10;
                        }
                        // */

                        // Mobility bonus (+15 elo alone)
                        /*
                        if (piece >= 2 && piece <= 4) {
                            int bonus = BitboardHelper.GetNumberOfSetBits(
                                BitboardHelper.GetPieceAttacks((PieceType)piece + 1, new Square(square ^ 56 * sideToMove), board, sideToMove > 0));
                            middlegame += bonus;
                            endgame += bonus * 2;
                        }
                        // */
                    }
            return (middlegame * gamephase + endgame * (24 - gamephase)) / (board.IsWhiteToMove ? 24 : -24)
            // Tempo bonus to help with aspiration windows
                + 16;
        }

        // To save tokens, PVS and Q-Search are in a single, combined method
        int PVS(int depth, int ply, int alpha, int beta, bool allowNull) {
#if TESTING
            nodes++;
#endif

            // Declare some reused variables
            bool inCheck = board.IsInCheck(),
                canFPrune = false,
                notRoot = ply++ > 0,
                notPV = beta - alpha == 1;

            // Draw detection
            if (notRoot && board.IsRepeatedPosition())
                return 0;

            int bestEval = -9999999,
                newTTFlag = 2,
                movesTried = 0,
                eval;

            // Evil local method to save tokens for similar calls to PVS (set eval inside search)
            int Search(int newAlpha, int R = 1, bool canNull = true) => eval = -PVS(depth - R, ply, -newAlpha, -alpha, canNull);

            // Check extensions
            if (inCheck)
                depth++;

            // Retrieve the transposition table entry (for this position, empty if it doesnt exist)
            ulong zobristKey = board.ZobristKey;
            var (entryKey, entryMove, entryScore, entryDepth, entryFlag) = transpositionTable[zobristKey & 0x3FFFFF];

            // Transposition Table cutoffs
            // If a position has been evaluated before (to an equal depth or higher) then just use the transposition table value
            if (entryKey == zobristKey && notRoot && entryDepth >= depth && Math.Abs(entryScore) < 50000 && (
                    // Exact
                    entryFlag == 1 ||
                    // Upperbound
                    entryFlag == 2 && entryScore <= alpha ||
                    // Lowerbound
                    entryFlag == 3 && entryScore >= beta))
                return entryScore;

            // Declare QSearch status here to prevent dropping into QSearch while in check
            bool qSearch = depth <= 0;
            if (qSearch) {
                // Determine if quiescence search should be continued
                bestEval = Evaluate();
                if (bestEval >= beta)
                    return bestEval;
                alpha = Math.Max(alpha, bestEval);
            }
            // No pruning in QSearch
            // If this node is NOT part of the PV and we're not in check
            else if (notPV && !inCheck) {
                // Reverse futility pruning
                int staticEval = Evaluate();

                // Give ourselves a margin of 74 centipawns times depth.
                // If we're up by more than that margin in material, there's no point in
                // searching any further since our position is so good
                if (depth <= 7 && staticEval - 74 * depth >= beta)
                    return staticEval;

                // NULL move pruning
                if (depth >= 2 && staticEval >= beta && allowNull) {
                    board.ForceSkipTurn();

                    // TODO: Play with values: Try a max of 4 or 5 instead of 6
                    Search(beta, 3 + depth / 4 + Math.Min(6, (staticEval - beta) / 175), false);
                    board.UndoSkipTurn();

                    // Failed high on the null move
                    if (eval >= beta)
                        return eval;
                }

                // Extended futility pruning
                // Can only prune when at lower depth and behind in evaluation by a large margin
                canFPrune = depth <= 8 && staticEval + depth * 141 <= alpha;
            }

            // Generate moves, only captures in qsearch, and order them to optimize Alpha-Beta pruning
            Move[] moves = board.GetLegalMoves(qSearch && !inCheck).OrderByDescending(
                move =>
                    // Transposition Table Move
                    move == entryMove ? 10_000_000 :
                    // MVV-LVA (Most Valuable Victim, Least Valuable Attacker)
                    move.IsCapture ? 2_000_000 * (int)move.CapturePieceType - (int)move.MovePieceType :
                    // Killer Moves
                    killerMoves[ply] == move ? 1_000_000 :
                    // History Heuristics
                    historyHeuristics[ply & 1, (int)move.MovePieceType, move.TargetSquare.Index]
            ).ToArray();

            Move bestMove = entryMove;
            foreach (Move move in moves) {
                // Out of time -> hard bound exceeded
                // -> Return checkmate so that this move is ignored
                // but better than the worst eval so a move is still picked if no moves are looked at
                // -> Depth check is to disallow timeouts before the bot has finished one round of ID
                if (depth > 2 && timer.MillisecondsElapsedThisTurn > searchMaxTime)
                    return 99999;

                // Futility pruning
                if (canFPrune && !(movesTried == 0 || move.IsCapture || move.IsPromotion))
                    continue;

                board.MakeMove(move);

                // LMR + PVS
                // Do a full window search if haven't tried any moves or in QSearch
                if (movesTried++ == 0 || qSearch ||

                    // Otherwise, skip reduced search if conditions are not met
                    (movesTried < 6 || depth < 2 ||

                        // If reduction is applicable do a reduced search with a null window
                        (Search(alpha + 1, (notPV ? 2 : 1) + movesTried / 13 + depth / 9) > alpha)) &&

                        // If alpha was above threshold after reduced search, or didn't match reduction conditions,
                        // update eval with a search with a null window
                        alpha < Search(alpha + 1))

                    // We either raised alpha on the null window search, or haven't searched yet,
                    // -> research with no null window
                    Search(beta);

                board.UndoMove(move);

                if (eval > bestEval) {
                    bestEval = eval;
                    if (eval > alpha) {
                        alpha = eval;
                        bestMove = move;
                        newTTFlag = 1;

                        // Update the root move
                        if (!notRoot)
                            bestMoveRoot = move;
                    }

                    // Cutoff
                    if (alpha >= beta) {
                        // Update history tables
                        if (!move.IsCapture) {
                            historyHeuristics[ply & 1, (int)move.MovePieceType, move.TargetSquare.Index] += depth * depth;
                            killerMoves[ply] = move;
                        }
                        newTTFlag = 3;
                        break;
                    }
                }
            }

            // Gamestate, checkmate and draws
            // -> no moves were looked at and eval was unchanged
            // -> must not be in QSearch and have had no legal moves
            if (bestEval == -9999999)
                return inCheck ? ply - 99999 : 0;

            // Transposition table insertion
            transpositionTable[zobristKey & 0x3FFFFF] = (
                zobristKey,
                bestMove,
                bestEval,
                depth,
                newTTFlag);

            return bestEval;
        }
    }
}