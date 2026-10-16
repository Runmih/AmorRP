using AmorRP.Plugin.Services.Ui;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class WrappedTextProjectionTests
{
    private static WrappedTextProjection Wrap(string text, int width = 5) => WrappedTextProjection.Create(text, width, _ => 1);
    [Fact]
    public void Wraps_words_and_long_tokens_without_changing_original_text()
    {
        var words = Wrap("one two three");
        Assert.Equal("one \ntwo \nthree", words.Display);
        Assert.Equal("one two three", words.Source);
        var token = Wrap(new string('d', 23));
        Assert.Equal("ddddd\nddddd\nddddd\nddddd\nddd", token.Display);
        Assert.Equal(token.Source, token.Apply(token.Display).Text);
    }
    [Fact]
    public void Preserves_real_line_breaks_spaces_and_unicode_graphemes()
    {
        const string source = "a  b\n\n猫猫猫猫猫猫\r\n👩‍💻e\u0301👩‍💻e\u0301";
        var view = Wrap(source, 2);
        Assert.Equal(source, view.Apply(view.Display).Text);
        Assert.Contains("\n\n", view.Display);
        Assert.Contains("\r\n", view.Display);
        Assert.DoesNotContain("👩\n", view.Display);
        Assert.DoesNotContain("e\n\u0301", view.Display);
        for (var i = 0; i <= source.Length; i++) Assert.Equal(i, view.ToSource(view.ToDisplay(i)));
    }
    [Fact]
    public void Typing_pasting_and_replacing_a_selection_across_soft_breaks_keep_only_intended_text()
    {
        var view = Wrap("abcdefghij"); // abcde\nfghij
        Assert.Equal("abcdeXfghij", view.Apply(view.Display.Insert(6, "X")).Text);
        Assert.Equal("abcde\nXfghij", view.Apply(view.Display.Insert(6, "\nX")).Text);
        var replaced = view.Apply("ab猫\nletterij");
        Assert.Equal("ab猫\nletterij", replaced.Text);
        Assert.Equal(2, replaced.Position(2));
        Assert.Equal(11, replaced.Position(11));
    }
    [Fact]
    public void Backspace_and_delete_at_a_soft_break_remove_the_adjacent_grapheme()
    {
        var view = Wrap("abcdefghij");
        var withoutBreak = view.Display.Remove(5, 1);
        var backward = view.Apply(withoutBreak, backspace: true);
        var forward = view.Apply(withoutBreak, delete: true);
        Assert.Equal("abcdfghij", backward.Text);
        Assert.Equal("abcdeghij", forward.Text);
        Assert.Equal(4, backward.Position(5));
        Assert.Equal(5, forward.Position(5));
        // Removing a selected visual break alone must not delete saved text.
        Assert.Equal(view.Source, view.Apply(withoutBreak).Text);
    }
    [Fact]
    public void Deleting_a_real_line_break_is_a_real_edit_and_resizing_does_not_edit_text()
    {
        var view = Wrap("abc\ndefgh");
        Assert.Equal("abcdefgh", view.Apply(view.Display.Remove(3, 1), backspace: true).Text);
        foreach (var width in new[] { 1, 2, 5, 10, 100 })
        {
            var resized = Wrap(view.Source, width);
            Assert.Equal(view.Source, resized.Apply(resized.Display).Text);
        }
    }
    [Fact]
    public void Enter_at_either_side_of_a_soft_break_preserves_caret_after_the_real_newline()
    {
        var view = Wrap("abcdefghij");
        foreach (var position in new[] { 5, 6 })
        {
            var entered = view.Display.Insert(position, "\n");
            var edit = view.Apply(entered, cursor: position + 1);
            Assert.Equal("abcde\nfghij", edit.Text);
            Assert.Equal(6, edit.Position(position + 1));
        }
    }
    [Fact]
    public void Insertions_and_deletions_at_every_visible_position_preserve_the_logical_text()
    {
        var view = Wrap("aaaaaa bbbbbb\nccccc");
        for (var i = 0; i <= view.Display.Length; i++)
        {
            var logical = view.ToSource(i);
            var insert = view.Apply(view.Display.Insert(i, "猫\n"), cursor: i + 2);
            Assert.Equal(view.Source.Insert(logical, "猫\n"), insert.Text);
            Assert.Equal(logical + 2, insert.Position(i + 2));
            if (i == view.Display.Length || view.ToSource(i + 1) == logical) continue;
            var deletion = view.Apply(view.Display.Remove(i, 1), delete: true, cursor: i);
            Assert.Equal(view.Source.Remove(logical, 1), deletion.Text);
            Assert.Equal(logical, deletion.Position(i));
        }
    }
    [Fact]
    public void Zero_width_and_empty_inputs_make_progress()
    {
        Assert.Equal("", Wrap("").Display);
        Assert.Equal("a\nb\nc", WrappedTextProjection.Create("abc", 0, _ => 10).Display);
    }
}
