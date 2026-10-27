using System.Net;
using AmorRP.Plugin.Services.Api;
using Xunit;
namespace AmorRP.Plugin.Tests;

public sealed class UserFeedbackTests
{
    [Theory]
    [InlineData("quota_exhausted", "weekly")]
    [InlineData("version_mismatch", "Refresh")]
    [InlineData("trading_restricted", "Trading")]
    [InlineData("invitation_unavailable", "new code")]
    public void Errors_explain_a_next_step_and_keep_debug_identity_in_details(string code, string expected)
    {
        var failure = new FoundationApiException(HttpStatusCode.Conflict, code, "opaque_machine_code", "request-123");
        Assert.Contains(expected, failure.Message); Assert.DoesNotContain("opaque_machine_code", failure.Message);
        Assert.Contains(code, UserFeedback.Details(failure)); Assert.Contains("request-123", UserFeedback.Details(failure));
        Assert.Contains("opaque_machine_code", UserFeedback.Details(failure));
    }
    [Fact]
    public void Startup_and_unknown_failures_do_not_show_raw_codes_to_players()
    {
        Assert.Contains("starting up", UserFeedback.Error(HttpStatusCode.ServiceUnavailable, "unknown", "Unavailable"));
        Assert.DoesNotContain("unknown_code", UserFeedback.Error(HttpStatusCode.BadRequest, "unknown_code", "unknown_code"));
        Assert.Contains("permissions saved", UserFeedback.Success(new("PUT", "api/v2/communities/11111111-1111-4111-8111-111111111111/members/22222222-2222-4222-8222-222222222222/capabilities", null, null)));
    }
}
