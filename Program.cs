using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

var appBasePath = AppContext.BaseDirectory;
builder.Configuration.AddJsonFile(Path.Combine(appBasePath, "appsettings.json"), optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile(Path.Combine(appBasePath, "appsettings.Local.json"), optional: true, reloadOnChange: true);

var urls = builder.Configuration["Urls"];
if (!string.IsNullOrWhiteSpace(urls))
{
    builder.WebHost.UseUrls(urls);
}

builder.Services.AddHttpClient("Nele", client =>
{
    var baseUrl = builder.Configuration["Nele:BaseUrl"];
    baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.aieva.io/api:v1/" : baseUrl.Trim();
    if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
    {
        baseUrl += "/";
    }
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

var app = builder.Build();

app.MapGet("/", () => Results.Text("Nele OpenAI Proxy running."));

app.MapGet("/v1/models", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "models");
    if (!TryApplyAuthorization(requestMessage, context.Request, config))
    {
        await WriteMissingAuth(context);
        return;
    }

    if (context.Request.Headers.TryGetValue("Accept-Language", out var language))
    {
        requestMessage.Headers.TryAddWithoutValidation("Accept-Language", language.ToString());
    }

    var client = httpClientFactory.CreateClient("Nele");
    using var response = await client.SendAsync(requestMessage, context.RequestAborted);
    var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
    if (!response.IsSuccessStatusCode)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        await context.Response.WriteAsync(payload, context.RequestAborted);
        return;
    }

    using var doc = JsonDocument.Parse(payload);
    var data = new JsonArray();
    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    AddModels(doc.RootElement, "models", data, created);
    AddModels(doc.RootElement, "team_models", data, created);
    AddModels(doc.RootElement, "image_generators", data, created);

    var result = new JsonObject
    {
        ["object"] = "list",
        ["data"] = data
    };

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(result.ToJsonString(), context.RequestAborted);
});

app.MapGet("/v1/models/{id}", async (HttpContext context, string id, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    using var requestMessage = new HttpRequestMessage(HttpMethod.Get, "models");
    if (!TryApplyAuthorization(requestMessage, context.Request, config))
    {
        await WriteMissingAuth(context);
        return;
    }

    var client = httpClientFactory.CreateClient("Nele");
    using var response = await client.SendAsync(requestMessage, context.RequestAborted);
    var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
    if (!response.IsSuccessStatusCode)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        await context.Response.WriteAsync(payload, context.RequestAborted);
        return;
    }

    using var doc = JsonDocument.Parse(payload);
    if (TryFindModel(doc.RootElement, id, out var model))
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(model.ToJsonString(), context.RequestAborted);
        return;
    }

    await WriteOpenAiError(context, StatusCodes.Status404NotFound, "Model not found.", "invalid_request_error", "model_not_found");
});

app.MapPost("/v1/chat/completions", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    JsonDocument requestDoc;
    try
    {
        requestDoc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
    }
    catch (JsonException)
    {
        await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Invalid JSON body.", "invalid_request_error", "invalid_json");
        return;
    }

    using (requestDoc)
    {
        var root = requestDoc.RootElement;
        var isStream = root.TryGetProperty("stream", out var streamValue) && streamValue.ValueKind == JsonValueKind.True;
        var defaultModel = GetDefaultChatModel(config);
        var model = ResolveChatModel(root, defaultModel);
        var nelePayload = BuildChatCompletionPayload(root, model);
        var json = nelePayload.ToJsonString();

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, isStream ? "chat-completion" : "chat-completion-sync")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!TryApplyAuthorization(requestMessage, context.Request, config))
        {
            await WriteMissingAuth(context);
            return;
        }

        var client = httpClientFactory.CreateClient("Nele");
        using var response = await client.SendAsync(
            requestMessage,
            isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
            context.RequestAborted);

        if (isStream)
        {
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "text/event-stream";
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(context.RequestAborted);
                await context.Response.WriteAsync(errorBody, context.RequestAborted);
                return;
            }

            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            return;
        }

        var body = await response.Content.ReadAsStringAsync(context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            await context.Response.WriteAsync(body, context.RequestAborted);
            return;
        }

        var openAiResponse = BuildChatCompletionResponse(model, body);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(openAiResponse.ToJsonString(), context.RequestAborted);
    }
});

