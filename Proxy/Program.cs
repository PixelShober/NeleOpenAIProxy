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

app.MapGet("/", (IConfiguration config) =>
{
    var key = GetConfiguredApiKey(config);
    return Results.Text(string.IsNullOrWhiteSpace(key) ? "API not provided" : "Nele OpenAI Proxy running.");
});

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

var knowledge = app.MapGroup("/v1/knowledge");
knowledge.MapGet("/models", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Get, "models", logger);
});
knowledge.MapGet("/collections", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Get, "document-collections", logger);
});
knowledge.MapPost("/collections", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Post, "document-collections", logger);
});
knowledge.MapGet("/collections/{collection}", async (HttpContext context, string collection, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Get, $"document-collections/{collection}", logger);
});
knowledge.MapPut("/collections/{collection}", async (HttpContext context, string collection, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Put, $"document-collections/{collection}", logger);
});
knowledge.MapDelete("/collections/{collection}", async (HttpContext context, string collection, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Delete, $"document-collections/{collection}", logger);
});
knowledge.MapPost("/collections/{collection}/items", async (HttpContext context, string collection, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Post, $"document-collections/{collection}/items", logger);
});
knowledge.MapPost("/collections/{collection}/from-url", async (HttpContext context, string collection, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Post, $"document-collections/{collection}/from-url", logger);
});
knowledge.MapPut("/collections/{collection}/embed", async (HttpContext context, string collection, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Put, $"document-collections/{collection}/embed", logger);
});
knowledge.MapPost("/collections/{collection}/search", async (HttpContext context, string collection, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Post, $"document-collections/{collection}/search", logger);
});
knowledge.MapGet("/items/{item}", async (HttpContext context, string item, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Get, $"document-collection-items/{item}", logger);
});
knowledge.MapDelete("/items/{item}", async (HttpContext context, string item, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Delete, $"document-collection-items/{item}", logger);
});
knowledge.MapPut("/items/{item}/embed", async (HttpContext context, string item, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
{
    await ProxyRequestAsync(context, httpClientFactory, config, HttpMethod.Put, $"document-collection-items/{item}/embed", logger);
});

app.MapPost("/v1/chat/completions", async (HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<Program> logger) =>
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
        var roleSummary = GetMessageRoleSummary(root);
        if (!string.IsNullOrWhiteSpace(roleSummary))
        {
            logger.LogInformation("Incoming chat roles: {Roles}", roleSummary);
        }
        var streamRequested = root.TryGetProperty("stream", out var streamValue) && streamValue.ValueKind == JsonValueKind.True;
        var forceStream = IsStreamingForced(config);
        var isStream = streamRequested || forceStream;
        var defaultModel = GetDefaultChatModel(config);
        var model = ResolveChatModel(root, defaultModel);
        var (messageCount, imagePartCount, toolCount, hasWebSearch, hasDocumentCollection) = GetMessageStatistics(root);
        logger.LogInformation(
            "Chat completion request received. Model={Model} StreamRequested={StreamRequested} ForceStream={ForceStream} Messages={MessageCount} ImageParts={ImagePartCount} Tools={ToolCount} WebSearch={HasWebSearch} DocumentCollection={HasDocumentCollection}",
            model,
            streamRequested,
            forceStream,
            messageCount,
            imagePartCount,
            toolCount,
            hasWebSearch,
            hasDocumentCollection);

        var nelePayload = await BuildChatCompletionPayloadAsync(root, model, context, httpClientFactory, config, logger);
        if (nelePayload is null)
        {
            return;
        }

        var json = nelePayload.ToJsonString();
        if (nelePayload.TryGetPropertyValue("modelConfiguration", out var modelConfiguration) && modelConfiguration is JsonObject modelConfig
            && modelConfig.TryGetPropertyValue("reasoning_effort", out var reasoningEffort))
        {
            logger.LogInformation("Using reasoning_effort={ReasoningEffort}", reasoningEffort?.ToString());
        }

        if (isStream)
        {
            var includeUsage = streamRequested && TryGetStreamIncludeUsage(root);
            using var upstreamReq = new HttpRequestMessage(HttpMethod.Post, "chat-completion-sync")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (!TryApplyAuthorization(upstreamReq, context.Request, config))
            {
                await WriteMissingAuth(context);
                return;
            }

            var upstreamClient = httpClientFactory.CreateClient("Nele");
            using var upstreamResp = await upstreamClient.SendAsync(upstreamReq, HttpCompletionOption.ResponseContentRead, context.RequestAborted);
            var upstreamBody = await upstreamResp.Content.ReadAsStringAsync(context.RequestAborted);

            if (!upstreamResp.IsSuccessStatusCode)
            {
                logger.LogWarning("Upstream chat completion failed. Status={StatusCode} Reason={ReasonPhrase}", (int)upstreamResp.StatusCode, upstreamResp.ReasonPhrase);
                // Optional: wenn upstream leer ist, gib wenigstens OpenAI-Error zurück (hilft bei "no body")
                if (string.IsNullOrWhiteSpace(upstreamBody))
                    await WriteOpenAiError(context, (int)upstreamResp.StatusCode,
                        $"Upstream returned {(int)upstreamResp.StatusCode} {upstreamResp.ReasonPhrase}.",
                        "upstream_error", "upstream_no_body");
                else
                {
                    context.Response.StatusCode = (int)upstreamResp.StatusCode;
                    context.Response.ContentType = upstreamResp.Content.Headers.ContentType?.ToString() ?? "application/json";
                    await context.Response.WriteAsync(upstreamBody, context.RequestAborted);
                }
                return;
            }

            // 2) Upstream payload -> OpenAI ChatCompletion (du hast das schon)
            var openAi = BuildChatCompletionResponse(model, upstreamBody);

            await WriteChatCompletionStream(context, openAi, includeUsage);
            return;
        }

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
        using var response = await client.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseContentRead,
            context.RequestAborted);


        var body = await response.Content.ReadAsStringAsync(context.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Upstream chat completion failed. Status={StatusCode} Reason={ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);
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

    return GetConfiguredApiKey(config);
}

static string? GetConfiguredApiKey(IConfiguration config)
{
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

static async Task ProxyRequestAsync(HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, HttpMethod method, string upstreamPath, ILogger logger)
{
    var upstreamPathWithQuery = upstreamPath + context.Request.QueryString.Value;
    using var requestMessage = new HttpRequestMessage(method, upstreamPathWithQuery);
    logger.LogInformation(
        "Knowledge proxy request. Method={Method} Path={Path} ContentType={ContentType} ContentLength={ContentLength}",
        method.Method,
        upstreamPathWithQuery,
        context.Request.ContentType ?? string.Empty,
        context.Request.ContentLength ?? 0);

    if (method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Delete)
    {
        if (context.Request.Body.CanRead)
        {
            var content = new StreamContent(context.Request.Body);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            {
                content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }

            requestMessage.Content = content;
        }
    }

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
    using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, context.RequestAborted);
    var payload = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);

    context.Response.StatusCode = (int)response.StatusCode;
    if (response.Content.Headers.ContentType is not null)
    {
        context.Response.ContentType = response.Content.Headers.ContentType.ToString();
    }

    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Knowledge proxy call failed. Path={Path} Status={StatusCode} Reason={ReasonPhrase}", upstreamPathWithQuery, (int)response.StatusCode, response.ReasonPhrase);
    }
    else
    {
        logger.LogDebug("Knowledge proxy call completed. Path={Path} Status={StatusCode}", upstreamPathWithQuery, (int)response.StatusCode);
    }

    if (payload.Length > 0)
    {
        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }
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

