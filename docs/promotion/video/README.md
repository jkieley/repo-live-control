# RepoCommandConsole screenshot walkthrough

For the current 2.2 video with genuine moving gameplay and game audio, see [GAMEPLAY.md](GAMEPLAY.md). This document describes the earlier screenshot walkthrough.

`repo-command-console-demo.mp4` is a 56-second silent, 1280 × 720, 24 fps H.264 video with web-friendly `yuv420p` pixels and MP4 fast-start metadata. It uses the selected Console Companion mascot, genuine 2.0.0 gameplay captures, edited closeups, and separate text cards for the documented 2.1.0 commands. The distinction appears in the video, captions, metadata, and publishing copy. No gameplay, game audio, or command results were synthesized.

## Deliverables

- `repo-command-console-demo.mp4`: final video.
- `repo-command-console-demo.srt`: English accessibility captions.
- `repo-command-console-demo-poster.jpg`: matching poster/thumbnail.
- `publishing-copy.md`: title, description, timestamps, and short caption.
- `build_demo.py`: reproducible render and encode source.
- `video-manifest.json`: dimensions, duration, source/output SHA-256 hashes, and decode result.
- `video-verification.txt`: encoder stream probe and complete decode verification.

Review frames are generated in `review-frames/` and kept out of Git. They contain the exact chapter designs before video compression.

## Rebuild

Requirements: Python 3.10 or newer, Pillow, and FFmpeg with `libx264`. The optional free `imageio-ffmpeg` Python package supplies an FFmpeg binary if FFmpeg is not on PATH. The build uses Windows Segoe UI and Consolas fonts where available, with DejaVu fallbacks on Linux.

```powershell
python -m pip install Pillow imageio-ffmpeg
python docs/promotion/video/build_demo.py
```

Or pass an existing encoder explicitly:

```powershell
python docs/promotion/video/build_demo.py --ffmpeg C:/path/to/ffmpeg.exe
```

Render review cards without encoding:

```powershell
python docs/promotion/video/build_demo.py --stills-only
```

The script reads only the selected original mascot and three JPG files in `docs/promotion/screenshots/`; it writes only within this video directory. Frames preserve captured UI content, with crop/scale transformations for legibility. The screenshot sources and 2.1.0 command reference should be reviewed before reusing this video for a later release. Encoding is deterministic for fixed inputs, fonts, Pillow/FFmpeg versions, and settings; metadata records source hashes to audit which assets were used.

This copy is ready to upload once the associated 2.1.0 release is available. Publication to a video or community account is a separate action.
