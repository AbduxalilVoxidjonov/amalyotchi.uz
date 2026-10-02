using System.Net;
using Amaliyotchi.Api.Infrastructure;
using FluentAssertions;

namespace Amaliyotchi.IntegrationTests.Unit;

public sealed class RateLimitPartitionKeyTests
{
    [Theory]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("::ffff:1.2.3.4", "1.2.3.4")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    public void PartitionKey_IPv4ToLiq_IPv6Prefiks64(string ip, string expected) =>
        RateLimitPolicies.PartitionKey(IPAddress.Parse(ip)).Should().Be(expected);

    [Fact]
    public void PartitionKey_Null_Unknown() =>
        RateLimitPolicies.PartitionKey(null).Should().Be("unknown");
}
