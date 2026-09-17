using Xunit;

namespace NotifyMessages.Tests;

public class GeoIpAddressTests
{
    [Theory]
    [InlineData("1.2.3.4:27015", "1.2.3.4")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("[2001:db8::1]:27015", "2001:db8::1")]
    [InlineData("2001:db8::1", "2001:db8::1")]   // голый IPv6 — раньше ломался о Split(':')[0]
    [InlineData("::1", "::1")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ExtractIp_HandlesIpv4AndIpv6(string? raw, string expected)
    {
        Assert.Equal(expected, GeoIpService.ExtractIp(raw));
    }
}
