# Nele OpenAI Proxy

Minimaler C#-Proxy, der OpenAI-kompatible Endpunkte auf die Nele.AI API uebersetzt.

## Anforderungen
- Windows x64
- Kein .NET Runtime noetig, wenn du die Self-Contained EXE nutzt

## Funktionen
- OpenAI-kompatibel:
  - `GET /v1/models`
  - `POST /v1/chat/completions` (sync + stream)
  - `POST /v1/responses` (sync)
  - `POST /v1/audio/transcriptions`
  - `POST /v1/images/generations`
- Optionaler Default fuer Chat-Modelle via Config
- Konfiguration wird immer aus dem Ordner der EXE gelesen

## Projektstruktur
- `Proxy/` enthaelt den Web-Proxy
- `Clients/NeleDesktop/` enthaelt die WPF-Desktop-App

## Desktop App (Chat Modus)
Eine schlanke WPF-App (net9.0) fuer den direkten Zugriff auf Nele AI.

Funktionen:
- Kompaktes Widget-Fenster mit globalem Hotkey (Default: `Ctrl+Alt+Space`)
- Dark/Light Mode Toggle oben rechts
- Chat-Verlauf mit mehreren Konversationen
- Ordner fuer Chats (Drag & Drop oder Rechtsklick -> Move to folder)
- Settings-Fenster fuer API-Key, Base-URL, Model-Liste und Hotkey
  - Startet automatisch, wenn noch kein API-Key gesetzt ist

### Start (Desktop App)
```powershell
dotnet build .\NeleOpenAIProxy.sln
dotnet run --project .\Clients\NeleDesktop\NeleDesktop.csproj
```

### Desktop-Konfiguration
Die App speichert ihre Daten lokal als JSON:
- `%AppData%\NeleAIProxy\settings.json`
- `%AppData%\NeleAIProxy\conversations.json`

Beispiel `settings.json`:
```json
{
  "apiKey": "YOUR_NELE_API_KEY",
  "baseUrl": "https://api.aieva.io/api:v1/",
  "selectedModel": "google-claude-4.5-sonnet",
  "darkMode": true,
  "hotkey": "Ctrl+Alt+Space"
}
```

## Konfiguration
Lege `appsettings.Local.json` im gleichen Ordner wie die EXE ab (oder nutze `appsettings.json`). Im Repo liegen die Proxy-Configs unter `Proxy/`.

Beispiel:
```json
{
  "Urls": "http://localhost:5155",
  "Nele": {
    "BaseUrl": "https://api.aieva.io/api:v1/",
    "ApiKey": "YOUR_NELE_API_KEY",
    "DefaultChatModel": "google-claude-4.5-sonnet",
    "ForceStream": false
  }
}
```

## Start
```powershell
cd .\Proxy\bin\Release\net9.0\win-x64\publish
.\NeleOpenAIProxy.exe
```

## Beispiele

### Models
```powershell
Invoke-RestMethod http://localhost:5155/v1/models
```
Beispielausgabe:
```json
{
  "object": "list",
  "data": [
    {
      "id": "google-claude-4.5-sonnet",
      "object": "model",
      "created": 1769121744,
      "owned_by": "nele"
    }
  ]
}
```

### Chat Completions (sync)
```powershell
$body = @{
  model = "google-claude-4.5-sonnet"
  messages = @(
    @{ role = "user"; content = "Sag kurz Hallo." }
  )
  temperature = 0.2
} | ConvertTo-Json -Depth 6

Invoke-RestMethod http://localhost:5155/v1/chat/completions `
  -Method Post -ContentType "application/json" -Body $body
```
Antwort anzeigen (komplett + nur Text):
```powershell
$response = Invoke-RestMethod http://localhost:5155/v1/chat/completions `
  -Method Post -ContentType "application/json" -Body $body

$response | ConvertTo-Json -Depth 6
$response.choices[0].message.content
```
Beispielausgabe:
```json
{
  "id": "chatcmpl-0123456789abcdef",
  "object": "chat.completion",
  "created": 1769121744,
  "model": "google-claude-4.5-sonnet",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "Hallo."
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 0,
    "completion_tokens": 0,
    "total_tokens": 0
  }
}
```

### Chat Completions (stream)
```powershell
$body = @{
  model = "google-claude-4.5-sonnet"
  messages = @(@{ role = "user"; content = "Sag kurz Hallo." })
  stream = $true
} | ConvertTo-Json -Depth 6

curl.exe -N http://localhost:5155/v1/chat/completions `
  -H "Content-Type: application/json" -d $body
```
Beispielausgabe (SSE, gekuerzt):
```text
data: {"id":"chatcmpl-...","object":"chat.completion.chunk",...}
data: [DONE]
```

### Responses (sync)
```powershell
$body = @{
  model = "google-claude-4.5-sonnet"
  input = "Sag kurz Hallo."
} | ConvertTo-Json -Depth 6

Invoke-RestMethod http://localhost:5155/v1/responses `
  -Method Post -ContentType "application/json" -Body $body
```
Antwort anzeigen:
```powershell
$response = Invoke-RestMethod http://localhost:5155/v1/responses `
  -Method Post -ContentType "application/json" -Body $body

$response | ConvertTo-Json -Depth 6
$response.output_text
```
Beispielausgabe:
```json
{
  "id": "resp_0123456789abcdef",
  "object": "response",
  "created_at": 1769121744,
  "model": "google-claude-4.5-sonnet",
  "status": "completed",
  "output": [
    {
      "id": "msg_0123456789abcdef",
      "type": "message",
      "role": "assistant",
      "status": "completed",
      "content": [
        {
          "type": "output_text",
          "text": "Hallo."
        }
      ]
    }
  ],
  "output_text": "Hallo."
}
```

### Audio Transcription
```powershell
curl.exe -X POST http://localhost:5155/v1/audio/transcriptions `
  -F "model=whisper-1" -F "file=@C:\path\audio.mp3"
```
Beispielausgabe:
```json
{
  "text": "Transkribierter Text..."
}
```

### Image Generation
```powershell
$body = @{
  model = "gpt-image-1"
  prompt = "A colorful abstract landscape"
} | ConvertTo-Json -Depth 6

Invoke-RestMethod http://localhost:5155/v1/images/generations `
  -Method Post -ContentType "application/json" -Body $body
```
Beispielausgabe:
```json
{
  "created": 1769121744,
  "data": [
    {
      "url": "https://example.com/generated-image.png"
    }
  ]
}
```

## Hinweise
- Wenn `model` fehlt, wird `Nele:DefaultChatModel` genutzt.
- `Nele:ForceStream=true` erzwingt Streaming-Antworten fuer `/v1/chat/completions` (SSE), auch wenn der Client kein `stream: true` sendet.
- Die API liefert keine offiziellen Kontext-Fenster-Groessen pro Modell.
