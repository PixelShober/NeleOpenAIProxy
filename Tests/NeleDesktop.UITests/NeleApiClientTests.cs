using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeleDesktop.Models;
using NeleDesktop.Services;

namespace NeleDesktop.UITests;

[TestClass]
public sealed class NeleApiClientTests
{
    [TestMethod]
    public async Task SendChatAsync_IncludesAttachmentIdAndReturnsContent()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var client = new NeleApiClient(httpClient);
        var messages = new List<ChatMessage>
        {
            new()
            {
                Role = "user",
                Content = "Check attachment",
                Attachments =
                {
                    new ChatAttachment
                    {
                        Id = "att-123",
                        Type = "text",
                        FileName = "notes.txt",
                        ContentType = "text/plain",
                        SizeBytes = 12,
                        Content = "hello world",
                        Encoding = "text"
                    }
                }
            }
        };

        var reply = await client.SendChatAsync(
            "test-key",
            "http://localhost/api:v1/",
            "test-model",
            messages,
            CancellationToken.None);

        Assert.AreEqual("ok", reply);
        Assert.IsNotNull(handler.LastContent, "Request content was not captured.");
        StringAssert.Contains(handler.LastContent, "\"attachments\"");
        StringAssert.Contains(handler.LastContent, "\"id\":\"att-123\"");
        StringAssert.Contains(handler.LastContent, "\"type\":\"text\"");
        StringAssert.Contains(handler.LastContent, "\"name\":\"notes.txt\"");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? LastContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastContent = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"content\":\"ok\"}", Encoding.UTF8, "application/json")
            };
        }
    }
}
