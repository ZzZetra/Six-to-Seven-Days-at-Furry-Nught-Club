"""Перегнать видео (mkv из Telegram и т. п.) в mp4 H.264 для монитора камер — через ffmpeg, встроенный в Blender.

  blender -b --python convert_video.py -- convert <откуда> <куда.mp4> [процент размера, 50]
  blender -b --python convert_video.py -- probe <откуда>
"""
import bpy, sys

args = sys.argv[sys.argv.index("--") + 1:]
mode = args[0]
src = args[1]
clip = bpy.data.movieclips.load(src)
w, h = clip.size
fps = clip.fps
frames = clip.frame_duration
print(f"VIDEO {src} {w}x{h} fps={fps} frames={frames} ({frames / max(fps, 1):.2f} s)")
if mode == "convert":
    out = args[2]
    scene = bpy.context.scene
    scene.sequence_editor_create()
    seqs = scene.sequence_editor.strips if hasattr(scene.sequence_editor, "strips") else scene.sequence_editor.sequences
    strip = seqs.new_movie("clip", src, channel=1, frame_start=1)
    scene.frame_start = 1
    scene.frame_end = int(clip.frame_duration)
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.resolution_percentage = int(args[3]) if len(args) > 3 else 50  # 1920×1080 → 960×540: экрану хватает
    scene.render.fps = int(round(fps))
    scene.render.fps_base = 1.0
    if hasattr(scene.render.image_settings, "media_type"):  # Blender 5: сначала «видео», потом формат
        scene.render.image_settings.media_type = "VIDEO"
    scene.render.image_settings.file_format = "FFMPEG"
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "HIGH"
    scene.render.ffmpeg.audio_codec = "NONE"
    scene.render.filepath = out
    scene.render.use_sequencer = True
    scene.render.use_compositing = False
    bpy.ops.render.render(animation=True)
    print("CONVERTED", out, "frames", scene.frame_end)
