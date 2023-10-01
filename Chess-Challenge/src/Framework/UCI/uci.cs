using System;
using ChessChallenge.API;
using ChessChallenge.Application;
using ChessChallenge.Application.APIHelpers;
using ChessChallenge.Chess;


// Example cutechess command:
// cutechess-cli -engine name="MyBot" cmd="./Chess-Challenge" arg="uci" arg="MyBot" -engine name="EvilBot" cmd="./Chess-Challenge" arg="uci" arg="EvilBot" -each proto=uci tc=60+0 -concurrency 10 -maxmoves 200 -rounds 100 -ratinginterval 20 -games 2 -openings file="Pohl-opening-book.pgn" format=pgn
// With sprt:
// cutechess-cli -engine name="MyBot" cmd="./Chess-Challenge" arg="uci" arg="MyBot" -engine name="EvilBot" cmd="./Chess-Challenge" arg="uci" arg="EvilBot" -each proto=uci tc=10+0.1 -concurrency 10 -maxmoves 200 -rounds 10000 -ratinginterval 20 -games 2 -repeat -openings file="Pohl-opening-book.pgn" format=pgn policy=round -sprt elo0=0 elo1=5 alpha=0.05 beta=0.05

namespace ChessChallenge.UCI {
    class UCIBot {
        IChessBot? bot;
        readonly ChallengeController.PlayerType type;
        readonly Chess.Board board;

        public UCIBot(IChessBot bot, ChallengeController.PlayerType type) {
            this.bot = bot;
            this.type = type;
            board = new Chess.Board();
        }

        void PositionCommand(string[] args) {
            int idx = Array.FindIndex(args, x => x == "moves");
            if (idx == -1) {
                if (args[1] == "startpos") {
                    board.LoadStartPosition();
                }
                else {
                    board.LoadPosition(String.Join(" ", args.AsSpan(1, args.Length - 1).ToArray()));
                }
            }
            else {
                if (args[1] == "startpos") {
                    board.LoadStartPosition();
                }
                else {
                    board.LoadPosition(String.Join(" ", args.AsSpan(1, idx - 1).ToArray()));
                }

                for (int i = idx + 1; i < args.Length; i++) {
                    // this is such a hack
                    API.Move move = new(args[i], new API.Board(board));
                    board.MakeMove(new Chess.Move(move.RawValue), false);
                }
            }

            string fen = FenUtility.CurrentFen(board);
            Console.WriteLine(fen);
        }

        void GoCommand(string[] args) {
            int wtime = 0, btime = 0;
            API.Board apiBoard = new(board);
            Console.WriteLine(FenUtility.CurrentFen(board));
            Console.WriteLine(apiBoard.GetFenString());
            for (int i = 0; i < args.Length; i++) {
                if (args[i] == "wtime") {
                    wtime = Int32.Parse(args[i + 1]);
                }
                else if (args[i] == "btime") {
                    btime = Int32.Parse(args[i + 1]);
                }
            }
            if (!apiBoard.IsWhiteToMove) {
                (btime, wtime) = (wtime, btime);
            }
            Timer timer = new(wtime, btime, 0);
            API.Move move = bot.Think(apiBoard, timer);
            Console.WriteLine($"bestmove {move.ToString()[7..^1]}");
        }

        void ExecCommand(string line) {
            // default split by whitespace
            var tokens = line.Split();

            if (tokens.Length == 0)
                return;

            switch (tokens[0]) {
                case "uci":
                    Console.WriteLine("id name Chess Challenge");
                    Console.WriteLine("id author AspectOfTheNoob, Sebastian Lague");
                    Console.WriteLine("uciok");
                    break;
                case "ucinewgame":
                    bot = ChallengeController.CreateBot(type);
                    break;
                case "position":
                    PositionCommand(tokens);
                    break;
                case "isready":
                    Console.WriteLine("readyok");
                    break;
                case "go":
                    GoCommand(tokens);
                    break;
            }
        }

        public void Run() {
            while (true) {
                string? line = Console.ReadLine();

                if (line == "quit" || line == "exit")
                    return;
                ExecCommand(line ?? "");
            }
        }
    }
}