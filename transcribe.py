import dataclasses
from faster_whisper import WhisperModel
import os
import json
import torch

# load list of podcast audio files from the `podcasts` subdirectory
podcast_dir = os.path.join(os.path.dirname(__file__), "podcasts")
audio_files = [os.path.join(podcast_dir, f) for f in os.listdir(podcast_dir) if f.lower().endswith(('.mp3', '.wav', '.m4a', '.flac'))]

# Create output directory if it doesn't exist
output_dir = os.path.join(os.path.dirname(__file__), "podcasts_transcription")
os.makedirs(output_dir, exist_ok=True)


if torch.cuda.is_available():
    print("CUDA is available. Using GPU for inference.")
    compute_type = "float16"
else:
    print("CUDA is not available. Using CPU for inference.")
    compute_type = "int8"

model = WhisperModel("distil-small.en", compute_type=compute_type)  # or "float16" if GPU

for audio_file in audio_files:
    base_name = os.path.splitext(os.path.basename(audio_file))[0]
    transcript_json_path = os.path.join(output_dir, f"{base_name}.json")
    if os.path.exists(transcript_json_path):
        print(f"Transcription for {audio_file} already exists at {transcript_json_path}. Skipping.")
        continue

    segments, info = model.transcribe(audio=audio_file)
    print(f"Transcription for {audio_file}:")
    segment_list = []
    for segment in segments:
        start_time = f"{int(segment.start // 3600):02d}:{int((segment.start % 3600) // 60):02d}:{int(segment.start % 60):02d}.{int((segment.start % 1) * 1000):03d}"
        end_time = f"{int(segment.end // 3600):02d}:{int((segment.end % 3600) // 60):02d}:{int(segment.end % 60):02d}.{int((segment.end % 1) * 1000):03d}"
        print(f"[{start_time} -> {end_time}] {segment.text}")
        segment_dict = dataclasses.asdict(segment)
        segment_dict.pop("tokens")
        segment_dict["start_time"] = start_time
        segment_dict["end_time"] = end_time
        segment_list.append(segment_dict)
    print()

    # Write segments to JSON file
    with open(transcript_json_path, "w", encoding="utf-8") as f:
        json.dump(segment_list, f, ensure_ascii=False, indent=2)
    print(f"Transcription segments saved to {transcript_json_path}\n")
