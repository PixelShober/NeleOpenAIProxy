using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeleDesktop.Services;

namespace NeleDesktop.UITests;

[TestClass]
public sealed class TranscriptionPolicyTests
{
    [TestMethod]
    public void TranscriptionPolicy_UsesDocLimit()
    {
        Assert.AreEqual(200L * 1024 * 1024, TranscriptionPolicy.MaxBytes);
    }
}
