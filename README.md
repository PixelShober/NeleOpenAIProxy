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

## Konfiguration
Lege `appsettings.Local.json` im gleichen Ordner wie die EXE ab (oder nutze `appsettings.json`).

Beispiel:
```json
{
  "Urls": "http://localhost:5155",
  "Nele": {
    "BaseUrl": "https://api.aieva.io/api:v1/",
    "ApiKey": "YOUR_NELE_API_KEY",
    "DefaultChatModel": "google-claude-4.5-sonnet"
  }
}
```

## Start
```powershell
cd .\bin\Release\net9.0\win-x64\publish
.\NeleOpenAIProxy.exe
```

## Beispiele

### Models
```powershell
Invoke-RestMethod http://localhost:5155/v1/models
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

### Responses (sync)
```powershell
$body = @{
  model = "google-claude-4.5-sonnet"
  input = "Sag kurz Hallo."
} | ConvertTo-Json -Depth 6

Invoke-RestMethod http://localhost:5155/v1/responses `
  -Method Post -ContentType "application/json" -Body $body
```

### Audio Transcription
```powershell
curl.exe -X POST http://localhost:5155/v1/audio/transcriptions `
  -F "model=whisper-1" -F "file=@C:\path\audio.mp3"
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

## Hinweise
- Wenn `model` fehlt, wird `Nele:DefaultChatModel` genutzt.
- Die API liefert keine offiziellen Kontext-Fenster-Groessen pro Modell.
