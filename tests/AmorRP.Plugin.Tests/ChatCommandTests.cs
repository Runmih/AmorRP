using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Game;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class ChatCommandTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("Normal RP text.", true)]
    [InlineData("drinks a potion <3", true)]
    [InlineData("feels better >:)", true)]
    [InlineData("drinks <3 <t>", false)]
    [InlineData("<se.1>", false)]
    [InlineData("<3>", false)]
    [InlineData("payload\u0002text\u0003", false)]
    [InlineData("100% effective %s %n", true)]
    [InlineData("hello\n/say spam", false)]
    [InlineData("hello\rworld", false)]
    [InlineData("hidden\u202etext", false)]
    [InlineData("<t> uses a potion", false)]
    [InlineData("line\u2028break", false)]
    public void Content_is_literal_bounded_and_cannot_inject_chat_commands(string text, bool accepted) =>
        Assert.Equal(accepted, ChatCommand.TryCreate(text, 0, out _));

    [Fact]
    public void Grapheme_and_byte_limits_are_enforced()
    {
        Assert.True(ChatCommand.TryCreate(new string('a', 50), 1, out _));
        Assert.False(ChatCommand.TryCreate(new string('a', 51), 1, out _));
        Assert.True(ChatCommand.TryCreate(string.Concat(Enumerable.Repeat("e\u0301", 50)), 1, out _));
        Assert.False(ChatCommand.TryCreate("a" + new string('\u0301', 150), 1, out _));
    }

    [Fact]
    public void Literal_hearts_are_unchanged_and_the_discovered_character_limit_is_honored()
    {
        Assert.True(ChatCommand.TryCreate("drinks a potion <3", 1, out var command));
        Assert.Equal("/say drinks a potion <3", command);
        Assert.True(ChatCommand.TryCreate(new string('a', 60), 1, out _, maximum: 60));
        Assert.False(ChatCommand.TryCreate(new string('a', 60), 1, out _, maximum: 50));
        Assert.False(ChatCommand.TryCreate("bad\ud800text", 1, out _));
    }

    [Fact]
    public void All_destinations_use_fixed_explicit_channel_commands()
    {
        var expected = new[] { "/em", "/say", "/p", "/l1", "/l2", "/l3", "/l4", "/l5", "/l6", "/l7", "/l8", "/cwl1", "/cwl2", "/cwl3", "/cwl4", "/cwl5", "/cwl6", "/cwl7", "/cwl8" };
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(ChatCommand.TryCreate("test", i, out var command));
            Assert.Equal(expected[i] + " test", command);
        }
        Assert.False(ChatCommand.TryCreate("test", -1, out _));
        Assert.False(ChatCommand.TryCreate("test", 19, out _));
    }

    [Theory]
    [InlineData("https://xivauth.net/oauth/authorize?state=test", true)]
    [InlineData("http://xivauth.net/oauth/authorize", false)]
    [InlineData("https://xivauth.net.evil.com/oauth/authorize", false)]
    [InlineData("https://evil.com/oauth/authorize", false)]
    [InlineData("https://user:password@xivauth.net/oauth/authorize", false)]
    [InlineData("https://xivauth.net:444/oauth/authorize", false)]
    [InlineData("https://xivauth.net/other", false)]
    public void Browser_authorization_origin_is_allowlisted(string url, bool accepted) =>
        Assert.Equal(accepted, XivAuthAuthorization.IsTrusted(url));
}
