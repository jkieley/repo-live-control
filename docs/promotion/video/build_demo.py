"""Rebuild the 56-second, silent screenshot walkthrough from repository assets.

Requirements: Python 3.10+, Pillow, and ffmpeg (or imageio-ffmpeg).
Example: python build_demo.py --ffmpeg C:/path/to/ffmpeg.exe
Use --stills-only to render the eight chapter cards for review.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
SHOTS = HERE.parent / "screenshots"
LOGO = HERE.parent / "logo-options" / "option-b-console-companion.png"
W, H, FPS = 1280, 720, 24
DURATIONS = [6, 6, 7, 8, 7, 6, 7, 9]
BG = "#0b111a"
PANEL = "#121f2d"
INK = "#f6f9fd"
MUTED = "#abb9ca"
CYAN = "#64e4f4"
GOLD = "#ffcd65"


def font(size: int, bold: bool = False, mono: bool = False):
    directory = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    name = "consola.ttf" if mono else "segoeuib.ttf" if bold else "segoeui.ttf"
    candidate = directory / name
    if not candidate.exists():
        candidate = Path("/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf" if mono else
                         "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if bold else
                         "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
    return ImageFont.truetype(str(candidate), size)


def txt(im, position, value, size=26, color=INK, bold=False, mono=False):
    ImageDraw.Draw(im).text(position, value, font=font(size, bold, mono), fill=color)


def box(im, rect, fill=PANEL, outline=None, radius=18):
    ImageDraw.Draw(im).rounded_rectangle(rect, radius=radius, fill=fill, outline=outline, width=2)


def base(index, label="SCREENSHOT WALKTHROUGH", version="GAMEPLAY CAPTURES: 2.0.0"):
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 10, H), fill=CYAN)
    txt(im, (64, 32), label, 18, CYAN, True)
    txt(im, (920, 34), f"{index:02d} / 08", 18, MUTED)
    d.line((64, 77, 1216, 77), fill="#263849", width=1)
    d.line((64, 651, 1216, 651), fill="#263849", width=1)
    txt(im, (64, 671), version, 17, MUTED)
    txt(im, (852, 671), "RepoCommandConsole", 17, MUTED, True)
    for n in range(8):
        d.rounded_rectangle((1070 + n * 19, 679, 1080 + n * 19, 685), radius=3,
                            fill=CYAN if n < index else "#334352")
    return im


def mascot(im, rect):
    source = Image.open(LOGO).convert("RGBA")
    source.thumbnail((rect[2], rect[3]), Image.Resampling.LANCZOS)
    im.paste(source, (rect[0], rect[1]), source)


def capture(im, filename, crop, rect):
    """Only crop and resize genuine captures; never alter their UI contents."""
    source = Image.open(SHOTS / filename).convert("RGB").crop(crop)
    source = source.resize((rect[2], rect[3]), Image.Resampling.LANCZOS)
    im.paste(source, (rect[0], rect[1]))
    ImageDraw.Draw(im).rectangle((rect[0], rect[1], rect[0]+rect[2], rect[1]+rect[3]),
                                outline="#415566", width=2)


def command(im, y, line, size=34):
    box(im, (64, y, 1216, y + 87), "#162a38", "#2d5364", 16)
    txt(im, (90, y + 17), line, size, CYAN, mono=True)


def chapters():
    slides = []
    im = base(1, version="GAMEPLAY CAPTURES: 2.0.0  |  NEW COMMANDS: 2.1.0")
    txt(im, (64, 132), "REPO", 76, INK, True)
    txt(im, (64, 220), "COMMAND CONSOLE", 52, INK, True)
    txt(im, (68, 310), "Spawn a loadout. Recover the team.", 29, MUTED)
    box(im, (66, 386, 722, 461), "#173442", "#326172")
    txt(im, (92, 402), "One searchable F2 console", 31, CYAN, True)
    txt(im, (68, 506), "A 56-second screenshot walkthrough", 25, GOLD)
    txt(im, (68, 550), "Real captures + new-command text examples", 22, MUTED)
    mascot(im, (862, 165, 326, 326))
    slides.append(im)

    im = base(2)
    txt(im, (64, 103), "01  Open with F2", 48, INK, True)
    txt(im, (64, 174), "In a lobby or run", 26, MUTED)
    box(im, (64, 244, 258, 381), "#193949", "#367487")
    txt(im, (114, 259), "F2", 74, CYAN, True)
    txt(im, (64, 420), "Your console has its", 27)
    txt(im, (64, 459), "own input window.", 27)
    txt(im, (64, 526), "F2 toggles · Escape closes", 22, GOLD)
    capture(im, "repo-command-console-autocomplete.jpg", (1260, 104, 2182, 724), (470, 145, 746, 502))
    slides.append(im)

    im = base(3)
    txt(im, (64, 103), "02  Find it with autocomplete", 46, INK, True)
    txt(im, (64, 176), "Type part of a name. Select with ↑ / ↓. Accept with Tab.", 27, MUTED)
    txt(im, (64, 234), "ACTUAL CONSOLE CLOSEUP", 17, GOLD, True)
    capture(im, "repo-command-console-autocomplete.jpg", (1270, 114, 2170, 337), (64, 270, 1152, 285))
    txt(im, (64, 586), "Tab inserts the exact target from your live game catalog.", 25, CYAN)
    slides.append(im)

    im = base(4)
    txt(im, (64, 103), "03  Spawn. Read the result.", 47, INK, True)
    txt(im, (64, 176), "This capture shows three Strength Upgrades spawned.", 27, MUTED)
    txt(im, (64, 239), "COMMAND CAPTURE", 17, GOLD, True)
    capture(im, "repo-command-console-spawn-success.jpg", (1275, 196, 2154, 244), (64, 274, 1152, 63))
    txt(im, (64, 367), "RESULT CAPTURE", 17, GOLD, True)
    capture(im, "repo-command-console-spawn-success.jpg", (1275, 487, 2154, 634), (64, 402, 1152, 193))
    slides.append(im)

    im = base(5)
    txt(im, (64, 103), "04  Clean up your spawns", 47, INK, True)
    txt(im, (64, 176), "Despawn matching objects created by this mod.", 27, MUTED)
    txt(im, (64, 239), "COMMAND CAPTURE", 17, GOLD, True)
    capture(im, "repo-command-console-despawn-success.jpg", (1275, 196, 2154, 244), (64, 274, 1152, 63))
    txt(im, (64, 367), "RESULT CAPTURE", 17, GOLD, True)
    capture(im, "repo-command-console-despawn-success.jpg", (1275, 487, 2154, 589), (64, 402, 1152, 134))
    txt(im, (64, 580), "Normal level content is left alone.", 25, CYAN)
    slides.append(im)

    im = base(6, "NEW IN 2.1.0 · COMMAND CARDS", "TEXT EXAMPLES · NOT LIVE EXECUTION FOOTAGE")
    txt(im, (64, 103), "Help the team recover", 48, INK, True)
    command(im, 211, "/revive all")
    txt(im, (90, 315), "Revive eligible dead characters; a death head is required.", 24, MUTED)
    command(im, 393, "/heal all")
    txt(im, (90, 497), "Fully heal living targets.", 26, MUTED)
    txt(im, (64, 591), "Choose one player with autocomplete, or use all.", 25, GOLD)
    slides.append(im)

    im = base(7, "NEW IN 2.1.0 · COMMAND CARDS", "TEXT EXAMPLES · NOT LIVE EXECUTION FOOTAGE")
    txt(im, (64, 103), "Run recovery in order", 48, INK, True)
    command(im, 209, "/chain all revive heal truck", 39)
    for x, number, verb, caption in [(64, "1", "REVIVE", "Eligible dead characters"),
                                     (457, "2", "HEAL", "Heal the living targets"),
                                     (850, "3", "TRUCK", "Return to the truck")]:
        box(im, (x, 356, x + 366, 521), PANEL, "#354d5e")
        txt(im, (x+23, 374), number, 24, GOLD, True)
        txt(im, (x+66, 373), verb, 30, CYAN, True)
        txt(im, (x+23, 445), caption, 22, MUTED)
    txt(im, (64, 570), "Each group-wide step finishes before the next begins.", 25, MUTED)
    txt(im, (64, 607), "A failed step stops the chain.", 24, GOLD)
    slides.append(im)

    im = base(8, version="GAMEPLAY CAPTURES: 2.0.0  |  NEW COMMANDS: 2.1.0")
    txt(im, (64, 111), "Make the next run your own.", 47, INK, True)
    txt(im, (64, 195), "Install with Mod Manager", 39, CYAN, True)
    txt(im, (64, 265), "Find RepoCommandConsole for R.E.P.O.", 26, MUTED)
    txt(im, (64, 309), "Download → Start Modded → F2", 29, INK, True)
    mascot(im, (960, 181, 226, 226))
    box(im, (64, 438, 1216, 501), "#162a38", "#2d5364", 13)
    txt(im, (82, 451), "thunderstore.io/c/repo/p/Coollectors/RepoCommandConsole/", 28, GOLD)
    txt(im, (64, 542), "The host and command users need the mod.", 25, MUTED)
    txt(im, (64, 584), "Host-controlled multiplayer permissions.", 25, MUTED)
    slides.append(im)
    return slides


def find_ffmpeg(explicit):
    if explicit:
        return explicit
    if shutil.which("ffmpeg"):
        return shutil.which("ffmpeg")
    try:
        import imageio_ffmpeg
        return imageio_ffmpeg.get_ffmpeg_exe()
    except ImportError as exc:
        raise SystemExit("Install ffmpeg or imageio-ffmpeg, or pass --ffmpeg PATH.") from exc


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ffmpeg")
    parser.add_argument("--stills-only", action="store_true")
    args = parser.parse_args()
    review = HERE / "review-frames"
    review.mkdir(parents=True, exist_ok=True)
    slides = chapters()
    for i, slide in enumerate(slides, 1):
        slide.save(review / f"chapter-{i:02d}.png")
    slides[0].save(HERE / "repo-command-console-demo-poster.jpg", quality=94, subsampling=0)
    if args.stills_only:
        print("Rendered 8 chapter cards.")
        return
    ffmpeg = find_ffmpeg(args.ffmpeg)
    output = HERE / "repo-command-console-demo.mp4"
    command_line = [ffmpeg, "-y", "-hide_banner", "-loglevel", "warning", "-f", "rawvideo",
                    "-vcodec", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
                    "-i", "-", "-an", "-c:v", "libx264", "-preset", "medium", "-crf", "21",
                    "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-metadata",
                    "title=REPO Command Console | Screenshot Walkthrough + 2.1.0 Command Examples",
                    "-metadata", "comment=Edited genuine 2.0.0 screenshots; 2.1.0 features are text examples. No live execution footage or audio.",
                    str(output)]
    process = subprocess.Popen(command_line, stdin=subprocess.PIPE)
    previous = Image.new("RGB", (W, H), BG)
    try:
        for i, (slide, duration) in enumerate(zip(slides, DURATIONS), 1):
            static_frame = slide.tobytes()
            fade_frames = 8
            for frame in range(duration * FPS):
                if frame < fade_frames:
                    alpha = (frame + 1) / fade_frames
                    alpha = alpha * alpha * (3 - 2 * alpha)
                    process.stdin.write(Image.blend(previous, slide, alpha).tobytes())
                else:
                    process.stdin.write(static_frame)
            previous = slide
            print(f"Encoded chapter {i}/8", flush=True)
    finally:
        process.stdin.close()
    if process.wait() != 0:
        raise SystemExit("ffmpeg failed.")
    verification = subprocess.run([ffmpeg, "-hide_banner", "-i", str(output), "-f", "null", "-"],
                                  capture_output=True, text=True)
    (HERE / "video-verification.txt").write_text(verification.stderr, encoding="utf-8")
    manifest = {"duration_seconds": sum(DURATIONS), "width": W, "height": H, "fps": FPS,
                "frames": sum(DURATIONS) * FPS, "audio": False, "codec": "H.264 / yuv420p",
                "output_bytes": output.stat().st_size,
                "output_sha256": hashlib.sha256(output.read_bytes()).hexdigest(),
                "sources": {str(p.relative_to(REPO)): hashlib.sha256(p.read_bytes()).hexdigest()
                            for p in [LOGO, *sorted(SHOTS.glob("*.jpg"))]},
                "chapter_seconds": DURATIONS, "verification_exit_code": verification.returncode}
    (HERE / "video-manifest.json").write_text(json.dumps(manifest, indent=2)+"\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    if verification.returncode:
        raise SystemExit("Video decode verification failed.")


if __name__ == "__main__":
    main()
