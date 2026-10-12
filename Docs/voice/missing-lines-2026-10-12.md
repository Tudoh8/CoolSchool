# Missing voice lines: handover

> **Done (2026-10-12):** all 55 lines were generated and linked in one batch (55 generated, 0 failed, 1,456 characters). No Dutch line is left without a clip. This file is kept as a record.

Written 2026-10-12. These **55 Dutch lines (1,456 characters of DeepVoice quota)** have text in `Assets/CoolSchool/Voice/Lines.asset` but no clip yet. In the game they fall back to a subtitle. They could not be generated on the first PC because its fresh checkout has no DeepVoice invoice number. English is not generated for any line yet, and is not part of this job.

The list below is everything with no Dutch clip after the 2026-10-12 scene rebuild. The generator skips lines that already have a clip, so running it is safe even if some of these were made in the meantime.

## Before you start (on the other PC)

1. **Get this working tree there.** The alien variety, race and back-button changes are **not committed** on the first PC. The lines below exist only in this working tree's `Editor/Voice/VoiceLineSeeder.cs` and `Voice/Lines.asset`. Commit them on the first PC and carry them over with git (don't push to GitHub without deciding to), or copy the repo folder.
2. **Unity 6000.3.24f1** must be installed at `C:\Program Files\Unity\Hub\Editor\6000.3.24f1\`. `Tools/unity-batch.ps1` expects that path, so edit the `$unity` line there if it is elsewhere.
3. **The DeepVoice invoice number.** Give it in one of these two ways:
   - Put it as the first line of `CoolSchool/UserSettings/DeepVoiceInvoice.txt`. That file is git-ignored; never commit it.
   - Or enter it once in the DeepVoice window inside Unity, which stores it in PlayerPrefs.

   A third way, the `-deepVoiceInvoice` command-line argument, gets echoed into the batch log, so avoid it.
4. **Close the Unity editor.** Batch mode refuses to run while the project is open.

## Generate

From the repository root:

```
powershell -ExecutionPolicy Bypass -File Tools/unity-batch.ps1 -Method CoolSchool.EditorTools.VoiceBatchGenerator.GenerateMissingFromCommandLine
```

- On a fresh checkout the first run also imports the whole project, so it can take a long time before generation starts.
- It sends one request at a time with a 1.6 s pause, as the DeepVoice terms require.
- It saves `Assets/CoolSchool/Voice/Clips/nl/nl_<key with _ for .>.mp3` and links each clip into `Lines.asset` by itself.
- Voices per speaker come from `Assets/CoolSchool/Voice/VoiceGenerationSettings.asset`:
  - Teacher: Lily
  - Alien: Freya
  - Kid: Freya
  - Announcer: Dave
- The asset doesn't list the Alien and Announcer voices yet. They use the defaults in `Editor/Voice/VoiceGenerationSettings.cs`; if DeepVoice doesn't know a voice, it falls back to Lily.

## Check afterwards

- `Logs/batch-CoolSchool.EditorTools.VoiceBatchGenerator.GenerateMissingFromCommandLine.log` must end with Unity exit code 0 and list the generated lines. An exit code of 1 with "no DeepVoice invoice number found" means step 3 is missing.
- In `Voice/Lines.asset`, no `dutchClip: {fileID: 0}` should be left.
- Spot-check by ear:
  - The single-word race countdown (`Drie!`, `Twee!`, `Eén!`, `Start!`), since short single words have sounded English before.
  - `race.gate_letter.1`, which ends in "…" because the letter sound is played right after it.
- To redo one line, delete its mp3 (and its `.meta` file) and run the command again.
- Bring the new `Clips/nl/*.mp3` + `.meta` files and the updated `Lines.asset` back to the first PC (commit them). Then add the 1,456 characters to the quota note in `Docs/HANDOVER.md`.

## The lines

### Back button (3 lines, 108 NL characters)

| key | speaker | Dutch | English |
|---|---|---|---|
| `quit.ask.1` | Teacher | Wil je stoppen met dit spel? | Do you want to stop this game? |
| `quit.bye.1` | Teacher | Oké! Dan gaan we weer lekker buiten spelen. | Okay! Let's go and play outside again. |
| `quit.bye.2` | Teacher | Prima! Je mag altijd weer terugkomen. | Fine! You can always come back. |

### Soapbox race (38 lines, 847 NL characters)

| key | speaker | Dutch | English |
|---|---|---|---|
| `invite.race.1` | Teacher | Wie doet er mee met de zeepkistrace? | Who wants to join the soapbox race? |
| `race.welcome.1` | Announcer | Welkom bij de grote zeepkistrace! Stap maar in je kar! | Welcome to the big soapbox race! Hop into your cart! |
| `race.howto.1` | Announcer | Tik links of rechts om te sturen. Tik op de knop om te springen! | Tap left or right to steer. Tap the button to jump! |
| `race.count3.1` | Announcer | Drie! | Three! |
| `race.count2.1` | Announcer | Twee! | Two! |
| `race.count1.1` | Announcer | Eén! | One! |
| `race.go.1` | Announcer | Start! | Go! |
| `race.gate_number.1.1` | Announcer | Rij door de één! | Drive through the one! |
| `race.gate_number.2.1` | Announcer | Rij door de twee! | Drive through the two! |
| `race.gate_number.3.1` | Announcer | Rij door de drie! | Drive through the three! |
| `race.gate_number.4.1` | Announcer | Rij door de vier! | Drive through the four! |
| `race.gate_number.5.1` | Announcer | Rij door de vijf! | Drive through the five! |
| `race.gate_number.6.1` | Announcer | Rij door de zes! | Drive through the six! |
| `race.gate_number.7.1` | Announcer | Rij door de zeven! | Drive through the seven! |
| `race.gate_number.8.1` | Announcer | Rij door de acht! | Drive through the eight! |
| `race.gate_number.9.1` | Announcer | Rij door de negen! | Drive through the nine! |
| `race.gate_number.10.1` | Announcer | Rij door de tien! | Drive through the ten! |
| `race.gate_letter.1` | Announcer | Rij door de letter… | Drive through the letter… |
| `race.gate_right.1` | Announcer | Ja, goed zo! | Yes, well done! |
| `race.gate_right.2` | Announcer | Super! Precies goed! | Super! Exactly right! |
| `race.gate_right.3` | Announcer | Knap gedaan! | Nicely done! |
| `race.gate_wrong.1` | Announcer | Bijna! Kijk, daar was hij! | Almost! Look, there it was! |
| `race.crash.1` | Announcer | Boem! Gelukkig is het zacht! | Boom! Good thing it's soft! |
| `race.crash.2` | Announcer | Oeps! Rij maar door! | Oops! Keep going! |
| `race.splash.1` | Announcer | Plons! Lekker nat! | Splash! Nice and wet! |
| `race.animal_cat.1` | Announcer | Kijk uit, een poes! | Watch out, a cat! |
| `race.animal_dog.1` | Announcer | Kijk uit, een hond! | Watch out, a dog! |
| `race.animal_hen.1` | Announcer | Kijk uit, een kip met kuikentjes! | Watch out, a hen with her chicks! |
| `race.animal_rabbit.1` | Announcer | Kijk uit, een konijn! | Watch out, a rabbit! |
| `race.ramp.1` | Announcer | Een schans! Hup, de lucht in! | A ramp! Up into the air! |
| `race.final.1` | Announcer | Bijna bij de finish! Zet hem op! | Almost at the finish! Go for it! |
| `race.win.1` | Announcer | Jij hebt gewonnen! Hoera voor de kampioen! | You won! Hooray for the champion! |
| `race.second.1` | Announcer | Wat een spannende race! Jullie waren allemaal supersnel! | What an exciting race! You were all super fast! |
| `race.end.1` | Announcer | Tot de volgende race! | See you at the next race! |
| `race.rival.1` | Kid | Ik ga je inhalen! | I'm going to catch you! |
| `race.rival.2` | Kid | Wie is er het snelst? | Who's the fastest? |
| `race.bump.1` | Kid | Wiee! Ik draai rondjes! | Wheee! I'm spinning! |
| `race.bump.2` | Kid | Hihi, botsauto! | Hehe, bumper car! |

### Alien variety (14 lines, 501 NL characters)

| key | speaker | Dutch | English |
|---|---|---|---|
| `aliens.arrive.1` | Teacher | Kijk goed, ze komen overal vandaan! | Look closely, they're coming from everywhere! |
| `aliens.big_hint.1` | Teacher | Wat een grote alien! Flits hem een paar keer! | What a big alien! Zap it a few times! |
| `aliens.big_hit.1` | Alien | Hihi, ik krimp! | Hehe, I'm shrinking! |
| `aliens.big_hit.2` | Alien | Oei, ik word kleiner! | Oops, I'm getting smaller! |
| `aliens.tiny_hint.1` | Teacher | Die kleine is snel! Als hij moe is, kun je hem flitsen! | That little one is fast! When he's tired, you can zap him! |
| `aliens.tiny_tease.1` | Alien | Hihi, vang me dan! | Hehe, catch me! |
| `aliens.tiny_tired.1` | Alien | Pfoe, ik ben moe! | Phew, I'm tired! |
| `aliens.shy_hint.1` | Teacher | Daar verstopt er eentje! Loop ernaartoe! | One is hiding there! Walk over to it! |
| `aliens.shy_peek.1` | Alien | Kiekeboe! | Peekaboo! |
| `aliens.robot_zapped.1` | Alien | Bliep bliep! Robotdans! | Beep beep! Robot dance! |
| `aliens.rescue_rocket.1` | Teacher | Help! De baby-alien zit bovenop de raket! Spring op de trampoline! | Help! The baby alien is on top of the rocket! Jump on the trampoline! |
| `aliens.rescue_bubble.1` | Teacher | Kijk! De baby-alien zweeft in een bel! Spring omhoog en pak hem! | Look! The baby alien is floating in a bubble! Jump up and grab him! |
| `aliens.rescue_slime.1` | Teacher | Oh nee, de baby-alien zit vast in het slijm! Flits het slijm weg! | Oh no, the baby alien is stuck in the slime! Zap the slime away! |
| `aliens.slime_free.1` | Alien | Dankjewel! Ik ben weer vrij! | Thank you! I'm free again! |