static async Task<JsonObject?> BuildChatCompletionPayloadAsync(JsonElement root, string model, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger logger)
{
    var payload = new JsonObject();

    payload["model"] = model;
    CopyProperty(root, payload, "max_tokens");
    CopyProperty(root, payload, "temperature");
    var requestCollectionId = GetStringProperty(root, "documentCollectionId");
    var defaultCollectionId = GetDefaultDocumentCollectionId(config);
    var hasWebSearch = root.TryGetProperty("web_search", out var webSearchValue) && webSearchValue.ValueKind != JsonValueKind.Null;
    if (!string.IsNullOrWhiteSpace(requestCollectionId))
    {
        payload["documentCollectionId"] = requestCollectionId;
    }
    else if (!string.IsNullOrWhiteSpace(defaultCollectionId) && !hasWebSearch)
    {
        payload["documentCollectionId"] = defaultCollectionId;
        logger.LogInformation("Using default documentCollectionId from config.");
    }
    CopyProperty(root, payload, "web_search");
    CopyProperty(root, payload, "tool_choice");
    var normalizedTools = NormalizeTools(root, config, logger);
    if (normalizedTools is not null)
    {
        payload["tools"] = normalizedTools;
    }

    var modelConfiguration = BuildModelConfiguration(root, config);
    if (modelConfiguration is not null)
    {
        payload["modelConfiguration"] = modelConfiguration;
    }

    if (root.TryGetProperty("messages", out var messagesValue))
    {
        var messages = await NormalizeMessagesAsync(messagesValue, context, httpClientFactory, config, logger);
        if (messages is null)
        {
            return null;
        }

        payload["messages"] = messages;
    }

    return payload;
}

