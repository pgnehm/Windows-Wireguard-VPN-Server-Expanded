using FluentAssertions;
using WireGuardServerForWindows.Models;
using Xunit;

namespace WireGuardServerForWindows.Tests
{
    public class DpapiSecretProtectorTests
    {
        [Fact]
        public void ShouldRoundTripASecretForTheCurrentWindowsUser()
        {
            const string secret = "test-private-key";

            string protectedValue = DpapiSecretProtector.Protect(secret);

            protectedValue.Should().StartWith("dpapi:");
            DpapiSecretProtector.Unprotect(protectedValue).Should().Be(secret);
        }

        [Fact]
        public void ShouldNotDoubleProtectAnAlreadyProtectedValue()
        {
            string protectedValue = DpapiSecretProtector.Protect("test-private-key");

            DpapiSecretProtector.Protect(protectedValue).Should().Be(protectedValue);
        }
    }
}
