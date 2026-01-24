using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(2)
    };

    public async Task<IReadOnlyList<string>> GetModelsAsync(string apiKey, string baseUrl, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(baseUrl, "models"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Model request failed ({(int)response.StatusCode}). {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddModels(document.RootElement, "models", models);
        AddModels(document.RootElement, "team_models", models);
        AddModels(document.RootElement, "image_generators", models);

        return models.OrderBy(model => model, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<string> SendChatAsync(
        string apiKey,
        string baseUrl,
        string model,
        IReadOnlyCollection<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["model"] = model,
            ["messages"] = BuildMessageArray(messages)
        };

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

    private static JsonArray BuildMessageArray(IReadOnlyCollection<ChatMessage> messages)
    {
        var array = new JsonArray();
        foreach (var message in messages)
        {
            array.Add(new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            });
        }

        return array;
    }
}