static JsonNode? NormalizeTools(JsonElement root, IConfiguration config, ILogger logger)
{
    if (!root.TryGetProperty("tools", out var toolsValue))
    {
        return null;
    }

    if (toolsValue.ValueKind != JsonValueKind.Array)
    {
        return JsonNode.Parse(toolsValue.GetRawText());
    }

    var maxDescriptionLength = GetToolDescriptionMaxLength(config);
    var trimmedCount = 0;
    var tools = new JsonArray();
    foreach (var tool in toolsValue.EnumerateArray())
    {
        var toolNode = JsonNode.Parse(tool.GetRawText());
        if (toolNode is not JsonObject toolObject)
        {
            if (toolNode is not null)
            {
                tools.Add(toolNode);
            }
            continue;
        }

        if (maxDescriptionLength > 0
            && toolObject["function"] is JsonObject functionObject
            && functionObject["description"] is JsonValue descriptionValue)
        {
            var description = descriptionValue.GetValue<string>();
            if (description.Length > maxDescriptionLength)
            {
                functionObject["description"] = description[..maxDescriptionLength];
                trimmedCount++;
            }
        }

        tools.Add(toolObject);
    }

    if (trimmedCount > 0)
    {
        logger.LogInformation("Trimmed {TrimmedCount} tool description(s) to {MaxLength} characters.", trimmedCount, maxDescriptionLength);
    }

    return tools;
}

static async Task<JsonArray?> NormalizeMessagesAsync(JsonElement messagesValue, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger logger)
{
    var messages = new JsonArray();
    if (messagesValue.ValueKind != JsonValueKind.Array)
    {
        return messages;
    }

    foreach (var message in messagesValue.EnumerateArray())
    {
        var (success, normalized) = await TryNormalizeMessageAsync(message, context, httpClientFactory, config, logger);
        if (!success)
        {
            return null;
        }

        if (normalized is not null)
        {
            messages.Add(normalized);
        }
    }

    return messages;
}

