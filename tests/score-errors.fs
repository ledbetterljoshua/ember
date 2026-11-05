\ needs: lib/tracker.fs
\ A reader whose "]" went missing stops at the next reader's opening word.
ARRANGE[ a b c
CHORDS[ Am F ]
\ A typo in a melody, with the bar it is in.
TRACK[ A-4 ... | C-5 X-5 ]
NOTES[ C-5 Eb5x ]
CHORDS[ Am Hm ]
\ After an error the reader's remaining lines are skipped up to its "]",
\ not interpreted one word at a time, and the file carries on after it.
TRACK[ A-4 ... |
  C-5 X-5 ... |
  E-5 ... G-5 ]
1 2 + . CR
\ RENDER refuses the broken score instead of writing a WAV from it.
: NOP ;
' NOP ' NOP RENDER
\ A reader still open at the end of the input says so.
TRACK[ A-4 C-5
