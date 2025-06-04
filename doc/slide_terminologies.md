# Terminologies 💬

## Vector Database 📚

A specialized database designed to store, index, and query high-dimensional vector representations (a.k.a. embeddings) of data like text, images, or audio. In the context my demo, it’s used to store embeddings and allow us to search by “semantic similarity” instead of exact matches.

## Embedding 🛌

A vector (array of floats) that represents the meaning of some arbitrary content. In the context of this discussion, an embedding represents meaning of a chunk of text in a high dimensional float array.

## Ollama 🦙

A tool for running large language models (LLMs) and embedding models locally on your machine, with minimal setup.

## Microsoft.KernelMemory 🍢

An open-source, multi-modal AI service/library that provides API for:

1. Ingest and Process Data
1. Perform Semantic Search
1. Support RAG Workflows
1. Provide Flexible Deployment Options
In this discussion, I am using it as a serverless API wrapper over Ollama and a vector database.