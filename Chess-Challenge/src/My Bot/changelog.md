# Changelog

## V0.0
#### Token count: 31
Initial release
* Plays first legal move

```bash
Score of MyBot vs EvilBot: 0 - 80 - 7  [0.040] 87
...      MyBot playing White: 0 - 36 - 6  [0.071] 42
...      MyBot playing Black: 0 - 44 - 1  [0.011] 45
...      White vs Black: 44 - 36 - 7  [0.546] 87
Elo difference: -551.0 +/- 159.1, LOS: 0.0 %, DrawRatio: 8.0 %
SPRT: llr -3 (-101.9%), lbound -2.94, ubound 2.94 - H0 was accepted
```


## V1.0
#### Token count: 289
Initial Negamax bot
* Alpha-Beta pruning
* Depth 4 search
* Material value evaluation
* Mate and Draw detection

```bash
Score of MyBot vs EvilBot: 69 - 0 - 10  [0.937] 79
...      MyBot playing White: 33 - 0 - 8  [0.902] 41
...      MyBot playing Black: 36 - 0 - 2  [0.974] 38
...      White vs Black: 33 - 36 - 10  [0.481] 79
Elo difference: 468.1 +/- 121.5, LOS: 100.0 %, DrawRatio: 12.7 %
SPRT: llr 2.98 (101.1%), lbound -2.94, ubound 2.94 - H1 was accepted
```