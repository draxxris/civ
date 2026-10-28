using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ariel.Game;
using Xunit;

namespace Ariel.Tests;

public sealed class CanonicalJsonTests
{
    [Theory]
    [InlineData("{\"z\":null,\"a\":[{\"b\":true,\"A\":false},3,1]}",
        "{\"a\":[{\"A\":false,\"b\":true},3,1],\"z\":null}")]
    [InlineData("{\"é\":\"<é>\\n\",\"Z\":-0,\"a\":18446744073709551615,\"A\":-9223372036854775808}",
        "{\"A\":-9223372036854775808,\"Z\":0,\"a\":18446744073709551615,\"\\u00E9\":\"\\u003C\\u00E9\\u003E\\n\"}")]
    [InlineData("{\"b\":0,\"a\":2,\"a\":1}", "{\"a\":2,\"a\":1,\"b\":0}")]
    public void DigestHashesExactCanonicalUtf8(string input, string canonical)
    {
        using var document = JsonDocument.Parse(input);
        Assert.Equal(canonical, CanonicalJson.Serialize(document.RootElement));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
            CanonicalJson.Digest(document.RootElement));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1e0")]
    [InlineData("0.5")]
    [InlineData("18446744073709551616")]
    [InlineData("-9223372036854775809")]
    public void DigestRejectsNonIntegerOrOutOfRangeNumbers(string number)
    {
        using var document = JsonDocument.Parse("{\"nested\":[" + number + "]}");
        Assert.Throws<InvalidDataException>(() => CanonicalJson.Digest(document.RootElement));
    }
}
