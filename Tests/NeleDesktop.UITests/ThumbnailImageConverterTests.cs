using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeleDesktop.Converters;

namespace NeleDesktop.UITests;

[TestClass]
public sealed class ThumbnailImageConverterTests
{
    [TestMethod]
    public void ThumbnailImageConverter_ReturnsNullForMissingFile()
    {
        var converter = new ThumbnailImageConverter();
        var result = converter.Convert("Z:\\does-not-exist.png", typeof(object), null!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsNull(result);
    }
}
