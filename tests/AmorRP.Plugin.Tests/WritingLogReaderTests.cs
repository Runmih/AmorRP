using System.Net;
using System.Net.Http.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Features.Inventory;
using AmorRP.Plugin.Features.Writing;
using AmorRP.Plugin.Services.Api;
using Xunit;

namespace AmorRP.Plugin.Tests;

public sealed class WritingLogReaderTests
{
    private static readonly Guid Season = Guid.NewGuid(), Actor = Guid.NewGuid();
    private static Holding Written(string title = "A private journal") => new(Guid.NewGuid(), Season, Actor,
        "book", title, Guid.NewGuid(), 1, 0, 1, 1, null, null, Guid.NewGuid());
    private static HoldingDetails Details(Holding item) => new(item, Letter: new(item.LetterId!.Value,
        Season, new Character(Guid.NewGuid(), "Other Author", "1", "World", DateTimeOffset.UtcNow, "verified"),
        item.CategoryId, item.Name, "A private body that is not part of the list.", 1, true, DateTimeOffset.UtcNow, item.TypeId));
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(value, options: FoundationClient.Json) };

    [Fact]
    public async Task Library_search_is_filtered_before_pagination_and_does_not_prefetch_private_bodies()
    {
        var first = Written(); var second = Written("Second journal");
        using var handler = new Replies(request => Json(request.RequestUri!.Query.Contains("cursor=")
            ? new HoldingPage([second], null, "2") : new HoldingPage([first], "next+page/", "1")));
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(2));
        var query = new InventoryCompanionQuery { Section = InventoryCompanionSection.Writing, Search = "Tea & books", Sort = 1 }.Build();
        var result = await WritingLogReader.ReadAsync(client, new("https://example.com/"), "token", Season,
            Actor, query, null, 2, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { first.Id, second.Id }, result.Page.Items.Select(x => x.Id));
        Assert.Null(result.Selected); Assert.Null(result.Page.NextCursor);
        Assert.Equal(2, handler.Paths.Count);
        Assert.All(handler.Paths, path => Assert.Contains("typeIds=book,letter,note", path));
        Assert.All(handler.Paths, path => Assert.Contains("search=Tea%20%26%20books", path));
        Assert.Contains("cursor=next%2Bpage%2F", handler.Paths[1]);
        Assert.All(handler.Authorization, value => Assert.Equal("Bearer token", value));
    }

    [Fact]
    public async Task A_selected_owned_item_can_be_read_directly_even_when_its_author_is_someone_else_and_it_is_not_on_the_first_page()
    {
        var selected = Written(); var details = Details(selected);
        using var handler = new Replies(request => Json(request.RequestUri!.AbsolutePath.EndsWith(selected.Id.ToString())
            ? (object)details : new HoldingPage([Written("Another title")], "later", "1")));
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(2));
        var result = await WritingLogReader.ReadAsync(client, new("https://example.com/"), "token", Season,
            Actor, "sort=name&typeIds=book,letter,note", selected.Id, 0, TestContext.Current.CancellationToken);
        Assert.Equal(details.Letter!.Body, result.Selected!.Letter!.Body);
        Assert.NotEqual(Actor, result.Selected.Letter.Author.Id);
        Assert.Equal(2, handler.Paths.Count);
    }

    [Theory]
    [InlineData("season")]
    [InlineData("character")]
    [InlineData("type")]
    [InlineData("quantity")]
    [InlineData("holding-id")]
    [InlineData("letter-id")]
    [InlineData("letter-community")]
    [InlineData("letter-type")]
    [InlineData("missing-letter")]
    public async Task Mismatched_selected_responses_are_rejected_before_their_contents_can_be_displayed(string mismatch)
    {
        var selected = Written(); var details = Details(selected);
        details = mismatch switch
        {
            "season" => details with { Holding = selected with { CommunityId = Guid.NewGuid() } },
            "character" => details with { Holding = selected with { CharacterId = Guid.NewGuid() } },
            "type" => details with { Holding = selected with { TypeId = "potion" } },
            "quantity" => details with { Holding = selected with { Quantity = 0 } },
            "holding-id" => details with { Holding = selected with { Id = Guid.NewGuid() } },
            "letter-id" => details with { Letter = details.Letter! with { Id = Guid.NewGuid() } },
            "letter-community" => details with { Letter = details.Letter! with { CommunityId = Guid.NewGuid() } },
            "letter-type" => details with { Letter = details.Letter! with { TypeId = "letter" } },
            _ => details with { Letter = null }
        };
        using var handler = new Replies(request => Json(request.RequestUri!.AbsolutePath.EndsWith(selected.Id.ToString())
            ? (object)details : new HoldingPage([selected], null, "1")));
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WritingLogReader.ReadAsync(client, new("https://example.com/"), "token",
            Season, Actor, "sort=name&typeIds=book,letter,note", selected.Id, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Losing_an_item_clears_its_reader_result_and_does_not_reuse_a_previously_fetched_body()
    {
        var selected = Written(); var removed = false;
        using var handler = new Replies(request => !request.RequestUri!.AbsolutePath.EndsWith(selected.Id.ToString())
            ? Json(new HoldingPage([], null, "1")) : removed
                ? new(HttpStatusCode.NotFound) { Content = JsonContent.Create(new { code = "not_found", title = "No longer held" }) }
                : Json(Details(selected)));
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(2));
        var first = await WritingLogReader.ReadAsync(client, new("https://example.com/"), "token", Season,
            Actor, "sort=name&typeIds=book,letter,note", selected.Id, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(first.Selected);
        removed = true;
        var after = await WritingLogReader.ReadAsync(client, new("https://example.com/"), "token", Season,
            Actor, "sort=name&typeIds=book,letter,note", selected.Id, 0, TestContext.Current.CancellationToken);
        Assert.Null(after.Selected);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Another_character_or_community_cannot_supply_the_writing_list(bool wrongCharacter)
    {
        var item = Written();
        item = wrongCharacter ? item with { CharacterId = Guid.NewGuid() } : item with { CommunityId = Guid.NewGuid() };
        using var handler = new Replies(_ => Json(new HoldingPage([item], null, "1")));
        using var client = new FoundationClient(handler, TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WritingLogReader.ReadAsync(client, new("https://example.com/"), "token",
            Season, Actor, "sort=name&typeIds=book,letter,note", null, 0, TestContext.Current.CancellationToken));
        Assert.Single(handler.Paths);
    }

    private sealed class Replies(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string?> Authorization { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsoluteUri); Authorization.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(reply(request));
        }
    }
}
