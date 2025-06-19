# AskData

A simple repo that demo's the use of Microsoft.KernelMemory + Ollama to vector index and query BYO data.

To ingest podcast data, check out the [Podcast Data](./DataPodcasts.md) page.

## Overview

Architecturally, we are using:

- Ollama as our embedding model server running locally.
- Qdrant as our vector database running in Docker.
- Microsoft.KernelMemory as the framework to interact with our data and push/pull from Ollama and Qdrant.

## Setting up the environment

First, install Ollama:

1. Install Ollama locally: <https://ollama.com/download>
1. Run `ollama pull nomic-embed-text` to download an embedding model.
1. Run `ollama serve` to start the Ollama server. This exposes the REST API for the models.  
  You will need to run this every time.

Next, install WSL and Docker:

1. Run `wsl --install`.
1. Install Docker Desktop: <https://apps.microsoft.com/detail/XP8CBJ40XLBWKX?hl=en-US&gl=AU&ocid=pdpshare>

Next, install Qdrant.

```cmd
docker run -it --rm --name qdrant \
  -p 6333:6333 \
  -v $(pwd)/qdrant_storage:/qdrant/storage \
  qdrant/qdrant
```

In this command, `$(pwd)/qdrant_storage` refers to a directory named qdrant_storage in your current working directory. You should replace it with something more sensible for your environment.

## Building the vector database

1. Create a new appsettings.*.json file under the [AskData.KernelMemory.CLI](./AskData/AskData.KernelMemory.CLI/) project folder.
1. Define new ContentSource's under the `ContentSources` key (refer to [appsettings.json](./AskData/AskData.KernelMemory.CLI/appsettings.json)).
1. Run the [AskData.KernelMemory.CLI](./AskData/AskData.KernelMemory.CLI/AskData.KernelMemory.CLI.csproj) project to preprocess and index the defined `ContentSource`'s into the vector DB.

Right now, only the `whisper-transcript` content type is supported. Implement new `IContentProcessor` class to support others (ref: [WhisperTranscriptProcessor.cs](./AskData.KernelMemory.CLI/DataProcessor/WhisperTranscriptProcessor.cs)).

## Querying the vector database

WIP

## References

- <https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/>
- <https://code.visualstudio.com/docs/copilot/chat/mcp-servers>
