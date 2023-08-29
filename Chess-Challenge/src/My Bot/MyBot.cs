using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChessChallenge.API;

public class MyBot : IChessBot {

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 310, 330, 500, 1000, 10000 };

    Board m_board;
    Timer m_timer;

    Move bestMoveRoot;
    int bestEvalRoot;
    Move bestIterativeMove;
    int bestIterativeEval;

    double maxTime;

    int positionsEvaled;
    int TTused;

    // Compressed Piece-Square tables used for evaluation, ComPresSTO
    int[] piecePhase = { 0, 0, 1, 1, 2, 4, 0 };
    ulong[] psts = {
    657614902731556116, 420894446315227099, 384592972471695068, 312245244820264086,
    364876803783607569, 366006824779723922, 366006826859316500, 786039115310605588,
    421220596516513823, 366011295806342421, 366006826859316436, 366006896669578452,
    162218943720801556, 440575073001255824, 657087419459913430, 402634039558223453,
    347425219986941203, 365698755348489557, 311382605788951956, 147850316371514514,
    329107007234708689, 402598430990222677, 402611905376114006, 329415149680141460,
    257053881053295759, 291134268204721362, 492947507967247313, 367159395376767958,
    384021229732455700, 384307098409076181, 402035762391246293, 328847661003244824,
    365712019230110867, 366002427738801364, 384307168185238804, 347996828560606484,
    329692156834174227, 365439338182165780, 386018218798040211, 456959123538409047,
    347157285952386452, 365711880701965780, 365997890021704981, 221896035722130452,
    384289231362147538, 384307167128540502, 366006826859320596, 366006826876093716,
    366002360093332756, 366006824694793492, 347992428333053139, 457508666683233428,
    329723156783776785, 329401687190893908, 366002356855326100, 366288301819245844,
    329978030930875600, 420621693221156179, 422042614449657239, 384602117564867863,
    419505151144195476, 366274972473194070, 329406075454444949, 275354286769374224,
    366855645423297932, 329991151972070674, 311105941360174354, 256772197720318995,
    365993560693875923, 258219435335676691, 383730812414424149, 384601907111998612,
    401758895947998613, 420612834953622999, 402607438610388375, 329978099633296596,
    67159620133902};

    // Transposition tables - stores the best move in a given position so that it can be looked up later (without having to redo a search)
    struct TTEntry {
        public ulong key;
        public Move move;
        public int depth, score, bound;
        public TTEntry(ulong _key, Move _move, int _depth, int _score, int _bound) {
            key = _key; move = _move; depth = _depth; score = _score; bound = _bound;
        }
    }

    // Creating the transposition table (2^22 entries)
    const int entries = (1 << 22); // this is 2^22
    TTEntry[] tt = new TTEntry[entries];

    // Main method, finds and returns the best move in any given position
    public Move Think(Board board, Timer timer) {
        m_board = board;
        m_timer = timer;

        positionsEvaled = 0;
        TTused = 0;

        maxTime = getTimeForTurn();

        // Default move in case there is no time for any other moves
        bestIterativeMove = bestMoveRoot = m_board.GetLegalMoves()[0];
        bestIterativeEval = bestEvalRoot = 0;

        // int eval = Search(4, 0, -99999, 99999);

        // Console.WriteLine("Side: " + (m_board.IsWhiteToMove ? "White" : "Black") + "   Depth: " + 4 + "   Eval: " + eval + "   Positions Evaluated: " + positionsEvaled + "   Time: " + timer.MillisecondsElapsedThisTurn + "ms   " + bestMoveRoot);

        // If we have time to think (more than a second) then do iterative deepening, otherwise just return the first move
        if (m_timer.MillisecondsRemaining > 1000) {

            // Iterative deepening
            for (int depth = 1; depth <= 50; depth++) {
                Search(depth, 0, -99999, 99999);

                // If too much time has elapsed or a mate move has been found
                if (timer.MillisecondsElapsedThisTurn >= maxTime || bestEvalRoot > 99900) {
                    // Console.WriteLine("Side: " + (m_board.IsWhiteToMove ? "White" : "Black") + "   Depth: " + depth + "   Eval: " + bestEvalRoot + "   Positions Evaluated: " + positionsEvaled + "   Transposition Table: " + ((double)tt.Count(s => s.bound != 0) / (double)entries * 100).ToString("F") + "%   TT values used: " + TTused + "   Time: " + timer.MillisecondsElapsedThisTurn + "ms   " + bestMoveRoot);
                    // Console.WriteLine("Side: " + (m_board.IsWhiteToMove ? "White" : "Black") + "   Depth: " + depth + "   Eval: " + bestEvalRoot + "   Time: " + timer.MillisecondsElapsedThisTurn + "ms   " + bestMoveRoot);

                    break;
                }
                else {
                    bestMoveRoot = bestIterativeMove;
                    bestEvalRoot = bestIterativeEval;
                }
            }
        }

        // Console.WriteLine("Max time: " + maxTime + "   time left/30: " + m_timer.MillisecondsRemaining / 30 +  "   Time used: " + m_timer.MillisecondsElapsedThisTurn + "  Used allocated time: " + (Math.Round(maxTime) == m_timer.MillisecondsElapsedThisTurn));
        // Console.WriteLine("Used allocated time: " + (Math.Round(maxTime) == m_timer.MillisecondsElapsedThisTurn));

        return bestMoveRoot;
    }

    // Custom function which decides how long to spend on each turn based on the number of pieces remaining
    double getTimeForTurn() {
        int materialCount = m_board.IsWhiteToMove ? BitboardHelper.GetNumberOfSetBits(m_board.WhitePiecesBitboard) : BitboardHelper.GetNumberOfSetBits(m_board.BlackPiecesBitboard);
        return Math.Min((-14.0625 * (materialCount - 16.4327) * (materialCount + 0.43274)), m_timer.MillisecondsRemaining / 30);
    }

    // ComPresSTO
    int Evaluate() {
        int mg = 0, eg = 0, phase = 0;

        foreach (bool stm in new[] { true, false }) {
            for (var p = PieceType.Pawn; p <= PieceType.King; p++) {
                int piece = (int)p, ind;
                ulong mask = m_board.GetPieceBitboard(p, stm);
                while (mask != 0) {
                    phase += piecePhase[piece];
                    ind = 128 * (piece - 1) + BitboardHelper.ClearAndGetIndexOfLSB(ref mask) ^ (stm ? 56 : 0);
                    mg += getPstVal(ind) + pieceValues[piece];
                    eg += getPstVal(ind + 64) + pieceValues[piece];
                }
            }
            mg = -mg;
            eg = -eg;
        }
        return (mg * phase + eg * (24 - phase)) / 24 * (m_board.IsWhiteToMove ? 1 : -1);
    }

    // ComPresSTO retrieving values from the piece square table
    int getPstVal(int psq) {
        return (int)(((psts[psq / 10] >> (6 * (psq % 10))) & 63) - 20) * 8;
    }

    // To save tokens, Negamax and Q-Search are in a single, combined method
    int Search(int depth, int ply, int alpha, int beta) {
        positionsEvaled++;

        bool qSearch = depth <= 0;

        if (ply > 0) {
            // Detect draw by repitition
            // Returns a draw score even if this position has only appeared once in the game history (for simplicity).
            if (m_board.GameRepetitionHistory.Contains(m_board.ZobristKey))
                return 0;

            // Skip this position if a mating sequence has already been found earlier in
            // the search, which would be shorter than any mate we could find from here.
            // This is done by observing that alpha can't possibly be worse (and likewise
            // beta can't  possibly be better) than being mated in the current position.
            alpha = Math.Max(alpha, -99999 + ply);
            beta = Math.Min(beta, 99999 - ply);
            if (alpha >= beta) return alpha;
        }

        ulong key = m_board.ZobristKey;

        // Retrieve the transposition table entry (for this position, empty if it doesnt exist)
        TTEntry entry = tt[key % entries];

        // Transposition Table cutoffs
        // If a position has been evaluated before (to an equal depth or higher) then just use the transposition table value
        if (ply > 0 && entry.key == key && entry.depth >= depth && (
            entry.bound == 3 // exact score
                || entry.bound == 2 && entry.score >= beta // lower bound, fail high
                || entry.bound == 1 && entry.score <= alpha // upper bound, fail low
        )) {
            TTused++;
            return entry.score;
        }

        int eval;

        // Quiescence search is in the same function as negamax to save tokens
        if (qSearch) {
            // If in Q-search
            // A player isn't forced to make a capture (typically), so see what the evaluation is without capturing anything.
            // This prevents situations where a player ony has bad captures available from being evaluated as bad,
            // when the player might have good non-capture moves available.
            eval = Evaluate();
            if (eval >= beta) return beta;
            alpha = Math.Max(alpha, eval);
        }

        // Generate moves, only captures in qsearch
        Move[] moves = m_board.GetLegalMoves(qSearch);
        OrderMoves(moves);

        Move bestPositionMove = Move.NullMove;
        int bestPositionEval = -99999;
        int origAlpha = alpha;

        // If there are no moves then the board is in check, which is bad, or stalemate, which is an equal position
        if (moves.Length == 0 && !qSearch)
            return m_board.IsInCheck() ? -(99999 - ply) : 0;

        foreach (Move move in moves) {
            // Cancel the search if we go over the time allocated for this turn
            if (m_timer.MillisecondsElapsedThisTurn >= maxTime) return 99999;

            m_board.MakeMove(move);
            eval = -Search(depth - 1, ply + 1, -beta, -alpha);
            m_board.UndoMove(move);

            if (eval >= beta) {
                // Move was too good, opponent will avoid this position

                // Push to TT
                tt[key % entries] = new TTEntry(key, move, depth, eval, 2);

                return beta;
            }

            // Found a new best move in this position
            if (eval > bestPositionEval) {
                bestPositionEval = eval;
                bestPositionMove = move;

                // Improve alpha
                alpha = Math.Max(alpha, bestPositionEval);

                if (ply == 0) {
                    bestIterativeMove = move;
                    bestIterativeEval = eval;
                }

                // Fail-high
                if (alpha >= beta) {
                    break;
                }
            }
        }

        // Did we fail high/low or get an exact score?
        int bound = bestPositionEval >= beta ? 2 : bestPositionEval > origAlpha ? 3 : 1;

        // Push to TT
        tt[key % entries] = new TTEntry(key, bestPositionMove, depth, bestPositionEval, bound);

        return alpha;
    }

    // Move ordering to optimize alpha-beta pruning
    void OrderMoves(Move[] moves) {
        int[] moveScores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++) {
            Move move = moves[i];
            moveScores[i] = 0;

            // check Transposition table move first
            if (move == tt[m_board.ZobristKey % entries].move)
                moveScores[i] += 10000;

            // MVV-LVA (Most valuable victim, least valuable attacker)
            if (move.IsCapture)
                // The * 100 is used to make even 'bad' captures like QxP rank above non-captures
                moveScores[i] += 100 * (int)move.CapturePieceType - (int)move.MovePieceType;
        }

        // Sort highest scored moves first
        Array.Sort(moveScores, moves);
        Array.Reverse(moves);
    }
}