static async Task<(bool Success, JsonObject? Normalized)> TryNormalizeMessageAsync(JsonElement message, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger logger)
{
    if (message.ValueKind != JsonValueKind.Object)
    {
        return (true, null);
    }

    var normalized = new JsonObject();
    var role = NormalizeRole(GetStringProperty(message, "role"), logger);
    if (!string.IsNullOrWhiteSpace(role))
    {
        normalized["role"] = role;
    }
    CopyProperty(message, normalized, "name");

    var attachments = new JsonArray();
    var messageRole = GetStringProperty(message, "role");
    if (message.TryGetProperty("attachments", out var attachmentsValue) && attachmentsValue.ValueKind == JsonValueKind.Array)
    {
        foreach (var attachment in attachmentsValue.EnumerateArray())
        {
            attachments.Add(JsonNode.Parse(attachment.GetRawText()));
        }
    }

    if (message.TryGetProperty("content", out var contentValue))
    {
        if (contentValue.ValueKind == JsonValueKind.Array)
        {
            var contentText = new StringBuilder();
            foreach (var part in contentValue.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var partType = GetStringProperty(part, "type");
                if (string.Equals(partType, "text", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(partType, "input_text", StringComparison.OrdinalIgnoreCase))
                {
                    if (part.TryGetProperty("text", out var textValue))
                    {
                        if (contentText.Length > 0)
                        {
                            contentText.Append('\n');
                        }

                        contentText.Append(textValue.GetString());
                    }

                    continue;
                }

                if (string.Equals(partType, "image_url", StringComparison.OrdinalIgnoreCase))
                {
                    if (!part.TryGetProperty("image_url", out var imageValue) || imageValue.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var url = GetStringProperty(imageValue, "url");
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    var attachment = await TryUploadImageAttachmentAsync(url, imageValue, context, httpClientFactory, config, logger);
                    if (attachment is null)
                    {
                        return (false, null);
                    }

                    attachments.Add(attachment);
                }
            }

            normalized["content"] = contentText.ToString();
        }
        else
        {
            normalized["content"] = JsonNode.Parse(contentValue.GetRawText());
        }
    }

    if (attachments.Count > 0)
    {
        normalized["attachments"] = attachments;
        logger.LogInformation("Added attachments to message. Role={Role} Attachments={AttachmentCount}", messageRole, attachments.Count);
    }

    CopyProperty(message, normalized, "results");

    return (true, normalized);
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

static async Task<JsonObject?> TryUploadImageAttachmentAsync(string url, JsonElement imageValue, HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger logger)
{
    if (!TryGetImageDetail(imageValue, out var detail))
    {
        detail = null;
    }

    var imageContent = await TryGetImageContentAsync(url, context.RequestAborted);
    if (!imageContent.Success)
    {
        logger.LogWarning("Image attachment download failed. Reason={Reason}", imageContent.ErrorMessage);
        await WriteOpenAiError(context, StatusCodes.Status400BadRequest, imageContent.ErrorMessage, "invalid_request_error", "invalid_image_url");
        return null;
    }

    logger.LogInformation("Uploading image attachment. ContentType={ContentType} Bytes={ByteLength}", imageContent.ContentType, imageContent.Data.Length);

    using var multipart = new MultipartFormDataContent();
    var fileContent = new ByteArrayContent(imageContent.Data);
    if (!string.IsNullOrWhiteSpace(imageContent.ContentType))
    {
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(imageContent.ContentType);
    }

    multipart.Add(fileContent, "file", imageContent.FileName);

    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "image-attachment")
    {
        Content = multipart
    };

    if (!TryApplyAuthorization(requestMessage, context.Request, config))
    {
        await WriteMissingAuth(context);
        return null;
    }

    var client = httpClientFactory.CreateClient("Nele");
    using var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, context.RequestAborted);
    var payload = await response.Content.ReadAsStringAsync(context.RequestAborted);
    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Image attachment upload failed. Status={StatusCode} Reason={ReasonPhrase}", (int)response.StatusCode, response.ReasonPhrase);
        context.Response.StatusCode = (int)response.StatusCode;
        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        await context.Response.WriteAsync(payload, context.RequestAborted);
        return null;
    }

    using var doc = JsonDocument.Parse(payload);
    if (!doc.RootElement.TryGetProperty("path", out var pathValue))
    {
        logger.LogWarning("Image attachment upload succeeded but no path was returned by upstream.");
        await WriteOpenAiError(context, StatusCodes.Status502BadGateway, "Upstream did not return image path.", "upstream_error", "image_attachment_missing_path");
        return null;
    }

    var path = pathValue.GetString();
    if (string.IsNullOrWhiteSpace(path))
    {
        logger.LogWarning("Image attachment upload returned an empty path.");
        await WriteOpenAiError(context, StatusCodes.Status502BadGateway, "Upstream returned empty image path.", "upstream_error", "image_attachment_empty_path");
        return null;
    }

    var attachment = new JsonObject
    {
        ["type"] = "image",
        ["id"] = path,
        ["name"] = imageContent.FileName,
        ["content"] = path
    };

    if (!string.IsNullOrWhiteSpace(detail))
    {
        attachment["detail"] = detail;
    }

    return attachment;
}

