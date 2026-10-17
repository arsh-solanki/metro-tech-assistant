# MetroTech Assist

An **offline AI troubleshooting assistant for metro technicians**. Technicians describe a fault by text or photo and get step-by-step, manual-based troubleshooting guidance in **Gujarati or English**, with no internet connection required.

## Why

Technicians often work in depots and tunnels with poor connectivity and need quick answers from long maintenance manuals. MetroTech Assist brings those manuals to them as an AI assistant that runs fully on local infrastructure.

## Features

- Offline operation: AI inference runs locally, with no cloud API calls
- Manual-based answers using Retrieval-Augmented Generation (RAG), so responses come from the maintenance manuals
- Gujarati and English support
- Text and photo input for describing faults
- ASP.NET Core Web API backend with SQL Server for data storage
- React frontend

## Tech Stack

| Layer | Technology |
| --- | --- |
| Frontend | React |
| Backend | ASP.NET Core Web API (C#) |
| Database | SQL Server |
| AI | Local Gemma model with RAG |

## How It Works

1. The technician sends a text query or photo from the React app.
2. The Web API receives the request and retrieves the relevant sections from the indexed manuals (RAG).
3. The local Gemma model uses that context to generate troubleshooting steps in the selected language.
4. The API returns the answer to the app.

## Getting Started

### Prerequisites

- .NET SDK
- Node.js
- SQL Server
- A local Gemma model runtime

### Run locally

1. Clone the repository

   ```bash
   git clone https://github.com/arsh-solanki/metro-tech-assistant.git
   ```

2. Backend: set the SQL Server connection string in `appsettings.json`, then

   ```bash
   dotnet ef database update
   dotnet run
   ```

3. Frontend

   ```bash
   npm install
   npm start
   ```

## Status

The backend Web API is complete. Frontend and further improvements are in progress.

## Author

**Arsh Solanki**
GitHub: [@arsh-solanki](https://github.com/arsh-solanki)
