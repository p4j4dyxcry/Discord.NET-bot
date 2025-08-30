using TsDiscordBot.Core.Services;
using Xunit;

namespace TsDiscordBot.Tests;

public class AnonymousProfileProviderTests
{
    [Fact]
    public void IndexZeroReturnsChris()
    {
        var profile = AnonymousProfileProvider.GetProfile(0);
        Assert.Equal("クリス", profile.Name);
    }

    [Fact]
    public void DiscriminatorFormatsToFourDigits()
    {
        Assert.Equal("0000", AnonymousProfileProvider.GetDiscriminator(0));
        Assert.Equal("0256", AnonymousProfileProvider.GetDiscriminator(256));
    }

    [Fact]
    public void SameBaseNameGetsDifferentDiscriminators()
    {
        var profile1 = AnonymousProfileProvider.GetProfile(0);
        var profile2 = AnonymousProfileProvider.GetProfile(256);
        Assert.Equal(profile1.Name, profile2.Name);
        Assert.NotEqual(
            AnonymousProfileProvider.GetDiscriminator(0),
            AnonymousProfileProvider.GetDiscriminator(256)
        );
    }
}

