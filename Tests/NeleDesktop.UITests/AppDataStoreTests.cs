using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeleDesktop.Models;
using NeleDesktop.Services;

namespace NeleDesktop.UITests;

[TestClass]
public sealed class AppDataStoreTests
{
    [TestMethod]
    public async Task AppDataStore_SavesAndLoadsSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "NeleAIProxyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var store = new AppDataStore(root);
            var settings = new AppSettings
            {
                ApiKey = "test-key",
                BaseUrl = "http://localhost:5155/api:v1/",
                SelectedModel = "model-a",
                TemporaryChatModel = "model-b",
                DarkMode = false,
                Hotkey = "Ctrl+Alt+Space",
                TemporaryHotkey = "Ctrl+Alt+T",
                WindowLeft = 12,
                WindowTop = 34,
                WindowWidth = 560,
                WindowHeight = 720
            };

            await store.SaveSettingsAsync(settings);
            var loaded = await store.LoadSettingsAsync();

            Assert.AreEqual(settings.ApiKey, loaded.ApiKey);
            Assert.AreEqual(settings.BaseUrl, loaded.BaseUrl);
            Assert.AreEqual(settings.SelectedModel, loaded.SelectedModel);
            Assert.AreEqual(settings.TemporaryChatModel, loaded.TemporaryChatModel);
            Assert.AreEqual(settings.DarkMode, loaded.DarkMode);
            Assert.AreEqual(settings.Hotkey, loaded.Hotkey);
            Assert.AreEqual(settings.TemporaryHotkey, loaded.TemporaryHotkey);
            Assert.AreEqual(settings.WindowLeft, loaded.WindowLeft);
            Assert.AreEqual(settings.WindowTop, loaded.WindowTop);
            Assert.AreEqual(settings.WindowWidth, loaded.WindowWidth);
            Assert.AreEqual(settings.WindowHeight, loaded.WindowHeight);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
