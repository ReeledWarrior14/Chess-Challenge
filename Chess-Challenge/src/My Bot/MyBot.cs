#define DEBUGGING

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChessChallenge.API;

public class MyBot : IChessBot {

  // Piece values: null, pawn, knight, bishop, rook, queen, king
  int[] pieceValues = { 0, 100, 310, 330, 500, 900, 20000 };

  Board m_board;

  Move bestMoveRoot;

#if DEBUGGING
  int positionsEvaluated = 0;
#endif

  public Move Think(Board board, Timer timer) {
    m_board = board;

    // set a default move
    bestMoveRoot = board.GetLegalMoves()[0];

    // search for the best move
    Search(4, -100000, 100000, 0);

    return bestMoveRoot;
  }

  int Evaluate() {
    int whiteEval = CountMaterial(true);
    int blackEval = CountMaterial(false);

    // return the difference in material values (positive if white is ahead, negative if black is ahead) 
    // multiplied by 1 or -1 depending on whose turn it is
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

  // Simple Negamax search
  int Search(int depth, int alpha, int beta, int ply) {

    int bestScore = -30000;

    if (depth == 0) {
      return Evaluate();
    }

    Move[] legalMoves = m_board.GetLegalMoves();

    // If there are no moves then the board is in check, which is the worst, or stalemate, which is an equal position
    if (legalMoves.Length == 0)
      return m_board.IsInCheck() ? -(99999 - ply) : -100;
    // manually added a ply bonus to prefer checkmate in fewer moves

    foreach (Move move in legalMoves) {
      m_board.MakeMove(move);

      int score = -Search(depth - 1, -beta, -alpha, ply + 1);

      m_board.UndoMove(move);

      if (score > bestScore) {
        bestScore = score;

        if (ply == 0) {
          bestMoveRoot = move;
        }

        alpha = Math.Max(alpha, score);

        if (alpha >= beta) {
          break;
        }
      }
    }

    return bestScore;
  }
}