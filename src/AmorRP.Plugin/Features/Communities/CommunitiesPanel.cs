using AmorRP.Contracts.Inventory;
using AmorRP.Plugin.Services.Game;
using AmorRP.Plugin.Services.Media;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AmorRP.Contracts.Auth;
using AmorRP.Contracts.Communities;
using AmorRP.Contracts.Currency;
using AmorRP.Contracts.Common;
using AmorRP.Plugin.Services.Api;
using AmorRP.Plugin.Services.Identity;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Plugin.Services;

namespace AmorRP.Plugin.Features.Communities;

public sealed partial class CommunitiesPanel(IPlayerState player, Configuration configuration, Action save, IPartyList party, ITextureProvider textures) : IDisposable
{
    private readonly FoundationClient client = new();
    private CancellationTokenSource lifetime = new();
    private Task<Action>? work, syncWork;
    private long viewGeneration, dataGeneration;
    private long syncView, syncData;
    private readonly Queue<(Func<CancellationToken, Task<Action>> Run, string Message)> reads = new();
    private enum MainSection { Home, Inventory, Crafting, Groups, Noticeboard, Members, Settings }
    private MainSection mainSection = MainSection.Inventory;
    private bool WriteBlocked => !serviceCompatible || currentCommand != null || configuration.PendingCommands.Any(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session?.Character.Id);
    private bool ActionButton(string label, bool mutation = true)
    {
        ImGui.BeginDisabled(mutation && WriteBlocked); var clicked = ImGui.Button(label); ImGui.EndDisabled(); return clicked;
    }
    private string scope = "";
    private readonly CommunityImageCache icons = new(textures, "currency icon", true);
    private readonly CommunityImageCache logos = new(textures, "community logo");
    private DateTimeOffset lastRefresh, nextRefresh;
    private int syncFailures;
    private bool showInactive, serviceCompatible;
    private string? memberDraftEtag;
    private int policyDraftVersion, editCommunityVersion, editCurrencyVersion;
    private string removalReason = "";
    private string editDescription = "", editCurrencyName = "";
    private PublicLimits? serviceLimits;
    private string status = "Sign in with XIVAuth to manage your communities.";
    private Uri? origin;
    private SessionTokens? session;
    private SavedSession? saved;
    private PendingCommand? currentCommand, queuedCommand;
    private bool queuedSignOut, queuedRecovery;
    private bool refreshNeeded;
    private DateTimeOffset nextRenewAttempt;
    private Community[] communities = [];
    private Community? community;
    private Member[] members = [];
    private Invitation[] invitations = [];
    private CommunityPolicies? policies;
    private OwnershipTransfer? transfer;
    private string? membersCursor, invitationsCursor;
    private Guid? selected;
    private string invitationCode = "";
    private bool visibilityAccepted;
    private string createdCode = "";
    private string name = "", description = "", currencyName = "Coins", symbol = "C";
    private int initialPoints, initialCooking, initialLetters = 5;
    private Guid? memberSelection;
    private bool grantCoins, grantPotions, grantRemoval, grantDefinitions, grantGeneralModeration, grantAnnouncements, grantGameplayPermissions, grantGroupCreation, grantGroupManagement, grantCooking, grantCookingDefinitions;
    private bool tradingAllowed = true;
    private string reason = "", delta = "";
    private Balance? selectedMemberBalance;
    private Guid? balanceFor;
    private string deleteConfirmation = "";
    private int nextPoints, nextLetters, nextCooking;