app.MapPost("/v1/responses", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    JsonDocument requestDoc;
    try
    {
        requestDoc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
    }
    catch (JsonException)
    {
        await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Invalid JSON body.", "invalid_request_error", "invalid_json");
        return;
    }

    using (requestDoc)
    {
        var root = requestDoc.RootElement;
        var isStream = root.TryGetProperty("stream", out var streamValue) && streamValue.ValueKind == JsonValueKind.True;
        if (isStream)
        {
            await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Streaming is not supported for /v1/responses.", "invalid_request_error", "streaming_not_supported");
            return;
        }

        if (!TryBuildResponsesMessages(root, out var messages, out var errorMessage))
        {
            await WriteOpenAiError(context, StatusCodes.Status400BadRequest, errorMessage, "invalid_request_error", "invalid_input");
            return;
        }

        var nelePayload = new JsonObject();
        var defaultModel = GetDefaultChatModel(config);
        var model = ResolveChatModel(root, defaultModel);
        nelePayload["model"] = model;
        CopyProperty(root, nelePayload, "temperature");
        CopyProperty(root, nelePayload, "max_tokens");
        if (root.TryGetProperty("max_output_tokens", out var maxOutputTokens))
        {
            nelePayload["max_tokens"] = JsonNode.Parse(maxOutputTokens.GetRawText());
        }

        nelePayload["messages"] = messages;

        var json = nelePayload.ToJsonString();
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat-completion-sync")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!TryApplyAuthorization(requestMessage, context.Request, config))
        {
            await WriteMissingAuth(context);
            return;
        }

        var client = httpClientFactory.CreateClient("Nele");
        using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, context.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            await context.Response.WriteAsync(body, context.RequestAborted);
            return;
        }

        var openAiResponse = BuildResponsesResponse(model, body);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(openAiResponse.ToJsonString(), context.RequestAborted);
    }
});

app.MapPost("/v1/audio/transcriptions", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    if (!context.Request.HasFormContentType)
    {
        await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Expected multipart form data.", "invalid_request_error", "invalid_content_type");
        return;
    }

    var form = await context.Request.ReadFormAsync(context.RequestAborted);
    var file = form.Files.GetFile("file");
    if (file is null)
    {
        await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Missing form field: file.", "invalid_request_error", "missing_file");
        return;
    }

    var model = MapTranscriptionModel(form["model"].ToString());
    var language = form["language"].ToString();

    using var content = new MultipartFormDataContent();
    content.Add(new StringContent(model), "model");
    if (!string.IsNullOrWhiteSpace(language))
    {
        content.Add(new StringContent(language), "language");
    }

    var fileContent = new StreamContent(file.OpenReadStream());
    if (!string.IsNullOrWhiteSpace(file.ContentType))
    {
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
    }

    content.Add(fileContent, "file", file.FileName);

    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "transcription")
    {
        Content = content
    };

    if (!TryApplyAuthorization(requestMessage, context.Request, config))
    {
        await WriteMissingAuth(context);
        return;
    }

    var client = httpClientFactory.CreateClient("Nele");
    using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, context.RequestAborted);
    var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
    if (!response.IsSuccessStatusCode)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        await context.Response.WriteAsync(payload, context.RequestAborted);
        return;
    }

    using var doc = JsonDocument.Parse(payload);
    var text = doc.RootElement.TryGetProperty("text", out var textValue) ? textValue.GetString() ?? string.Empty : string.Empty;
    var openAiResponse = new JsonObject
    {
        ["text"] = text
    };

    context.Response.ContentType = "application/json";
    await context.Response.WriteAsync(openAiResponse.ToJsonString(), context.RequestAborted);
});

