using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChessChallenge.API;

public class MyBot : IChessBot {

    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 310, 330, 500, 900, 20000 };

    Board m_board;

    Move bestMoveRoot;

    int positionsEvaled;

    public Move Think(Board board, Timer timer) {
        m_board = board;

        bestMoveRoot = m_board.GetLegalMoves()[0];

        Search(4, 0, -99999, 99999);

        return bestMoveRoot;
    }

    int Evaluate() {
        int whiteEval = CountMaterial(true);
        int blackEval = CountMaterial(false);

        return (whiteEval - blackEval) * (m_board.IsWhiteToMove ? 1 : -1);
    }

    int CountMaterial(bool white) {
        int material = 0;
        for (int i = (white ? 0 : 6); i < (white ? 6 : 12); i++) {
            foreach (Piece piece in m_board.GetAllPieceLists()[i]) {
                material += pieceValues[(int)piece.PieceType];
            }
        }
        return material;
    }

    int Search(int depth, int ply, int alpha, int beta) {
        positionsEvaled++;

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

        if (depth == 0) {
            return Evaluate();
        }

        Move[] moves = m_board.GetLegalMoves();
        OrderMoves(moves);

        // If there are no moves then the board is in check, which is bad, or stalemate, which is an equal position
        if (moves.Length == 0)
            return m_board.IsInCheck() ? -(99999 - ply) : 0;

        foreach (Move move in moves) {
            m_board.MakeMove(move);
            int eval = -Search(depth - 1, ply + 1, -beta, -alpha);
            m_board.UndoMove(move);

            if (eval >= beta) {
                // Move was too good, opponent will avoid this position
                return beta;
            }

            // Found a new best move in this position
            if (eval > alpha) {
                alpha = eval;

                if (ply == 0) {
                    bestMoveRoot = move;
                }
            }
        }

        return alpha;
    }

    void OrderMoves(Move[] moves) {
        int[] moveScores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++) {
            moveScores[i] = 0;

            // MVV-LVA (Most valuable victim, least valuable attacker)
            if (moves[i].IsCapture) {
                // The * 10 is used to make even 'bad' captures like QxP rank above non-captures
                moveScores[i] += 10 * pieceValues[(int)moves[i].CapturePieceType] - pieceValues[(int)moves[i].MovePieceType];
            }
        }

        Array.Sort(moveScores, moves);
        Array.Reverse(moves);
    }
}