    public void Draw(string backendUrl, bool compactMode = false)
    {
        EnsureScope(backendUrl);
        var world = player.HomeWorld;
        var localName = player.IsLoaded ? player.CharacterName : "";
        var localWorld = player.IsLoaded && world.IsValid ? world.Value.Name.ToString() : "";
        Collect(); CollectSync();
        if (syncWork == null && affectedRefresh != null && session != null) RefreshAffected(affectedRefresh);
        PumpForeground();
        var busy = work != null;
        if (!busy && session != null && session.AccessExpiresAt < DateTimeOffset.UtcNow.AddMinutes(5) && saved != null && DateTimeOffset.UtcNow >= nextRenewAttempt) Resume(saved);
        if (refreshNeeded && work == null && syncWork == null && session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow)
        { refreshNeeded = false; Refresh(); }
        busy = work != null;
        if (compactMode && session == null) { Wrapped("Open /amorrp to sign in and select your character."); return; }
        if (!compactMode && session == null) { DrawLogin(busy, localName, localWorld, world.RowId); DrawAnnouncementDialogs(); return; }
        var pending = configuration.PendingCommands.FirstOrDefault(x => x.Origin == origin?.AbsoluteUri && x.CharacterId == session?.Character.Id);
        if (session == null || session.AccessExpiresAt <= DateTimeOffset.UtcNow) return;
        if (session.Character.DisplayName != localName || session.Character.HomeWorldName != localWorld)
        { ImGui.TextWrapped("The verified profile does not match the character currently in game. Wait for XIVAuth to refresh it, then resume login. Existing assets are preserved."); return; }
        if (work == null && syncWork == null && pending == null && DateTimeOffset.UtcNow >= nextRefresh && !refreshNeeded) Refresh();
        TickTrades(); icons.Draw(); logos.Draw();
        DrawCommunitySelection();
        var feedbackHeight = ActionFeedbackHeight(pending);
        ImGui.BeginChild("ProductContent", new System.Numerics.Vector2(0, feedbackHeight > 0 ? -feedbackHeight - ImGui.GetStyle().ItemSpacing.Y : 0), false);
        if (compactMode) { DrawConsumables(); ImGui.EndChild(); DrawActionFeedback(pending); return; }
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Header, new System.Numerics.Vector4(0.33f, 0.23f, 0.49f, 0.85f));
        ImGui.BeginChild("MainNavigation", new System.Numerics.Vector2(ImGui.GetFrameHeight() + ImGui.CalcTextSize("Inventory").X + ImGui.GetStyle().ItemSpacing.X * 3 + ImGui.GetStyle().WindowPadding.X * 2, 0), true);
        foreach (var section in Enum.GetValues<MainSection>())
        {
            var icon = section switch
            {
                MainSection.Home => FontAwesomeIcon.Home,
                MainSection.Inventory => FontAwesomeIcon.Boxes,
                MainSection.Crafting => FontAwesomeIcon.Hammer,
                MainSection.Groups => FontAwesomeIcon.Sitemap,
                MainSection.Members => FontAwesomeIcon.Users,
                _ => FontAwesomeIcon.Cog
            };
            if (ImGuiComponents.IconButton("NavIcon" + section, icon, new System.Numerics.Vector2(ImGui.GetFrameHeight()))) mainSection = section;
            if (section == MainSection.Home && UnreadAnnouncements > 0) DrawAnnouncementBadge();
            ImGui.SameLine();
            if (ImGui.Selectable(section.ToString(), mainSection == section, ImGuiSelectableFlags.None, new System.Numerics.Vector2(0, ImGui.GetFrameHeight()))) mainSection = section;
        }
        ImGui.EndChild(); ImGui.PopStyleColor(); ImGui.SameLine();
        ImGui.BeginChild("SectionContent", new System.Numerics.Vector2(0, 0), false);
        ImGui.PushItemWidth(-1);
        switch (mainSection)
        {
            case MainSection.Home: DrawHome(); break;
            case MainSection.Inventory: DrawInventoryTabs(); break;
            case MainSection.Crafting: DrawCraftingTabs(); break;
            case MainSection.Groups: DrawCommunityGroups(); break;
            case MainSection.Noticeboard: DrawNoticeboard(); break;
            case MainSection.Members: DrawMembers(); break;
            case MainSection.Settings: DrawSettings(); break;
        }
        ImGui.PopItemWidth(); ImGui.EndChild();
        ImGui.EndChild();
        DrawActionFeedback(pending); files.Draw(); DrawAnnouncementDialogs();
    }
    public void EnsureScope(string backendUrl)
    {
        var world = player.HomeWorld;
        var localName = player.IsLoaded ? player.CharacterName : "";
        var localWorld = player.IsLoaded && world.IsValid ? world.Value.Name.ToString() : "";
        var nextScope = backendUrl + "|" + localName + "|" + localWorld;
        if (scope != nextScope)
        {
            Clear(); scope = nextScope;
            if (BackendAddress.TryParse(backendUrl, out var parsed)) origin = parsed;
            saved = configuration.SavedSessions.LastOrDefault(x => x.Origin == origin?.AbsoluteUri && x.DisplayName == localName && x.HomeWorldName == localWorld);
            if (saved != null && localName.Length > 0) Resume(saved);
        }
    }
    private string loginFeedback = "", loginDiagnostic = "";
    private bool waitingForBrowser;
    private LoginAttemptCreated? loginAttempt;
    private string loginPairingCode = "";
    private bool loginPairingReady;
    private Guid loginExchangeKey;
    private void ResetLogin()
    { loginAttempt = null; loginPairingCode = ""; loginPairingReady = waitingForBrowser = false; loginExchangeKey = default; }
    private void BeginLogin(string localName, string localWorld, uint worldId)
    {
        loginFeedback = loginDiagnostic = ""; ResetLogin();
        var body = new LoginStart(localName, worldId.ToString(System.Globalization.CultureInfo.InvariantCulture), localWorld, saved?.CharacterId);
        var uri = origin!;
        Start(async ct => {
            var created = await client.StartAsync(uri, body, ct);
            return () => {
                if (!XivAuthAuthorization.IsTrusted(created.AuthorizationUrl)) throw new InvalidOperationException("The server returned an untrusted login page.");
                try { Process.Start(new ProcessStartInfo(created.AuthorizationUrl) { UseShellExecute = true }); }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
                { loginFeedback = "Could not open your browser. Set a default browser and try logging in again."; return; }
                loginAttempt = created; loginExchangeKey = Guid.CreateVersion7(); waitingForBrowser = true;
                Start(async token => { await client.WaitForVerificationAsync(uri, created, token); return () => { waitingForBrowser = false; loginPairingReady = true; }; }, "Waiting for XIVAuth…");
            };
        }, "Signing in…");
    }
    private void DrawLogin(bool busy, string localName, string localWorld, uint worldId)
    {
        if (session == null)
        {
            if (busy)
            {
                if (ImGui.Button("Cancel login")) { ClearWork(); loginFeedback = loginDiagnostic = ""; ResetLogin(); }
                Wrapped(waitingForBrowser ? "Complete sign-in in your browser." : "Signing in…");
            }
            else
            {
                if (loginPairingReady && loginAttempt != null)
                {
                    if (loginAttempt.ExpiresAt <= DateTimeOffset.UtcNow) { ResetLogin(); loginFeedback = "Login attempt expired. Start again."; }
                    else
                    {
                        ImGui.InputText("Pairing code", ref loginPairingCode, 64);
                        Wrapped("Paste the code from your verified browser page into this plugin only.");
                        ImGui.BeginDisabled(string.IsNullOrWhiteSpace(loginPairingCode));
                        if (ImGui.Button("Finish login"))
                        {
                            var attempt = loginAttempt; var code = loginPairingCode; var key = loginExchangeKey; var uri = origin!;
                            Start(async ct => { var tokens = await client.ExchangeLoginAsync(uri, attempt, code, key, ct); return () => Accept(tokens); }, "Pairing this plugin…");
                        }
                        ImGui.EndDisabled(); ImGui.SameLine();
                        if (ImGui.Button("Cancel login")) { ClearWork(); ResetLogin(); loginFeedback = ""; }
                        DrawLoginFeedback();
                        return;
                    }
                }
                ImGui.BeginDisabled(origin == null || localName.Length == 0 || localWorld.Length == 0);
                if (ImGui.Button("Log in with XIVAuth")) BeginLogin(localName, localWorld, worldId);
                ImGui.EndDisabled();
                if (localName.Length == 0 || localWorld.Length == 0) Wrapped("Log into your FFXIV character first.");
                DrawLoginFeedback();
            }
            return;
        }
        ImGui.TextUnformatted($"{session.Character.DisplayName} @ {session.Character.HomeWorldName}");
        ImGui.BeginDisabled(busy);
        if (ImGui.Button("Sign out this device")) Mutate("DELETE", "api/v2/auth/sessions/current", null, null, signOut: true);
        ImGui.EndDisabled();
    }

    public void Tick()
    {
        ExpireFeedback();
        if (session != null && (!player.IsLoaded || !player.HomeWorld.IsValid || session.Character.DisplayName != player.CharacterName || session.Character.HomeWorldName != player.HomeWorld.Value.Name.ToString()))
        { Clear(); return; }
        if (scope.Length > 0 && !scope.StartsWith(ProductServer.Origin + "|", StringComparison.Ordinal)) { Clear(); return; }
        Collect(); CollectSync(); PumpForeground();
        if (session != null && work == null && saved != null && session.AccessExpiresAt < DateTimeOffset.UtcNow.AddMinutes(5) && DateTimeOffset.UtcNow >= nextRenewAttempt) Resume(saved);
        if (refreshNeeded && work == null && syncWork == null && session != null && session.AccessExpiresAt > DateTimeOffset.UtcNow) { refreshNeeded = false; Refresh(); }
        PumpContextTargetResolution(); TickTradeInbox(); TickTrades(); TickAnnouncements(); TickFavoriteEvents();
    }
    private bool Owner => community != null && session?.Character.Id == community.OwnerCharacterId;
    private bool CanManageGameplay => Owner || community?.MyCapabilities.Contains("members.gameplay.manage") == true;
    private bool CanGeneralModerate => Owner || community?.MyCapabilities.Contains("community.moderate") == true;
    private bool CanAnnounce => Owner || community?.MyCapabilities.Contains("announcements.manage") == true;
    private bool CanCraftPotions => Owner || community?.MyCapabilities.Contains("items.potion.create") == true;
    private bool CanCraftCooking => Owner || community?.MyCapabilities.Contains("items.cooking.create") == true;
    private bool CanDefineCooking => Owner || community?.MyCapabilities.Contains("items.cooking.define") == true;
    private bool CanDefinePotions => Owner || community?.MyCapabilities.Contains("items.potion.define") == true;
    private bool CanCoins => Owner || community?.MyCapabilities.Contains("currency.manage") == true;
    private void Resume(SavedSession entry)
    {
        try {
            nextRenewAttempt = DateTimeOffset.UtcNow.AddMinutes(1);
            saved = entry; selected = saved.SelectedCommunityId; var tokens = saved.Read();
            saved.PendingRefreshKey ??= Guid.CreateVersion7(); save(); var key = saved.PendingRefreshKey.Value;
            Start(async ct => { var renewed = await client.RenewAsync(origin!, tokens, key, ct); return () => Accept(renewed); }, "Renewing saved login…");
        } catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        { status = "Saved login could not be restored. Log in again with XIVAuth."; loginFeedback = status; }
    }
    private void Accept(SessionTokens tokens)
    {
        session = tokens; loginFeedback = loginDiagnostic = ""; ResetLogin();
        saved = configuration.SavedSessions.SingleOrDefault(x => x.Origin == origin!.AbsoluteUri && x.CharacterId == tokens.Character.Id);
        if (saved == null) { saved = new SavedSession { Origin = origin!.AbsoluteUri, CharacterId = tokens.Character.Id }; configuration.SavedSessions.Add(saved); }
        saved.DisplayName = tokens.Character.DisplayName; saved.HomeWorldName = tokens.Character.HomeWorldName;
        saved.ProtectedTokens = SavedSession.Protect(tokens); saved.PendingRefreshKey = null; save();
        status = "Character login saved. Loading communities…"; refreshNeeded = true;
    }
    private void Start(Func<CancellationToken, Task<Action>> run, string message)
    {
        if (work != null) { if (reads.Count >= 4) reads.Clear(); reads.Enqueue((run, message)); return; }
        status = message; var token = lifetime.Token; var epoch = viewGeneration;
        work = Task.Run(async () => { var apply = await run(token); return (Action)(() => { if (viewGeneration == epoch) apply(); }); }, token);
    }
    private void PumpForeground()
    {
        if (work != null) return;
        if (session == null || origin == null) { queuedCommand = null; reads.Clear(); return; }
        if (queuedCommand != null) { var pending = queuedCommand; queuedCommand = null; ExecutePending(pending, queuedSignOut, queuedRecovery); }
        else if (reads.TryDequeue(out var read)) Start(read.Run, read.Message);
    }
    private void StartSync(Func<CancellationToken, Task<Action>> run)
    {
        if (syncWork != null) return;
        syncView = viewGeneration; syncData = dataGeneration; var token = lifetime.Token;
        syncWork = Task.Run(() => run(token), token);
    }
    private void CollectSync()
    {
        if (syncWork is not { IsCompleted: true }) return;
        var finished = syncWork; syncWork = null;
        if (syncView != viewGeneration || syncData != dataGeneration) { _ = finished.Exception; refreshNeeded = affectedRefresh == null; if (affectedRefresh != null) RefreshAffected(affectedRefresh); return; }
        if (finished.IsCompletedSuccessfully) { connectionError = connectionDiagnostic = ""; syncFailures = 0; finished.Result(); }
        else
        {
            var error = finished.Exception?.GetBaseException();
            syncFailures = Math.Min(syncFailures + 1, 5);
            nextRefresh = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 5 * Math.Pow(2, syncFailures - 1)));
            if (error is InvalidOperationException) serviceCompatible = false;
            status = error is FoundationApiException or InvalidOperationException ? error.Message : "Reconnecting to Amor RP… Your last confirmed information and unfinished edits have been kept.";
            connectionError = status; connectionDiagnostic = UserFeedback.Details(error);
            if (error is FoundationApiException api && api.Status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.NotFound)
            { ClearCommunityViews(); if (api.Status == System.Net.HttpStatusCode.Unauthorized) session = null; else refreshNeeded = true; }
        }
    }
    private void Collect()
    {
        if (work is not { IsCompleted: true }) return;
        var finished = work; work = null;
        if (finished.IsCompletedSuccessfully) { currentCommand = null; status = "Ready."; finished.Result(); }
        else
        {
            var failure = finished.Exception?.GetBaseException();
            if (session == null)
            {
                waitingForBrowser = false;
                if (failure is FoundationApiException { Code: "login_pairing_locked" or "login_not_ready" or "login_expired" }) ResetLogin();
                loginFeedback = failure is FoundationApiException or InvalidOperationException ? failure.Message : "Could not reach Amor RP. Wait a moment and try logging in again.";
                loginDiagnostic = UserFeedback.Details(failure);
            }
            nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1);
            status = failure is FoundationApiException or InvalidOperationException ? failure.Message : "Could not reach Amor RP. Your changes have been kept; check the action result before trying again.";
            if (currentCommand == null && session != null) { connectionError = status; connectionDiagnostic = UserFeedback.Details(failure); }
            if (currentCommand != null)
            {
                actionResult = status;
                actionDiagnostic = UserFeedback.Details(failure) + "\nAction ID: " + currentCommand.Key;
                if (currentCommand.Read().Path.EndsWith("/announcements", StringComparison.Ordinal))
                    announcementPostError = status + (failure is FoundationApiException { OutcomeUncertain: false } ? "" : " Close this window to recover the saved action.");
                NoteAuthorizationFailure(currentCommand.Read(), failure);
            }
            if (!groupLogos.Matches(requestedGroupLogo)) requestedGroupLogo = "";
            NoteAuthorizationReadFailure(failure);
            NoteTradeFailure(currentCommand?.Read(), failure);
            if (failure is FoundationApiException api)
            {
                var commandFailed = currentCommand != null;
                if (api.OutcomeUncertain && commandFailed && feedback.Latest("action") is { } message)
                    message.Text += " This may already have been saved. Use Check action result after reconnecting before trying again.";
                if (!api.OutcomeUncertain && (int)api.Status is >= 400 and < 500 && api.Status != System.Net.HttpStatusCode.TooManyRequests && api.Code != "operation_replay_expired" && currentCommand != null)
                { configuration.PendingCommands.Remove(currentCommand); save(); currentCommand = null; refreshNeeded = session != null; }
                if (api.Status == System.Net.HttpStatusCode.NotFound && !commandFailed && community != null) refreshNeeded = true;
                if (api.Status == System.Net.HttpStatusCode.Unauthorized) { ClearWork(); ClearCommunityViews(); session = null; if (saved != null) { saved.PendingRefreshKey = null; save(); } }
            }
            currentCommand = null;
        }
    }
    private void Mutate(string method, string path, object? body, string? etag = null, bool signOut = false)
    {
        if (session == null || origin == null || WriteBlocked) return;
        dataGeneration++;
        var command = new CommandRequest(method, path, body == null ? null : JsonSerializer.Serialize(body, FoundationClient.Json), etag);
        var pending = new PendingCommand { Origin = origin.AbsoluteUri, CharacterId = session.Character.Id, Key = Guid.CreateVersion7(), ProtectedRequest = PendingCommand.Protect(command) };
        configuration.PendingCommands.Add(pending); save(); ExecutePending(pending, signOut);
    }
    private void ExecutePending(PendingCommand pending, bool signOut, bool recovering = false)
    {
        if (work != null) { queuedCommand = pending; queuedSignOut = signOut; queuedRecovery = recovering; return; }
        var wireRequest = pending.Read(); var request = wireRequest.CurrentShape(); currentCommand = pending; var token = session!.AccessToken;
        Start(async ct => {
            var response = await client.CommandWithMetadataAsync(origin!, wireRequest, pending.Key, token, ct);
            return () => {
                configuration.PendingCommands.Remove(pending); save(); status = UserFeedback.Success(request); actionResult = status; actionDiagnostic = "Operation key: " + pending.Key;
                ApplyFormSuccess(request, response.Json);
                ApplyHomeSuccess(request, response.Json);
                ApplyNestedGroupSuccess(request, response.Json);
                ApplyInventorySuccess(request, response, recovering);
                ApplyTradeSuccess(request, response.Json);
                if (request.Path.EndsWith("/currency/adjustments", StringComparison.Ordinal))
                {
                    var balance = JsonSerializer.Deserialize<CommandResult<Balance>>(response.Json, FoundationClient.Json)!.Result;
                    if (memberSelection == balance.CharacterId && community?.Currency.Id == balance.CurrencyId)
                    { selectedMemberBalance = balance; balanceFor = balance.CharacterId; }
                }
                if (request.Path.Contains("/members/", StringComparison.Ordinal) && !request.Path.Contains("/holdings", StringComparison.Ordinal) && !request.Path.EndsWith("/balances", StringComparison.Ordinal))
                {
                    var updated = JsonSerializer.Deserialize<CommandResult<Member>>(response.Json, FoundationClient.Json)!;
                    members = members.Select(x => x.Character.Id == updated.Result.Character.Id ? updated.Result : x).ToArray();
                    memberSearchResults = memberSearchResults.Select(x => x.Character.Id == updated.Result.Character.Id ? updated.Result : x).ToArray();
                    if (memberSelection == updated.Result.Character.Id) memberDraftEtag = updated.Result.Etag; AcceptAuthorizationSave(request, updated);
                    AcceptMemberDescriptionSave(request, updated.Result);
                }
                if (request.Path.EndsWith("/policies", StringComparison.Ordinal)) AcceptPolicySave(request, JsonSerializer.Deserialize<CommandResult<CommunityPolicies>>(response.Json, FoundationClient.Json)!.Result);
                if (request.Method == "PATCH" && request.Path.EndsWith("/currency", StringComparison.Ordinal)) editCurrencyVersion = JsonSerializer.Deserialize<CommandResult<AmorRP.Contracts.Currency.Currency>>(response.Json, FoundationClient.Json)!.Result.Version;
                if (request.Method == "PATCH" && request.Path.Split('/').Length == 4) editCommunityVersion = JsonSerializer.Deserialize<CommandResult<Community>>(response.Json, FoundationClient.Json)!.Result.Version;
                if (request.Method == "POST" && request.Path.EndsWith("/invitations", StringComparison.Ordinal))
                    createdCode = JsonSerializer.Deserialize<CommandResult<InvitationCreated>>(response.Json, FoundationClient.Json)!.Result.InvitationCode;
                if (signOut || request.Path == "api/v2/auth/sessions/current") { ForgetCurrent(); status = "This device session was revoked. Sign in to reconnect."; }
                else { dataGeneration++; RefreshAffected(request); }
            };
        }, "Saving action…");
    }
    private void Refresh()
    {
        var uri = origin!; var token = session!.AccessToken; var selectedId = selected;
        var actorId = session.Character.Id; var includeInactive = showInactive; var selectedMemberId = memberSelection; var rosterCount = members.Length; var inspector = memberInventoryFor; var inspectorCount = memberHoldings.Length;
        nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1);
        StartSync(async ct => {
            var capability = await client.GetAsync<CapabilitiesResponse>(uri, "api/v2/capabilities", token, ct);
            if (capability.ApiVersion != "2" || !Version.TryParse(capability.ServerVersion, out var serverVersion) || serverVersion < new Version(0, 0, 6, 18)
                || !Version.TryParse(capability.MinimumClientVersion, out var minimumClient) || minimumClient > typeof(CommunitiesPanel).Assembly.GetName().Version)
                throw new InvalidOperationException("Amor RP needs an update. Check for a plugin update; the server may also need updating.");
            if (capability.Maintenance) throw new InvalidOperationException(capability.Message);
            var communityList = new List<Community>(); string? communityCursor = null;
            do { var page = await client.GetAsync<CommunityPage>(uri, "api/v2/communities" + (communityCursor == null ? "" : "?cursor=" + Uri.EscapeDataString(communityCursor)), token, ct); communityList.AddRange(page.Items); communityCursor = page.NextCursor; } while (communityCursor != null);
            var list = new CommunityPage(communityList.ToArray(), null, "read", null);
            var active = list.Items.FirstOrDefault(x => x.Id == selectedId) ?? list.Items.FirstOrDefault();
            MemberPage? roster = null; InvitationPage? invites = null; CommunityPolicies? limits = null; OwnershipTransfer? proposal = null;
            HoldingPage? inspectorState = null;
            if (active != null)
            {
                var isOwner = active.OwnerCharacterId == actorId;
                roster = await client.GetAsync<MemberPage>(uri, $"api/v2/communities/{active.Id}/members" + (isOwner && includeInactive ? "?includeDormant=true" : ""), token, ct);
                var allMembers = roster.Items.ToList();
                while (roster.NextCursor != null && allMembers.Count < rosterCount)
                { var page = await client.GetAsync<MemberPage>(uri, $"api/v2/communities/{active.Id}/members?cursor=" + Uri.EscapeDataString(roster.NextCursor) + (isOwner && includeInactive ? "&includeDormant=true" : ""), token, ct); allMembers.AddRange(page.Items); roster = page; }
                roster = roster with { Items = allMembers.ToArray() };
                if (inspector.HasValue && (isOwner || active.MyCapabilities.Contains("inventory.remove")))
                {
                    try {
                        var path = $"api/v2/communities/{active.Id}/members/{inspector}/holdings";
                        inspectorState = await client.GetAsync<HoldingPage>(uri, path, token, ct);
                        var rows = inspectorState.Items.ToList();
                        while (inspectorState.NextCursor != null && rows.Count < inspectorCount)
                        { var page = await client.GetAsync<HoldingPage>(uri, path + "?cursor=" + Uri.EscapeDataString(inspectorState.NextCursor), token, ct); rows.AddRange(page.Items); inspectorState = page; }
                        inspectorState = inspectorState with { Items = rows.ToArray() };
                    } catch (FoundationApiException ex) when (ex.Status is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden) { inspectorState = new([], null, "0"); }
                }
                if (selectedMemberId.HasValue && !roster.Items.Any(x => x.Character.Id == selectedMemberId))
                {
                    try {
                        var current = await client.GetAsync<Member>(uri, $"api/v2/communities/{active.Id}/members/{selectedMemberId}", token, ct);
                        if (current.Status == "active" || (isOwner && includeInactive)) roster = roster with { Items = [..roster.Items, current] };
                    } catch (FoundationApiException e) when (e.Status == System.Net.HttpStatusCode.NotFound) { }
                }
                limits = await client.GetAsync<CommunityPolicies>(uri, $"api/v2/communities/{active.Id}/policies", token, ct);
                if (isOwner) invites = await client.GetAsync<InvitationPage>(uri, $"api/v2/communities/{active.Id}/invitations", token, ct);
                if (active.PendingOwnershipTransferId.HasValue && (isOwner || roster.Items.Any(x => x.Character.Id == actorId)))
                {
                    try { proposal = await client.GetAsync<OwnershipTransfer>(uri, $"api/v2/communities/{active.Id}/ownership-transfers/{active.PendingOwnershipTransferId}", token, ct); }
                    catch (FoundationApiException e) when (e.Status == System.Net.HttpStatusCode.NotFound) { }
                }
            }
            var groupHub = active == null ? new AmorRP.Contracts.Groups.GroupHub([], []) : await client.GetAsync<AmorRP.Contracts.Groups.GroupHub>(uri, $"api/v2/communities/{active.Id}/group-hub", token, ct);
            var favorites = await client.GetAsync<AmorRP.Contracts.Groups.FavoriteEvents>(uri, "api/v2/me/favorite-group-events", token, ct);
            var nestedGroups = active == null ? [] : await LoadCommunityGroupsAsync(uri, token, active.Id, ct);
            var inventoryState = active == null ? null : await LoadInventoryAsync(uri, token, active.Id, ct);
            byte[]? iconBytes = null;
            if (active?.Currency.IconAssetId != null && !icons.Matches(uri.AbsoluteUri + actorId + active.Id + active.Currency.IconAssetId))
                iconBytes = await client.GetIconAsync(uri, $"api/v2/communities/{active.Id}/currency/icon", token, ct);
            byte[]? logoBytes = null;
            if (active?.LogoAssetId != null && !logos.Matches(uri.AbsoluteUri + actorId + active.Id + active.LogoAssetId))
                logoBytes = await client.GetImageAsync(uri, $"api/v2/communities/{active.Id}/logo", token, 1048576, ct);
            return () => {
                var changedCommunity = community?.Id != active?.Id;
                serviceCompatible = true; serviceLimits = capability.Limits;
                if (lastRefresh == default && !initialAllowancesEdited) { initialPoints = capability.Limits.DefaultPotionPoints; initialLetters = capability.Limits.DefaultLettersPerWeek; }
                if (changedCommunity) { ClearCommunityViews(); ClearMemberSearch(); }
                communities = list.Items; community = active; selected = active?.Id; members = roster?.Items ?? []; invitations = invites?.Items ?? [];
                membersCursor = roster?.NextCursor; invitationsCursor = invites?.NextCursor;
                policies = limits; transfer = proposal;
                if (saved != null) { saved.SelectedCommunityId = selected; save(); }
                if (limits != null && (changedCommunity || !policyDraftLoaded)) LoadActivePolicy(limits);
                if (!members.Concat(memberSearchResults).Any(x => x.Character.Id == memberSelection)) memberSelection = null;
                if (!(active?.OwnerCharacterId == actorId || active?.MyCapabilities.Contains("inventory.remove") == true)) { memberHoldings = []; memberInventoryFor = null; memberHoldingSelection = null; }
                if (inspectorState != null && memberInventoryFor == inspector) { memberHoldings = inspectorState.Items; memberHoldingCursor = inspectorState.NextCursor; }
                AcceptInventoryState(inventoryState); AcceptCommunityGroups(nestedGroups); hub = groupHub; favoriteEvents = favorites;
                if (active != null) icons.Update(uri.AbsoluteUri + actorId + active.Id + active.Currency.IconAssetId, iconBytes, active.Currency.IconAssetId != null);
                if (active != null) logos.Update(uri.AbsoluteUri + actorId + active.Id + active.LogoAssetId, logoBytes, active.LogoAssetId != null);
                lastRefresh = DateTimeOffset.UtcNow; nextRefresh = lastRefresh.AddMinutes(1); status = "Data refreshed.";
            };
        });
    }
    private void ForgetCurrent()
    { ClearAnnouncementNotifications(); ClearWork(); if (saved != null) configuration.SavedSessions.Remove(saved); save(); session = null; saved = null; community = null; communities = []; members = []; ClearCommunityViews(); }
    private void ClearWork()
    {
        if (pendingAuthorizationRead.HasValue) { authorizationResult = "Permission check cancelled. Reload saved permissions to try again."; pendingAuthorizationRead = null; }
        if (pendingPotionAccessRead) { potionAccessResult = "Permission check cancelled. Try checking again."; pendingPotionAccessRead = false; }
        viewGeneration++; reads.Clear(); queuedCommand = null;
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        if (syncWork != null) _ = syncWork.ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default); syncWork = null;
        if (work != null) _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        work = null; currentCommand = null;
    }
    private void Clear()
    { ClearMemberSearch(); personalNote = savedPersonalNote = noteKey = ""; ClearAnnouncementNotifications(); ClearTradeInbox(); ResetCommunityForms(); ResetLogin(); loginFeedback = loginDiagnostic = ""; syncFailures = 0; scope = ""; serviceCompatible = false; affectedRefresh = null; ClearWork(); origin = null; session = null; saved = null; selected = null; community = null; communities = []; members = []; invitations = []; transfer = null; refreshNeeded = false; createdCode = ""; contextTarget = null; contextLookup = null; ClearTransientFeedback(); connectionError = connectionDiagnostic = ""; lastRefresh = default; nextRefresh = default; ClearCommunityViews(); }
    private static void Wrapped(string message) { ImGui.PushTextWrapPos(); ImGui.TextUnformatted(message); ImGui.PopTextWrapPos(); }
    public void Dispose() { ClearMemberSearch(); personalNote = savedPersonalNote = noteKey = ""; ClearAnnouncementNotifications(); ClearTradeInbox(); ClearWork(); lifetime.Dispose(); icons.Dispose(); logos.Dispose(); groupLogos.Dispose(); client.Dispose(); }
}