static bool TryGetImageDetail(JsonElement imageValue, out string? detail)
{
    detail = null;
    if (imageValue.TryGetProperty("detail", out var detailValue) && detailValue.ValueKind == JsonValueKind.String)
    {
        detail = detailValue.GetString();
        return true;
    }

    return false;
}

static async Task<(bool Success, byte[] Data, string ContentType, string FileName, string ErrorMessage)> TryGetImageContentAsync(string url, CancellationToken cancellationToken)
{
    var data = Array.Empty<byte>();
    var contentType = string.Empty;
    var fileName = "image";
    var errorMessage = "Invalid image_url.";

    if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryParseDataUrl(url, out data, out contentType))
        {
            errorMessage = "Invalid data URL for image_url.";
            return (false, data, contentType, fileName, errorMessage);
        }

        fileName = EnsureFileNameExtension("image", contentType);
        return (true, data, contentType, fileName, errorMessage);
    }

    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        errorMessage = "image_url must be a data URL or an http(s) URL.";
        return (false, data, contentType, fileName, errorMessage);
    }

    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    using var response = await httpClient.GetAsync(uri, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        errorMessage = $"Failed to download image_url: {(int)response.StatusCode} {response.ReasonPhrase}.";
        return (false, data, contentType, fileName, errorMessage);
    }

    data = await response.Content.ReadAsByteArrayAsync(cancellationToken);
    contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
    fileName = EnsureFileNameExtension(GetFileNameFromUrl(uri), contentType);
    return (true, data, contentType, fileName, errorMessage);
}

static bool TryParseDataUrl(string url, out byte[] data, out string contentType)
{
    data = Array.Empty<byte>();
    contentType = string.Empty;

    var commaIndex = url.IndexOf(',');
    if (commaIndex <= 0)
    {
        return false;
    }

    var header = url.Substring(5, commaIndex - 5);
    var payload = url[(commaIndex + 1)..];
    if (!header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var typeSplit = header.Split(';', StringSplitOptions.RemoveEmptyEntries);
    if (typeSplit.Length > 0)
    {
        contentType = typeSplit[0];
    }

    try
    {
        data = Convert.FromBase64String(payload);
        return true;
    }
    catch (FormatException)
    {
        return false;
    }
}

static string GetFileNameFromUrl(Uri uri)
{
    var name = Path.GetFileName(uri.LocalPath);
    return string.IsNullOrWhiteSpace(name) ? "image" : name;
}

static string EnsureFileNameExtension(string fileName, string contentType)
{
    if (Path.HasExtension(fileName))
    {
        return fileName;
    }

    var extension = GetExtensionForContentType(contentType);
    return string.IsNullOrWhiteSpace(extension) ? fileName : $"{fileName}.{extension}";
}

static string GetExtensionForContentType(string contentType)
{
    if (string.IsNullOrWhiteSpace(contentType))
    {
        return string.Empty;
    }

    return contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => "jpg",
        "image/jpg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        "image/gif" => "gif",
        "image/svg+xml" => "svg",
        _ when contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => contentType["image/".Length..],
        _ => string.Empty
    };
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

static string GetMessageRoleSummary(JsonElement root)
{
    if (!root.TryGetProperty("messages", out var messagesValue) || messagesValue.ValueKind != JsonValueKind.Array)
    {
        return string.Empty;
    }

    var builder = new StringBuilder();
    foreach (var message in messagesValue.EnumerateArray())
    {
        if (builder.Length > 0)
        {
            builder.Append(", ");
        }

        builder.Append(GetStringProperty(message, "role"));
    }

    return builder.ToString();
}

static string NormalizeRole(string role, ILogger? logger)
{
    if (string.IsNullOrWhiteSpace(role))
    {
        return role;
    }

    var mappedRole = role;
    if (string.Equals(role, "developer", StringComparison.OrdinalIgnoreCase))
    {
        mappedRole = "system";
    }
    else if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "function", StringComparison.OrdinalIgnoreCase))
    {
        mappedRole = "assistant";
    }

    if (logger is not null && !string.Equals(role, mappedRole, StringComparison.Ordinal))
    {
        logger.LogInformation("Mapped role {OriginalRole} to {MappedRole}.", role, mappedRole);
    }

    return mappedRole;
}

