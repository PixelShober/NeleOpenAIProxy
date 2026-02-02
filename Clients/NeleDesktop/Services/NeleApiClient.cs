using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using NeleDesktop.Models;

namespace NeleDesktop.Services;

public sealed class NeleApiClient
{
    private readonly HttpClient _httpClient;

    public NeleApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }

    public async Task<IReadOnlyList<string>> GetModelsAsync(string apiKey, string baseUrl, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(baseUrl, "models"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedAccessException("API key unauthorized.");
            }

            throw new InvalidOperationException($"Model request failed ({(int)response.StatusCode}). {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddModels(document.RootElement, "models", models);
        AddModels(document.RootElement, "team_models", models);

        return models.OrderBy(model => model, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetVerifiedModelsAsync(string apiKey, string baseUrl, CancellationToken cancellationToken)
    {
        var models = await GetModelsAsync(apiKey, baseUrl, cancellationToken);
        var verified = new List<string>();

        foreach (var model in models)
        {
            if (await IsModelUsableAsync(apiKey, baseUrl, model, cancellationToken))
            {
                verified.Add(model);
            }
        }

        return verified;
    }

    public async Task<bool> IsModelUsableAsync(string apiKey, string baseUrl, string model, CancellationToken cancellationToken)
    {
        try
        {
            var messages = new[]
            {
                new ChatMessage
                {
                    Role = "user",
                    Content = "ping"
                }
            };

            _ = await SendChatAsync(apiKey, baseUrl, model, messages, cancellationToken, maxTokens: 1, temperature: 0);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> SendChatAsync(
        string apiKey,
        string baseUrl,
        string model,
        IReadOnlyCollection<ChatMessage> messages,
        CancellationToken cancellationToken,
        int? maxTokens = null,
        double? temperature = null,
        WebSearchOptions? webSearch = null,
        string? reasoningEffort = null)
    {
        var payload = new JsonObject
        {
            ["model"] = model,
            ["messages"] = BuildMessageArray(messages)
        };

        if (maxTokens is not null)
        {
            payload["max_tokens"] = maxTokens.Value;
        }

        if (temperature is not null)
        {
            payload["temperature"] = temperature.Value;
        }

        if (webSearch is not null && webSearch.Enabled)
        {
            var webSearchPayload = new JsonObject
            {
                ["enabled"] = true
            };

            if (!string.IsNullOrWhiteSpace(webSearch.Language))
            {
                webSearchPayload["language"] = webSearch.Language;
            }

            if (!string.IsNullOrWhiteSpace(webSearch.Country))
            {
                webSearchPayload["country"] = webSearch.Country;
            }

            if (webSearch.Results > 0)
            {
                webSearchPayload["results"] = webSearch.Results;
            }

            webSearchPayload["queries"] = new JsonObject
            {
                ["min"] = webSearch.QueriesMin,
                ["max"] = webSearch.QueriesMax
            };

            payload["web_search"] = webSearchPayload;
        }

        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            payload["modelConfiguration"] = new JsonObject
            {
                ["reasoning_effort"] = reasoningEffort
            };
        }

        var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, "chat-completion-sync"))
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Chat request failed ({(int)response.StatusCode}). {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.TryGetProperty("content", out var content)
            ? content.GetString() ?? string.Empty
            : string.Empty;
    }

    public async Task<string> UploadImageAttachmentAsync(string apiKey, string baseUrl, string filePath, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", Path.GetFileName(filePath));

        var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, "image-attachment"))
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Image upload failed ({(int)response.StatusCode}). {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.TryGetProperty("path", out var pathValue))
        {
            return pathValue.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("Image upload response missing path.");
    }

    public async Task<string> TranscribeAudioAsync(
        string apiKey,
        string baseUrl,
        string model,
        byte[] audioBytes,
        string fileName,
        string? language,
        CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(model), "model");
        if (!string.IsNullOrWhiteSpace(language))
        {
            content.Add(new StringContent(language), "language");
        }

        var fileContent = new ByteArrayContent(audioBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, BuildUri(baseUrl, "transcription"))
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Transcription failed ({(int)response.StatusCode}). {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.TryGetProperty("text", out var textValue)
            ? textValue.GetString() ?? string.Empty
            : string.Empty;
    }

    private static Uri BuildUri(string baseUrl, string relative)
    {
        var trimmed = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.aieva.io/api:v1/" : baseUrl.Trim();
        if (!trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed += "/";
        }

        return new Uri(new Uri(trimmed), relative);
    }

    private static void AddModels(JsonElement root, string propertyName, HashSet<string> target)
    {
        if (!root.TryGetProperty(propertyName, out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var model in models.EnumerateArray())
        {
            if (!IsModelUsableFlag(model))
            {
                continue;
            }

            if (model.TryGetProperty("id", out var idValue))
            {
                var id = idValue.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    target.Add(id);
                }
            }
        }
    }

    private static bool IsModelUsableFlag(JsonElement model)
    {
        if (TryGetBoolean(model, "is_usable", out var isUsable) && !isUsable)
        {
            return false;
        }

        if (TryGetBoolean(model, "isUsable", out var isUsableAlt) && !isUsableAlt)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetBoolean(JsonElement model, string propertyName, out bool value)
    {
        if (model.TryGetProperty(propertyName, out var property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            value = property.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static JsonArray BuildMessageArray(IReadOnlyCollection<ChatMessage> messages)
    {
        var array = new JsonArray();
        foreach (var message in messages)
        {
            var messageObject = new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            };

            if (message.Attachments is { Count: > 0 })
            {
                var attachments = new JsonArray();
                foreach (var attachment in message.Attachments)
                {
                    var attachmentObject = new JsonObject
                    {
                        ["type"] = attachment.Type,
                        ["id"] = attachment.Id,
                        ["name"] = attachment.FileName,
                        ["content"] = attachment.Content
                    };

                    if (!string.IsNullOrWhiteSpace(attachment.Detail))
                    {
                        attachmentObject["detail"] = attachment.Detail;
                    }

                    attachments.Add(attachmentObject);
                }

                messageObject["attachments"] = attachments;
            }

            array.Add(messageObject);
        }

        return array;
    }

}