app.MapPost("/v1/images/generations", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config) =>
{
    JsonDocument requestDoc;
    try
    {
        requestDoc = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
    }
    catch (JsonException)
    {
        await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Invalid JSON body.", "invalid_request_error", "invalid_json");
        return;
    }

    using (requestDoc)
    {
        var root = requestDoc.RootElement;
        var prompt = GetStringProperty(root, "prompt");
        var model = GetStringProperty(root, "model");
        if (string.IsNullOrWhiteSpace(prompt) || string.IsNullOrWhiteSpace(model))
        {
            await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Fields 'prompt' and 'model' are required.", "invalid_request_error", "missing_fields");
            return;
        }

        var responseFormat = GetStringProperty(root, "response_format");
        if (!string.IsNullOrWhiteSpace(responseFormat) && !responseFormat.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Only response_format=url is supported.", "invalid_request_error", "unsupported_response_format");
            return;
        }

        var n = root.TryGetProperty("n", out var nValue) && nValue.ValueKind == JsonValueKind.Number ? nValue.GetInt32() : 1;
        if (n > 1)
        {
            await WriteOpenAiError(context, StatusCodes.Status400BadRequest, "Only n=1 is supported.", "invalid_request_error", "unsupported_n");
            return;
        }

        var quality = GetStringProperty(root, "quality");
        var size = GetStringProperty(root, "size");
        var style = GetStringProperty(root, "style");
        var background = GetStringProperty(root, "background");

        var modelConfiguration = new JsonObject
        {
            ["quality"] = string.IsNullOrWhiteSpace(quality) ? GetDefaultImageQuality(model) : quality,
            ["size"] = string.IsNullOrWhiteSpace(size) ? GetDefaultImageSize(model) : size
        };

        if (!string.IsNullOrWhiteSpace(style))
        {
            modelConfiguration["style"] = style;
        }

        if (!string.IsNullOrWhiteSpace(background))
        {
            modelConfiguration["background"] = background;
        }

        var nelePayload = new JsonObject
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["modelConfiguration"] = modelConfiguration
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "image")
        {
            Content = new StringContent(nelePayload.ToJsonString(), Encoding.UTF8, "application/json")
        };

        if (!TryApplyAuthorization(requestMessage, context.Request, config))
        {
            await WriteMissingAuth(context);
            return;
        }

        var client = httpClientFactory.CreateClient("Nele");
        using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, context.RequestAborted);
        var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            await context.Response.WriteAsync(payload, context.RequestAborted);
            return;
        }

        using var doc = JsonDocument.Parse(payload);
        var url = doc.RootElement.TryGetProperty("url", out var urlValue) ? urlValue.GetString() ?? string.Empty : string.Empty;
        var revisedPrompt = doc.RootElement.TryGetProperty("revisedPrompt", out var revisedValue) ? revisedValue.GetString() : null;

        var dataItem = new JsonObject
        {
            ["url"] = url
        };

        if (!string.IsNullOrWhiteSpace(revisedPrompt))
        {
            dataItem["revised_prompt"] = revisedPrompt;
        }

        var openAiResponse = new JsonObject
        {
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["data"] = new JsonArray(dataItem)
        };

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(openAiResponse.ToJsonString(), context.RequestAborted);
    }
});

app.Run();

static bool TryApplyAuthorization(HttpRequestMessage requestMessage, HttpRequest request, IConfiguration config)
{
    var token = ExtractBearerToken(request, config);
    if (string.IsNullOrWhiteSpace(token))
    {
        return false;
    }

    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return true;
}

static string? ExtractBearerToken(HttpRequest request, IConfiguration config)
{
    if (request.Headers.TryGetValue("Authorization", out var authHeader))
    {
        var value = authHeader.ToString();
        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return value.Substring("Bearer ".Length).Trim();
        }
    }

    if (request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
    {
        var apiKey = apiKeyHeader.ToString();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey.Trim();
        }
    }

    var configKey = config["Nele:ApiKey"];
    if (!string.IsNullOrWhiteSpace(configKey))
    {
        return configKey.Trim();
    }

    var envKey = Environment.GetEnvironmentVariable("NELE_API_KEY");
    return string.IsNullOrWhiteSpace(envKey) ? null : envKey.Trim();
}

static async Task WriteMissingAuth(HttpContext context)
{
    await WriteOpenAiError(context, StatusCodes.Status401Unauthorized, "Missing API key. Use Authorization: Bearer <key> or set NELE_API_KEY.", "invalid_request_error", "missing_api_key");
}

static async Task WriteOpenAiError(HttpContext context, int statusCode, string message, string type, string code)
{
    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/json";
    var payload = new JsonObject
    {
        ["error"] = new JsonObject
        {
            ["message"] = message,
            ["type"] = type,
            ["code"] = code
        }
    };
    await context.Response.WriteAsync(payload.ToJsonString(), context.RequestAborted);
}

