using AmorRP.Core.Items;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class TextInputLimitTests
{
    [Fact]
    public void Paste_is_clipped_at_limit_and_keeps_text_after_the_selection()
    {
        var edit = TextInputLimit.Apply("abXYcd", "ab123456cd", 8);
        Assert.Equal("ab1234cd", edit.Text);
        Assert.Equal(6, edit.Position(8)); Assert.Equal(8, edit.Position(10));
        Assert.Equal("abc", TextInputLimit.Apply("abc", "abcd", 3).Text);
        Assert.Equal("axc", TextInputLimit.Apply("abc", "axc", 3).Text);
    }
    [Fact]
    public void Unicode_limits_use_whole_text_elements_and_chat_also_respects_byte_budget()
    {
        Assert.Equal("🌸e\u0301", TextInputLimit.Apply("", "🌸e\u0301猫", 2).Text);
        Assert.Equal("👩‍💻", TextInputLimit.Apply("", "👩‍💻🌸", 1).Text);
        Assert.Equal("猫", TextInputLimit.Apply("", "猫猫", 50, 5).Text);
        Assert.Equal("🌸", TextInputLimit.Apply("", "🌸🌸", 3, maxCodeUnits: 3).Text);
        Assert.Equal(new string('a', 300), TextInputLimit.Apply("", new string('a', 301), 300).Text);
        Assert.Equal(new string('b', 500), TextInputLimit.Apply("", new string('b', 501), 500).Text);
    }
    [Fact]
    public void Editing_an_old_oversized_value_returns_a_valid_draft()
    {
        Assert.Equal("abc", TextInputLimit.Apply("abcdef", "abcdex", 3).Text);
        Assert.Equal("ab", TextInputLimit.Apply("abcd", "ab", 3).Text);
    }
}
