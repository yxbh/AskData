# Podcast Data

This page covers how to get podcast data read for AskData.

## Install podcast-dl

Just get it from <https://github.com/lightpohl/podcast-dl/releases>.

You can try pip install but it might run into pprint related dependency errors on Windows.

1. `python3 -m venv venv`
1. `.\venv\Scripts\activate`
1. `python -m pip install podcast-dl `

## Getting the podcast

Here, I put podcast-dl into a subdir called `3p_bins`.

1. `cd .\3p_bins\`
1. `.\podcast-dl-10.5.0-win-x64.exe --url https://feeds.simplecast.com/_7lcF_6g --out-dir ../podcasts/ --include-meta --include-episode-meta --include-episode-transcripts --include-episode-images --episode-digits 4`

## Now , lets get ready for transcribing

We will use whisper, which means we need:

- ffmpeg: <https://www.ffmpeg.org/download.html#build-windows>
- whisper: <https://github.com/openai/whisper>
- PyTorch: <https://pytorch.org/get-started/locally/> (Optional: for GPU acceleration)

For ffmpeg:

1. Down ffmpeg and stick the bin's into your environment `PATH` variable.
1. Make sure ffmpeg is accessible from your terminal.

For whisper:

1. `.\venv\Scripts\activate`
1. `pip install git+https://github.com/openai/whisper.git`

For faster-whisper (faster alternative to whisper):

1. `.\venv\Scripts\activate`
1. `pip install faster-whisper`

You will also need to install CUDA Toolkit for this: <https://developer.nvidia.com/cuda-downloads>  
The runtime components are really all we need during the installation process.

You may fail with an error like below if you don't install CUDA Toolkit:

```log
Library cublas64_12.dll is not found or cannot be loaded
```

For PyTorch:

1. `.\venv\Scripts\activate`
1. `pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu118`

## Lets get transcribing

Running whisper for the first time will make it pull down a model.

Example:

1. `.\venv\Scripts\activate`
1. `whisper '.\podcasts\20211215-Ep001_ Home Alone (1990) + Time Out.mp3' --language English --model medium --output_dir podcasts_transcription\`

Alternatively, use [transcribe.py](./transcribe.py) to call faster-whisper instead which will run much faster (depending on the model used).

1. `.\venv\Scripts\activate`
1. `python transcribe.py`
