# Fable Shot Detection

This webapp was built for an assigment at the [FH Kufstein](https://www.fh-kufstein.ac.at/) using [Fable](http://fable.io/), [Elmish](https://fable-elmish.github.io/) and [Suave](https://suave.io) - (heavily inspired by the [SAFE-BookStore](https://github.com/SAFE-Stack/SAFE-BookStore)).

It is currently still __WIP__ and mainly a play-thing for me to learn to work with a full F# web stack (and of course how to shoehorn it into a CI/CD Azure workflow without paying a dime).

If you have any questions and/or suggestions hit me up at twitter [@GBeyerle](https://twitter.com/GBeyerle).

## Regarding Shot Detection

The implemented algorithm isn't highly sophisticated. If two consecutive frames are very different (different meaning that the accumulated luma values of all pixels vary to a great extent) it is assumend that a hard cut happened and a key frame (the first frame in the new shot) is extracted (if you try out the IT trailer in this example you will see that this method has its problems). A fade is detected by meassuring if the difference between two consecutive frames is higher than an threshold value (can be adjusted as this value is subject to many factors). In this case the following frame changes should also show a difference higher than the threshold value. If the accumulated threshold deltas in a sequence are bigger than the cut threshold (value which would signal a hard cut) a fade is detected.

## Known Bugs

- Going back to a previous point in the video is not caught by the vizualizer of frame differences which leads to a wrong visualization
- Going back will also result in key frame duplicates as they are currently not bound to the play time
- The styling is still very very very bad (yes, I'd classify this as a bug)
- The unfixed positions of the sliders can cause jumpy behavior for users (as the label length can change)