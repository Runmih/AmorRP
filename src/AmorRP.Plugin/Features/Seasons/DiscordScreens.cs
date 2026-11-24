using AmorRP.Contracts.Discord;
using AmorRP.Contracts.Groups;
using AmorRP.Plugin.Services.Api;
using Dalamud.Bindings.ImGui;
using GroupProfile = AmorRP.Contracts.Groups.Group;

namespace AmorRP.Plugin.Features.Seasons;

public sealed partial class SeasonsPanel
{
    private readonly BackgroundRefresh<DiscordPlayerState> discordPlayerRefresh = new(TimeSpan.FromSeconds(15));
    private readonly BackgroundRefresh<DiscordGroupState> discordGroupRefresh = new(TimeSpan.FromSeconds(15));
    private DiscordRewardPolicy? discordPolicy, savedDiscordPolicy;
    private string? discordCeilingText;
    private DiscordPlayerState? discordPlayer;
    private DiscordGroupState? discordGroup;
    private DiscordCode? discordPlayerCode, discordGroupCode;
    private Guid? discordGroupId;

    private void ResetDiscordViews()
    {
        discordPlayerRefresh.Reset();
        discordGroupRefresh.Reset();
        discordPlayer = null;
        discordGroup = null;
        discordGroupId = null;
        discordPlayerCode = discordGroupCode = null;
        discordPolicy = savedDiscordPolicy = null;
        discordCeilingText = null;
    }

    private string DiscordPath => $"api/v2/seasons/{community!.Id}/discord";

    private void TickDiscordSettings()
    {
        var now = DateTimeOffset.UtcNow;
        if (discordPlayerRefresh.TryCollect(now, dataGeneration, out var result))
        {
            discordPlayer = result;
            if (result.Linked) discordPlayerCode = null;
            if (discordPolicy == savedDiscordPolicy)
            {
                discordPolicy = result.Policy;
                discordCeilingText = null;
            }
            savedDiscordPolicy = result.Policy;
        }
        if (session == null || origin == null || SessionRenewalInProgress) return;
        var uri = origin;
        var token = session.AccessToken;
        var path = DiscordPath;
        discordPlayerRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<DiscordPlayerState>(uri, path, token, ct));
    }

    private void DrawDiscordPlayer()
    {
        TickDiscordSettings();
        if (discordPlayer is not { } player || (!player.Available && !player.Linked)) return;
        if (!ImGui.CollapsingHeader("Discord writing rewards")) return;
        if (player.Linked)
        {
            ImGui.TextUnformatted("This character earns rewards from eligible Discord posts.");
            if (ActionButton("Unlink Discord")) Mutate("DELETE", DiscordPath + "/link", null);
        }
        else if (player.Available)
        {
            if (ActionButton("Link Discord")) Mutate("POST", DiscordPath + "/link", null);
            DrawDiscordCode(discordPlayerCode, "link");
        }
    }

    private void DrawDiscordGroup(GroupProfile group)
    {
        if (discordGroupId != group.Id)
        {
            discordGroupId = group.Id;
            discordGroup = null;
            discordGroupCode = null;
            discordGroupRefresh.Reset();
        }
        var now = DateTimeOffset.UtcNow;
        if (discordGroupRefresh.TryCollect(now, dataGeneration, out var loaded))
        {
            discordGroup = loaded;
            if (loaded.Connected) discordGroupCode = null;
        }
        TickDiscordSettings();
        var path = GroupPath(group) + "/discord";
        var uri = origin!;
        var token = session!.AccessToken;
        discordGroupRefresh.Start(now, dataGeneration, requests.Token,
            ct => client.GetAsync<DiscordGroupState>(uri, path, token, ct));
        DrawRefreshState(discordGroupRefresh, "Discord connection");
        if (discordGroup is { Connected: true })
        {
            ImGui.TextUnformatted("Connected to Discord");
            if (ActionButton("Disconnect bot")) Mutate("DELETE", path + "/link", null);
        }
        else if (discordPlayer?.Available == true)
        {
            if (ActionButton("Create bot link")) Mutate("POST", path + "/link", null);
            DrawDiscordCode(discordGroupCode, "connect");
        }
        else ImGui.TextDisabled("Discord rewards are disabled for this season.");
    }

    private static void DrawDiscordCode(DiscordCode? code, string verb)
    {
        if (code is not { } active || active.ExpiresAt <= DateTimeOffset.UtcNow) return;
        ImGui.TextWrapped("Use this command on Discord to complete the link within 10 minutes.");
        ImGui.TextUnformatted("/amorrp " + verb);
        ImGui.SameLine();
        if (ImGui.SmallButton("Copy link code")) ImGui.SetClipboardText(active.Code);
    }

    private void DrawDiscordPolicy()
    {
        TickDiscordSettings();
        DrawRefreshState(discordPlayerRefresh, "Discord settings");
        if (discordPolicy is not { } draft) return;
        var enabled = draft.Enabled;
        var coins = draft.CoinsPerPost;
        var minimum = draft.MinimumCharacters;
        var cooldown = draft.CooldownSeconds;
        var weekly = draft.WeeklyLimit;
        var budget = draft.SeasonWeeklyBudget;
        var ceiling = draft.BalanceCeiling;
        ImGui.Checkbox("Enable Discord writing rewards", ref enabled);
        ImGui.InputInt("Coins per eligible post", ref coins);
        ImGui.InputInt("Minimum message length", ref minimum);
        ImGui.InputInt("Cooldown (seconds)", ref cooldown);
        ImGui.InputInt("Weekly coins per player", ref weekly);
        ImGui.InputInt("Season weekly budget", ref budget);
        var ceilingInput = discordCeilingText ?? ceiling.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (ImGui.InputText("Personal balance ceiling", ref ceilingInput, 21, ImGuiInputTextFlags.CharsDecimal))
            discordCeilingText = ceilingInput;
        var validCeiling = long.TryParse(ceilingInput, out var limit) && limit > 0;
        if (validCeiling) ceiling = limit;
        discordPolicy = new(enabled, coins, minimum, cooldown, weekly, budget, ceiling);
        var valid = validCeiling && coins is >= 1 and <= 10000 && minimum is >= 10 and <= 2000
            && cooldown is >= 10 and <= 86400 && weekly is >= 1 and <= 1000000 && budget is >= 1 and <= 100000000;
        ImGui.BeginDisabled(!valid || discordPolicy == savedDiscordPolicy);
        if (ActionButton("Save Discord settings")) Mutate("PUT", DiscordPath + "/policy", discordPolicy);
        ImGui.EndDisabled();
    }

    private void ApplyDiscordSuccess(CommandRequest request, string json, bool recovering)
    {
        switch (request.Operation.Kind)
        {
            case CommandKind.SetDiscordPolicy:
                discordPolicy = savedDiscordPolicy = ReadCommand<DiscordRewardPolicy>(json);
                discordCeilingText = null;
                break;
            case CommandKind.CreateDiscordPlayerCode when !recovering:
                discordPlayerCode = ReadCommand<DiscordCode>(json);
                break;
            case CommandKind.CreateDiscordGroupCode when !recovering && request.AppliesToGroup(discordGroupId):
                discordGroupCode = ReadCommand<DiscordCode>(json);
                break;
            case CommandKind.UnlinkDiscordPlayer:
                discordPlayerCode = null;
                break;
            case CommandKind.UnlinkDiscordGroup when request.AppliesToGroup(discordGroupId):
                discordGroupCode = null;
                break;
        }
        discordPlayerRefresh.Invalidate();
        discordGroupRefresh.Invalidate();
    }
}