static void AddModels(JsonElement root, string propertyName, JsonArray data, long created)
{
    if (!root.TryGetProperty(propertyName, out var models) || models.ValueKind != JsonValueKind.Array)
    {
        return;
    }

    foreach (var model in models.EnumerateArray())
    {
        if (!model.TryGetProperty("id", out var idValue))
        {
            continue;
        }

        var id = idValue.GetString();
        if (string.IsNullOrWhiteSpace(id))
        {
            continue;
        }

        data.Add(new JsonObject
        {
            ["id"] = id,
            ["object"] = "model",
            ["created"] = created,
            ["owned_by"] = "nele"
        });
    }
}

static bool TryFindModel(JsonElement root, string id, out JsonObject model)
{
    model = new JsonObject();
    var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    foreach (var propertyName in new[] { "models", "team_models", "image_generators" })
    {
        if (!root.TryGetProperty(propertyName, out var models) || models.ValueKind != JsonValueKind.Array)
        {
            continue;
        }

        foreach (var item in models.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idValue))
            {
                continue;
            }

            if (!string.Equals(idValue.GetString(), id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            model = new JsonObject
            {
                ["id"] = id,
                ["object"] = "model",
                ["created"] = created,
                ["owned_by"] = "nele"
            };
            return true;
        }
    }

    return false;
}

static JsonObject BuildChatCompletionPayload(JsonElement root, string model)
{
    var payload = new JsonObject();

    payload["model"] = model;
    CopyProperty(root, payload, "max_tokens");
    CopyProperty(root, payload, "temperature");
    CopyProperty(root, payload, "documentCollectionId");
    CopyProperty(root, payload, "web_search");
    CopyProperty(root, payload, "tool_choice");
    CopyProperty(root, payload, "tools");

    if (root.TryGetProperty("messages", out var messagesValue))
    {
        payload["messages"] = NormalizeMessages(messagesValue);
    }

    return payload;
}

static JsonArray NormalizeMessages(JsonElement messagesValue)
{
    var messages = new JsonArray();
    if (messagesValue.ValueKind != JsonValueKind.Array)
    {
        return messages;
    }

    foreach (var message in messagesValue.EnumerateArray())
    {
        if (!TryNormalizeMessage(message, out var normalized))
        {
            continue;
        }

        messages.Add(normalized);
    }

    return messages;
}

static bool TryNormalizeMessage(JsonElement message, out JsonObject normalized)
{
    normalized = new JsonObject();
    if (message.ValueKind != JsonValueKind.Object)
    {
        return false;
    }

    CopyProperty(message, normalized, "role");
    CopyProperty(message, normalized, "name");

    if (message.TryGetProperty("content", out var contentValue))
    {
        if (contentValue.ValueKind == JsonValueKind.Array)
        {
            normalized["content"] = ExtractTextContent(contentValue);
        }
        else
        {
            normalized["content"] = JsonNode.Parse(contentValue.GetRawText());
        }
    }

    CopyProperty(message, normalized, "attachments");
    CopyProperty(message, normalized, "results");

    return true;
}

