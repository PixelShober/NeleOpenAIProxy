using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeleDesktop.Services;

namespace NeleDesktop.UITests;

[TestClass]
public sealed class AttachmentPolicyTests
{
    [TestMethod]
    public void AttachmentPolicy_RejectsUnsupportedExtension()
    {
        var root = Path.Combine(Path.GetTempPath(), "NeleAIProxyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "sample.psd");
        File.WriteAllText(path, "test");

        try
        {
            var result = AttachmentPolicy.ValidateFile(path);
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains(result.Title, "unterstuetzt");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void AttachmentPolicy_AllowsTextFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "NeleAIProxyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "notes.txt");
        File.WriteAllText(path, "hello");

        try
        {
            var result = AttachmentPolicy.ValidateFile(path);
            Assert.IsTrue(result.IsValid);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void AttachmentPolicy_AllowsImageFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "NeleAIProxyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "preview.png");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });

        try
        {
            var result = AttachmentPolicy.ValidateFile(path);
            Assert.IsTrue(result.IsValid);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
