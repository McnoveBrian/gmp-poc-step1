"""Transcribe meeting audio with faster-whisper; write SRT + plain transcript."""
from __future__ import annotations

import argparse
import sys
from datetime import timedelta
from pathlib import Path


def fmt_ts(seconds: float) -> str:
    td = timedelta(seconds=max(0.0, seconds))
    total = int(td.total_seconds())
    h, rem = divmod(total, 3600)
    m, s = divmod(rem, 60)
    ms = int((seconds - int(seconds)) * 1000)
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("audio", type=Path)
    p.add_argument("-o", "--outdir", type=Path, default=None)
    p.add_argument("--model", default="small")
    p.add_argument("--device", default="cpu")
    p.add_argument("--compute-type", default="int8")
    p.add_argument("--language", default="en")
    args = p.parse_args()

    audio = args.audio.resolve()
    outdir = (args.outdir or audio.parent).resolve()
    outdir.mkdir(parents=True, exist_ok=True)
    stem = audio.stem

    from faster_whisper import WhisperModel

    model_ref = args.model
    print(f"Loading model={model_ref} device={args.device} compute={args.compute_type}", flush=True)
    model = WhisperModel(model_ref, device=args.device, compute_type=args.compute_type)

    print(f"Transcribing: {audio}", flush=True)
    segments, info = model.transcribe(
        str(audio),
        language=args.language,
        vad_filter=True,
        beam_size=5,
        word_timestamps=False,
    )
    print(
        f"Detected language={info.language} prob={info.language_probability:.3f} "
        f"duration={info.duration:.1f}s",
        flush=True,
    )

    lines: list[str] = []
    srt_parts: list[str] = []
    for i, seg in enumerate(segments, start=1):
        text = seg.text.strip()
        if not text:
            continue
        start = seg.start
        end = seg.end
        stamp = f"[{int(start // 60):02d}:{int(start % 60):02d}]"
        lines.append(f"{stamp} {text}")
        srt_parts.append(f"{i}\n{fmt_ts(start)} --> {fmt_ts(end)}\n{text}\n")
        print(f"{stamp} {text}", flush=True)

    transcript_path = outdir / f"{stem}.transcript.md"
    srt_path = outdir / f"{stem}.srt"
    body = "\n\n".join(lines)
    transcript_path.write_text(
        f"# Meeting transcript — {stem}\n\n"
        f"- Source: `{audio.name}`\n"
        f"- Model: faster-whisper `{args.model}` ({args.device}/{args.compute_type})\n"
        f"- Language: {info.language} ({info.language_probability:.3f})\n"
        f"- Duration: {info.duration / 60:.1f} min\n\n"
        f"---\n\n{body}\n",
        encoding="utf-8",
    )
    srt_path.write_text("\n".join(srt_parts), encoding="utf-8")
    print(f"\nWrote {transcript_path}", flush=True)
    print(f"Wrote {srt_path}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