static string ExtractTextContent(JsonElement contentValue)
{
    var builder = new StringBuilder();
    foreach (var part in contentValue.EnumerateArray())
    {
        if (part.ValueKind != JsonValueKind.Object)
        {
            continue;
        }

        if (!part.TryGetProperty("type", out var typeValue))
        {
            continue;
        }

        var type = typeValue.GetString();
        if (!string.Equals(type, "text", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(type, "input_text", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (part.TryGetProperty("text", out var textValue))
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(textValue.GetString());
        }
    }

    return builder.ToString();
}

static void CopyProperty(JsonElement source, JsonObject target, string propertyName)
{
    if (!source.TryGetProperty(propertyName, out var value))
    {
        return;
    }

    target[propertyName] = JsonNode.Parse(value.GetRawText());
}

static string GetStringProperty(JsonElement source, string propertyName)
{
    if (!source.TryGetProperty(propertyName, out var value))
    {
        return string.Empty;
    }

    if (value.ValueKind == JsonValueKind.Null)
    {
        return string.Empty;
    }

    return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
}

static string GetDefaultChatModel(IConfiguration config)
{
    var model = config["Nele:DefaultChatModel"];
    return string.IsNullOrWhiteSpace(model) ? "google-claude-4.5-sonnet" : model.Trim();
}

static string ResolveChatModel(JsonElement root, string defaultModel)
{
    var model = GetStringProperty(root, "model");
    return string.IsNullOrWhiteSpace(model) ? defaultModel : model;
}

static bool TryBuildResponsesMessages(JsonElement root, out JsonArray messages, out string errorMessage)
{
    messages = new JsonArray();
    errorMessage = string.Empty;

    if (root.TryGetProperty("messages", out var messagesValue))
    {
        if (messagesValue.ValueKind != JsonValueKind.Array)
        {
            errorMessage = "messages must be an array.";
            return false;
        }

        foreach (var message in messagesValue.EnumerateArray())
        {
            if (!TryNormalizeMessage(message, out var normalized))
            {
                continue;
            }

            messages.Add(normalized);
        }

        return messages.Count > 0;
    }

    if (!root.TryGetProperty("input", out var inputValue))
    {
        errorMessage = "Either input or messages is required.";
        return false;
    }

    if (inputValue.ValueKind == JsonValueKind.String)
    {
        messages.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = inputValue.GetString() ?? string.Empty
        });
        return true;
    }

    if (inputValue.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in inputValue.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                messages.Add(new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = item.GetString() ?? string.Empty
                });
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("messages", out var nestedMessages) && nestedMessages.ValueKind == JsonValueKind.Array)
            {
                foreach (var nested in nestedMessages.EnumerateArray())
                {
                    if (TryNormalizeMessage(nested, out var normalized))
                    {
                        messages.Add(normalized);
                    }
                }

                continue;
            }

            if (TryNormalizeMessage(item, out var normalizedItem))
            {
                messages.Add(normalizedItem);
            }
        }

        return messages.Count > 0;
    }

    errorMessage = "Unsupported input format.";
    return false;
}

static JsonObject BuildResponsesResponse(string model, string payload)
{
    using var doc = JsonDocument.Parse(payload);
    var root = doc.RootElement;
    var text = root.TryGetProperty("content", out var contentValue) && contentValue.ValueKind != JsonValueKind.Null
        ? contentValue.GetString() ?? string.Empty
        : string.Empty;
    var outputText = new JsonObject
    {
        ["type"] = "output_text",
        ["text"] = text
    };

    var outputMessage = new JsonObject
    {
        ["id"] = $"msg_{Guid.NewGuid():N}",
        ["type"] = "message",
        ["role"] = "assistant",
        ["status"] = "completed",
        ["content"] = new JsonArray(outputText)
    };

    var responseId = $"resp_{Guid.NewGuid():N}";

    return new JsonObject
    {
        ["id"] = responseId,
        ["object"] = "response",
        ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ["model"] = model,
        ["status"] = "completed",
        ["output"] = new JsonArray(outputMessage),
        ["output_text"] = text
    };
}

static JsonObject BuildChatCompletionResponse(string model, string payload)
{
    using var doc = JsonDocument.Parse(payload);
    var root = doc.RootElement;

    var message = new JsonObject
    {
        ["role"] = "assistant",
        ["content"] = root.TryGetProperty("content", out var contentValue) && contentValue.ValueKind != JsonValueKind.Null
            ? contentValue.GetString()
            : null
    };

    if (root.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
    {
        message["tool_calls"] = JsonNode.Parse(toolCalls.GetRawText());
    }

    var finishReason = message["tool_calls"] is null ? "stop" : "tool_calls";

    return new JsonObject
    {
        ["id"] = $"chatcmpl-{Guid.NewGuid():N}",
        ["object"] = "chat.completion",
        ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ["model"] = model,
        ["choices"] = new JsonArray
        {
            new JsonObject
            {
                ["index"] = 0,
                ["message"] = message,
                ["finish_reason"] = finishReason
            }
        },
        ["usage"] = new JsonObject
        {
            ["prompt_tokens"] = 0,
            ["completion_tokens"] = 0,
            ["total_tokens"] = 0
        }
    };
}

static string MapTranscriptionModel(string model)
{
    if (string.IsNullOrWhiteSpace(model))
    {
        return "azure-whisper";
    }

    return model switch
    {
        "whisper-1" => "azure-whisper",
        _ => model
    };
}

static string GetDefaultImageQuality(string model)
{
    return model == "gpt-image-1" ? "auto" : "standard";
}

static string GetDefaultImageSize(string model)
{
    return model == "gpt-image-1" ? "auto" : "1024x1024";
}