static string GetDefaultChatModel(IConfiguration config)
{
    var model = config["Nele:DefaultChatModel"];
    return string.IsNullOrWhiteSpace(model) ? "google-claude-4.5-sonnet" : model.Trim();
}

static string GetDefaultDocumentCollectionId(IConfiguration config)
{
    var collectionId = config["Nele:DefaultDocumentCollectionId"];
    return string.IsNullOrWhiteSpace(collectionId) ? string.Empty : collectionId.Trim();
}

static string GetDefaultReasoningEffort(IConfiguration config)
{
    var effort = config["Nele:ModelConfiguration:ReasoningEffort"];
    return string.IsNullOrWhiteSpace(effort) ? string.Empty : effort.Trim();
}

static int GetToolDescriptionMaxLength(IConfiguration config)
{
    if (int.TryParse(config["Nele:ToolDescriptionMaxLength"], out var maxLength) && maxLength > 0)
    {
        return Math.Min(maxLength, 1000);
    }

    return 1000;
}

static bool IsStreamingForced(IConfiguration config)
{
    return bool.TryParse(config["Nele:ForceStream"], out var forceStream) && forceStream;
}

static JsonObject? BuildModelConfiguration(JsonElement root, IConfiguration config)
{
    JsonObject? modelConfiguration = null;
    var defaultReasoning = GetDefaultReasoningEffort(config);
    if (!string.IsNullOrWhiteSpace(defaultReasoning))
    {
        modelConfiguration = new JsonObject
        {
            ["reasoning_effort"] = defaultReasoning
        };
    }

    if (root.TryGetProperty("modelConfiguration", out var modelConfigValue) && modelConfigValue.ValueKind == JsonValueKind.Object)
    {
        modelConfiguration ??= new JsonObject();
        foreach (var property in modelConfigValue.EnumerateObject())
        {
            modelConfiguration[property.Name] = JsonNode.Parse(property.Value.GetRawText());
        }
    }

    if (root.TryGetProperty("reasoning_effort", out var reasoningOverride) && reasoningOverride.ValueKind == JsonValueKind.String)
    {
        modelConfiguration ??= new JsonObject();
        modelConfiguration["reasoning_effort"] = reasoningOverride.GetString();
    }

    return modelConfiguration;
}

static bool TryGetStreamIncludeUsage(JsonElement root)
{
    if (!root.TryGetProperty("stream_options", out var streamOptions) || streamOptions.ValueKind != JsonValueKind.Object)
    {
        return false;
    }

    return streamOptions.TryGetProperty("include_usage", out var includeUsage)
        && includeUsage.ValueKind == JsonValueKind.True;
}

static string ResolveChatModel(JsonElement root, string defaultModel)
{
    var model = GetStringProperty(root, "model");
    return string.IsNullOrWhiteSpace(model) ? defaultModel : model;
}

