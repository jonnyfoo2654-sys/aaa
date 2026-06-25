using FluentAssertions;
using QTO.TextRecognition;
using Xunit;

namespace QTO.Core.Tests
{
    public class TextRecognizerTests
    {
        private readonly TextRecognizer _sut = new();

        [Theory]
        [InlineData("Depth = 800 mm",   "Depth",    "800")]
        [InlineData("Depth: 1.2 m",     "Depth",    "1.2")]
        [InlineData("depth=500mm",      "Depth",    "500")]
        public void ParseDepth_ShouldExtractCorrectValue(string text, string key, string expectedValuePart)
        {
            var result = _sut.ParseProperties(text);
            result.Should().ContainKey(key);
            result[key].Should().Contain(expectedValuePart);
        }

        [Theory]
        [InlineData("4C x 240 mm²",  "CableSize", "4C")]
        [InlineData("3x185mm2",      "CableSize", "3")]
        [InlineData("1C x 95 mm²",   "CableSize", "1C")]
        public void ParseCableSize_ShouldExtractCoreConfig(string text, string key, string expectedPart)
        {
            var result = _sut.ParseProperties(text);
            result.Should().ContainKey(key);
            result[key].Should().Contain(expectedPart);
        }

        [Theory]
        [InlineData("Voltage = 13.8 kV", "Voltage", "13.8")]
        [InlineData("kv=33",             "Voltage", "33")]
        public void ParseVoltage_ShouldExtract(string text, string key, string expectedPart)
        {
            var result = _sut.ParseProperties(text);
            result.Should().ContainKey(key);
            result[key].Should().Contain(expectedPart);
        }

        [Theory]
        [InlineData("Material: XLPE",          "Material", "XLPE")]
        [InlineData("Material = PVC",           "Material", "PVC")]
        public void ParseMaterial_ShouldExtract(string text, string key, string expected)
        {
            var result = _sut.ParseProperties(text);
            result.Should().ContainKey(key);
            result[key].Should().Contain(expected);
        }

        [Fact]
        public void ParseMultiLine_ShouldExtractAllProperties()
        {
            var text = "4C x 240 mm²\nDepth = 1.0m\nMaterial: XLPE\nVoltage = 11 kV";
            var result = _sut.ParseProperties(text);

            result.Should().ContainKey("CableSize");
            result.Should().ContainKey("Depth");
            result.Should().ContainKey("Insulation").WhoseValue.ToUpper().Should().Be("XLPE");
            result.Should().ContainKey("Voltage");
        }

        [Fact]
        public void ParseEmpty_ShouldReturnEmptyDictionary()
        {
            var result = _sut.ParseProperties(string.Empty);
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParseGenericKeyValue_ShouldCaptureUnknownPairs()
        {
            var result = _sut.ParseProperties("Location: Basement");
            result.Should().ContainKey("Location");
            result["Location"].Should().Contain("Basement");
        }
    }
}
