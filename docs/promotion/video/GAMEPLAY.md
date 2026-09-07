# REPO Command Console 2.2 gameplay demo

The current demo is `repo-command-console-2.2-gameplay.mp4`: 2 minutes 8.2 seconds of genuine, edited R.E.P.O. gameplay at 1920 × 1080 and 30 fps. It shows the F2 browser, fuzzy search, equipment, loot, money bags, cosmetic cases, Apex Predator, normal item pickup, and targeted cleanup. All excerpts run at their original speed; idle waits are removed. Captured game audio is retained, with no narration or added music. The selected mascot appears only in small intro/outro overlays over gameplay.

The large MP4 and raw recordings remain in the local recording archive; distribute the final MP4 as a release asset. The earlier `repo-command-console-demo.mp4` is a separate, older screenshot walkthrough.

Public destinations: [YouTube gameplay video](https://youtu.be/j_Qd60MG6VA) and [demo website](https://repo-command-console.jkieley543940.chatgpt.site). On September 7, 2026, YouTube confirmed publication and anonymous oEmbed returned HTTP 200 with the correct title and embeddable player. English captions and the custom gameplay poster are saved. The website returned HTTP 200 with the actual privacy-enhanced YouTube embed and VideoObject metadata. The final YouTube title is **REPO Command Console 2.2 — Spawn Items, Loot & Enemies | Real Gameplay**.

YouTube requires the channel's one-time verification before external description links become clickable; the plain URLs remain visible.

## Upload assets

- Video: `.local/recordings/final-edit/repo-command-console-2.2-gameplay.mp4`.
- Captions: `repo-command-console-2.2-gameplay.srt` in this directory.
- Chapters: `repo-command-console-2.2-gameplay.chapters.txt`.
- Duration, source cuts, audio offsets, output hash: `repo-command-console-2.2-gameplay.metadata.json`.
- Poster: `repo-command-console-gameplay-poster.jpg`, an unmodified capture of the full spawn browser.
- Suggested title and description: `gameplay-publishing-copy.md`.

## Rebuild

Requirements: Python 3.10+, Pillow, FFmpeg with libx264, and Windows Segoe UI fonts (or DejaVu on Linux). From the repository root:

```powershell
python docs/promotion/video/build_gameplay.py --ffmpeg C:/path/to/ffmpeg.exe --plan docs/promotion/video/gameplay-edit-plan.json --out .local/recordings/final-edit/repo-command-console-2.2-gameplay.mp4
```

The script expects `equipment-01`, `loot-01`, `bag-enemy-01`, and `enemy-apex-final` capture sets in `.local/recordings/`. Each set contains `.mp4`, `.wav`, `.audio.log`, and `.video-start.json` files. Pass `--sources` to use another source directory. Raw footage is not included in the public repository. The approved mascot master is read from `docs/promotion/logo-options/option-b-console-companion.png`.

The edit plan specifies every source in/out time and overlay. The renderer uses cached segments, original-speed video, and per-take process-clock estimates to align output-loopback audio. It reduces audio by about 1.5 dB, applies short boundary fades, and limits peaks. It does not synthesize gameplay or command results. A fixed encoder, fonts, and source files are needed for matching output.

## Verification

Final output: **67,646,090 bytes**, **3,846 video frames**, **128.2 seconds**. H.264/yuv420p video, stereo 48 kHz AAC audio, and MP4 fast-start metadata. Full FFmpeg decode succeeded and the concat warning log was empty. Source cuts and representative encoded intro, item, loot, case, enemy, and cleanup frames were visually reviewed. Captions end at `00:02:08,200`; all five chapters are at least 10 seconds long.

SHA-256: `f97fe4962d751c7c1ce61eab859c1fc6ecf00461384397fe808591094ee35b60`.

Audio capture logs and signal levels were checked. Audio was captured from desktop output without a microphone; listening-based audio review was unavailable in the editing environment.