static (int MessageCount, int ImagePartCount, int ToolCount, bool HasWebSearch, bool HasDocumentCollection) GetMessageStatistics(JsonElement root)
{
    var messageCount = 0;
    var imagePartCount = 0;
    var toolCount = 0;

    if (root.TryGetProperty("messages", out var messagesValue) && messagesValue.ValueKind == JsonValueKind.Array)
    {
        messageCount = messagesValue.GetArrayLength();
        foreach (var message in messagesValue.EnumerateArray())
        {
            if (!message.TryGetProperty("content", out var contentValue) || contentValue.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in contentValue.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var partType = GetStringProperty(part, "type");
                if (string.Equals(partType, "image_url", StringComparison.OrdinalIgnoreCase))
                {
                    imagePartCount++;
                }
            }
        }
    }

    if (root.TryGetProperty("tools", out var toolsValue) && toolsValue.ValueKind == JsonValueKind.Array)
    {
        toolCount = toolsValue.GetArrayLength();
    }

    var hasWebSearch = root.TryGetProperty("web_search", out var webSearchValue) && webSearchValue.ValueKind != JsonValueKind.Null;
    var hasDocumentCollection = root.TryGetProperty("documentCollectionId", out var documentCollectionValue)
        && documentCollectionValue.ValueKind != JsonValueKind.Null
        && !string.IsNullOrWhiteSpace(documentCollectionValue.ToString());

    return (messageCount, imagePartCount, toolCount, hasWebSearch, hasDocumentCollection);
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

static bool TryNormalizeMessage(JsonElement message, out JsonObject normalized)
{
    normalized = new JsonObject();
    if (message.ValueKind != JsonValueKind.Object)
    {
        return false;
    }

    var role = NormalizeRole(GetStringProperty(message, "role"), null);
    if (!string.IsNullOrWhiteSpace(role))
    {
        normalized["role"] = role;
    }
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

    var response = new JsonObject
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

    if (root.TryGetProperty("web_search_results", out var webSearchResults) && webSearchResults.ValueKind != JsonValueKind.Null)
    {
        response["web_search_results"] = JsonNode.Parse(webSearchResults.GetRawText());
    }

    return response;
}

static async Task WriteChatCompletionStream(HttpContext context, JsonObject openAiResponse, bool includeUsage)
{
    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/event-stream; charset=utf-8";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";
    context.Response.Headers["X-Accel-Buffering"] = "no";

    var id = openAiResponse["id"]?.ToString() ?? $"chatcmpl-{Guid.NewGuid():N}";
    var created = openAiResponse["created"]?.GetValue<long>() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var model = openAiResponse["model"]?.ToString() ?? "unknown";

    var choices = openAiResponse["choices"] as JsonArray;
    var choice = choices is not null && choices.Count > 0 ? choices[0] as JsonObject : null;
    var message = choice?["message"] as JsonObject;
    var finishReason = choice?["finish_reason"]?.ToString() ?? "stop";

    var delta = new JsonObject { ["role"] = "assistant" };
    var content = message?["content"]?.ToString();
    if (!string.IsNullOrWhiteSpace(content) && !string.Equals(content, "null", StringComparison.OrdinalIgnoreCase))
    {
        delta["content"] = content;
    }

    if (message?["tool_calls"] is JsonNode toolCalls)
    {
        delta["tool_calls"] = toolCalls.DeepClone();
    }

    var firstChunk = new JsonObject
    {
        ["id"] = id,
        ["object"] = "chat.completion.chunk",
        ["created"] = created,
        ["model"] = model,
        ["choices"] = new JsonArray
        {
            new JsonObject
            {
                ["index"] = 0,
                ["delta"] = delta,
                ["finish_reason"] = null
            }
        }
    };

    await WriteSseEvent(context, firstChunk);

    var finalChunk = new JsonObject
    {
        ["id"] = id,
        ["object"] = "chat.completion.chunk",
        ["created"] = created,
        ["model"] = model,
        ["choices"] = new JsonArray
        {
            new JsonObject
            {
                ["index"] = 0,
                ["delta"] = new JsonObject(),
                ["finish_reason"] = finishReason
            }
        }
    };

    if (openAiResponse.TryGetPropertyValue("web_search_results", out var webSearchResults) && webSearchResults is not null)
    {
        finalChunk["web_search_results"] = webSearchResults.DeepClone();
    }

    await WriteSseEvent(context, finalChunk);

    if (includeUsage)
    {
        var usage = openAiResponse["usage"]?.DeepClone() ?? new JsonObject
        {
            ["prompt_tokens"] = 0,
            ["completion_tokens"] = 0,
            ["total_tokens"] = 0
        };

        var usageChunk = new JsonObject
        {
            ["id"] = id,
            ["object"] = "chat.completion.chunk",
            ["created"] = created,
            ["model"] = model,
            ["choices"] = new JsonArray(),
            ["usage"] = usage
        };

        await WriteSseEvent(context, usageChunk);
    }

    await context.Response.WriteAsync("data: [DONE]\n\n", context.RequestAborted);
    await context.Response.Body.FlushAsync(context.RequestAborted);
}

static async Task WriteSseEvent(HttpContext context, JsonObject payload)
{
    await context.Response.WriteAsync($"data: {payload.ToJsonString()}\n\n", context.RequestAborted);
    await context.Response.Body.FlushAsync(context.RequestAborted);
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
