# ImagePilot_Agent

Local dashboard for preparing prompt batches and controlling a dedicated browser profile for image generation websites.

## Features

- Project, provider, batch run, and prompt job management
- Prompt template expansion with JSON variables such as `{{style}}`
- Prompt category presets for character design, cartoon art, pure-white backgrounds, product cutouts, and food posters
- Optional OpenAI-compatible external API for improving image-generation prompts
- Start, pause, resume, stop, retry, and manual completion actions
- Playwright browser profiles stored separately under `browser-profiles/`
- Configurable provider selectors with manual fallback
- SQL Server settings, connection test, and schema initialization
- SQL passwords stored in Windows Credential Manager, never in JSON
- External prompt API keys stored in Windows Credential Manager, never in JSON
- Local agent endpoints under `/api/agent-tools/image-batch`

The app starts in manual mode for every provider. Log in yourself in the dedicated browser profile. Enable automation only after checking the selectors for the provider website.

## Run

```powershell
cd frontend
cmd /c npm install
cd ..
.\start.ps1
```

Open `http://localhost:5173`.

## Development

```powershell
dotnet run --project backend\ImagePilot.Api.csproj
cd frontend
cmd /c npm run dev
```

The API listens only on `http://localhost:5000`. The UI sends a local token in `X-ImagePilot-Token`.

## SQL Server

The dashboard works immediately with its local JSON store under `data/`. To enable SQL Server mirroring, open **SQL Server**, enter the connection settings, test the connection, save them, and click **Initialize schema**. The app creates the database if needed, applies `configs/schema.sql`, then mirrors project, provider, batch, job, and output metadata in the background.

## External Prompt AI

Open **Prompt AI API** to connect Google Gemini or a custom OpenAI-compatible API endpoint. Gemini includes a model selector and uses its native REST API. For custom providers, enter the base URL, model name, and API key. Test and save the settings, then use **Improve with external AI** in **Prompt Builder**. The API key is stored in Windows Credential Manager and is not returned to the UI.

## Browser safety

- The program does not store website passwords.
- It does not log in for you.
- It does not bypass captcha or verification pages.
- It pauses for manual action when automation cannot safely continue.
