///----------------------------------------------------------------------------
///   Module:       VK Video Live Service
///   Author:       NuboHeimer (https://live.vkvideo.ru/nuboheimer)
///   Email:        nuboheimer@yandex.ru
///   Help:         https://t.me/nuboheimersb/29
///   Help:         https://vk.com/topic-236253647_57236856
///----------------------------------------------------------------------------

///   Version:      5.0.0_dev.5
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;

public class CPHInline
{
    private const string VkLiveTodaysViewersKey = "VkLiveTodaysViewers";
    private const string VkLivePreviousPresentViewersKey = "VkLivePreviousPresentViewers";
    private const string VkLiveLastRandomViewerKey = "VkLiveLastRandomViewer";

    private const string VkLiveAuthAccessTokenKey = "VkLiveAuthAccessToken";
    private const string VkLiveAuthRefreshTokenKey = "VkLiveAuthRefreshToken";
    private const string VkLiveAuthExpiresAtUtcKey = "VkLiveAuthExpiresAtUtc";

    private const string VkLiveAuthClientIdKey = "VkLiveAuthClientId";
    private const string VkLiveAuthClientSecretKey = "VkLiveAuthClientSecret";
    private const string VkLiveAuthRedirectUriKey = "VkLiveAuthRedirectUri";
    private const string VkLiveAuthUserNickKey = "VkLiveAuthUserNick";
    private const string VkLiveAuthUserAvatarUrlKey = "VkLiveAuthUserAvatarUrl";

    private const string VkLiveRewardsCacheKey = "VkLiveRewardsCache";
    private const string VkLiveMiniChatServiceKey = "VKVideoLive";

    private const string VkLiveViewerFirstTodayEvent = "VKVideoLive_ViewerFirstToday";
    private const string VkLiveViewerJoinedEvent = "VKVideoLive_ViewerJoined";
    private const string VkLiveViewerLeftEvent = "VKVideoLive_ViewerLeft";

    private readonly HttpClient _client = new();
    private VKVideoLiveApiService _vkVideoLiveApiService;
    private VkOAuthService _vkAuthService;
    public void Init()
    {
        _vkVideoLiveApiService = new VKVideoLiveApiService(_client);
        _vkAuthService = new VkOAuthService(_client);

        if (CPH.GetGlobalVar<HashSet<string>>(VkLiveTodaysViewersKey, true) == null)
            CPH.SetGlobalVar(VkLiveTodaysViewersKey, new HashSet<string>(), true);
        if (CPH.GetGlobalVar<HashSet<string>>(VkLivePreviousPresentViewersKey, true) == null)
            CPH.SetGlobalVar(VkLivePreviousPresentViewersKey, new HashSet<string>(), true);

        var vkLiveCategory = new[] { "VK Video Live" };
        CPH.RegisterCustomTrigger("Present Viewers (VkLive)", "VKVideoLive_PresentViewers", vkLiveCategory);
        CPH.RegisterCustomTrigger("Viewer First Today (VkLive)", VkLiveViewerFirstTodayEvent, vkLiveCategory);
        CPH.RegisterCustomTrigger("Viewer Joined (VkLive)", VkLiveViewerJoinedEvent, vkLiveCategory);
        CPH.RegisterCustomTrigger("Viewer Left (VkLive)", VkLiveViewerLeftEvent, vkLiveCategory);
    }

    public bool ClearTodaysViewers()
    {
        CPH.SetGlobalVar(VkLiveTodaysViewersKey, new HashSet<string>(), true);
        return true;
    }

    public bool ClearPreviousPresentViewers()
    {
        CPH.SetGlobalVar(VkLivePreviousPresentViewersKey, new HashSet<string>(), true);
        return true;
    }

    public bool OnReward()
    {
        return OnRewardInternal(CPH);
    }

    private bool OnRewardInternal(IInlineInvokeProxy cph)
    {
        try
        {
            if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
                return false;

            if (!cph.TryGetArg("rewardName", out object rewardNameObj) || rewardNameObj == null)
            {
                cph.LogWarn("[VKVideoLive reward manager] Аргумент rewardName не передан.");
                return false;
            }

            string channelName = channelNameObj.ToString();
            string rewardName = rewardNameObj.ToString();

            if (string.IsNullOrWhiteSpace(channelName))
            {
                cph.LogWarn("[VKVideoLive reward manager] Значение channel_name пустое.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(rewardName))
            {
                cph.LogWarn("[VKVideoLive reward manager] Значение rewardName пустое.");
                return false;
            }

            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            string rewardId = ResolveRewardIdByName(cph, channelName, rewardName, authState.AccessToken);
            if (string.IsNullOrEmpty(rewardId))
            {
                cph.LogWarn("[VKVideoLive reward manager] Награда с именем \"" + rewardName + "\" не найдена ни в кэше, ни после обновления manage_info.");
                return false;
            }

            _vkVideoLiveApiService.EnableReward(channelName, rewardId, authState.AccessToken);
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive reward manager] Ошибка при включении награды по имени, " + e.Message);
            return false;
        }
    }

    private string ResolveRewardIdByName(IInlineInvokeProxy cph, string channelName, string rewardName, string accessToken)
    {
        var cache = cph.GetGlobalVar<Dictionary<string, string>>(VkLiveRewardsCacheKey, true)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string cacheKey = channelName + "::" + rewardName;

        if (cache.TryGetValue(cacheKey, out string rewardId) && !string.IsNullOrEmpty(rewardId))
            return rewardId;

        if (cache.TryGetValue(rewardName, out rewardId) && !string.IsNullOrEmpty(rewardId))
            return rewardId;

        RefreshRewardsCache(cph, channelName, accessToken);

        cache = cph.GetGlobalVar<Dictionary<string, string>>(VkLiveRewardsCacheKey, true)
               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (cache.TryGetValue(cacheKey, out rewardId) && !string.IsNullOrEmpty(rewardId))
            return rewardId;

        return null;
    }

    private List<VKVideoLiveApiService.ChannelPointReward> RefreshRewardsCache(
        IInlineInvokeProxy cph,
        string channelName,
        string accessToken)
    {
        var rewards = new List<VKVideoLiveApiService.ChannelPointReward>();
        var channelPoints = _vkVideoLiveApiService.GetChannelPoints(channelName, accessToken);
        var updatedCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (channelPoints?.Rewards != null)
        {
            foreach (var reward in channelPoints.Rewards)
            {
                rewards.Add(reward);

                if (!string.IsNullOrWhiteSpace(reward.Name) && !string.IsNullOrWhiteSpace(reward.Id))
                    updatedCache[channelName + "::" + reward.Name] = reward.Id;
            }
        }

        cph.SetGlobalVar(VkLiveRewardsCacheKey, updatedCache, true);
        return rewards;
    }

    public bool GetRewards()
    {
        return GetRewardsInternal(CPH);
    }

    private bool GetRewardsInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
            return false;

        string channelName = channelNameObj.ToString();
        if (string.IsNullOrWhiteSpace(channelName))
        {
            cph.LogWarn("[VKVideoLive get rewards] Значение channel_name пустое.");
            return false;
        }

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            var rewards = RefreshRewardsCache(cph, channelName, authState.AccessToken);
            var rewardNames = rewards
                .Where(reward => !string.IsNullOrWhiteSpace(reward.Name))
                .Select(reward => reward.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            cph.SetArgument("rewardNames", rewardNames);
            cph.SetArgument("rewardsCount", rewardNames.Count);
            cph.SetArgument("minichat.Service", VkLiveMiniChatServiceKey);

            cph.LogInfo(
                "[VKVideoLive get rewards] Канал: '"
                + channelName
                + "', получено наград: "
                + rewardNames.Count
                + ".");

            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive get rewards] Ошибка при получении списка наград, " + e.Message);
            return false;
        }
    }

    public bool OffReward()
    {
        return OffRewardInternal(CPH);
    }

    private bool OffRewardInternal(IInlineInvokeProxy cph)
    {
        try
        {
            if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
                return false;

            if (!cph.TryGetArg("rewardName", out object rewardNameObj) || rewardNameObj == null)
            {
                cph.LogWarn("[VKVideoLive reward manager] Аргумент rewardName не передан.");
                return false;
            }

            string channelName = channelNameObj.ToString();
            string rewardName = rewardNameObj.ToString();

            if (string.IsNullOrWhiteSpace(channelName))
            {
                cph.LogWarn("[VKVideoLive reward manager] Значение channel_name пустое.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(rewardName))
            {
                cph.LogWarn("[VKVideoLive reward manager] Значение rewardName пустое.");
                return false;
            }

            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            string rewardId = ResolveRewardIdByName(cph, channelName, rewardName, authState.AccessToken);
            if (string.IsNullOrEmpty(rewardId))
            {
                cph.LogWarn("[VKVideoLive reward manager] Награда с именем \"" + rewardName + "\" не найдена ни в кэше, ни после обновления manage_info.");
                return false;
            }

            _vkVideoLiveApiService.DisableReward(channelName, rewardId, authState.AccessToken);
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive reward manager] Ошибка при выключении награды по имени, " + e.Message);
            return false;
        }
    }

    public bool ActivateReward()
    {
        return ActivateRewardInternal(CPH);
    }

    private bool ActivateRewardInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
            return false;

        if (!cph.TryGetArg("rewardName", out object rewardNameObj) || rewardNameObj == null)
        {
            cph.LogWarn("[VKVideoLive reward activate] Аргумент rewardName не передан.");
            return false;
        }

        string rewardTextArg = cph.TryGetArg("rewardText", out object rewardTextObj) && rewardTextObj != null
            ? rewardTextObj.ToString()
            : string.Empty;

        string channelName = channelNameObj.ToString();
        string rewardName = rewardNameObj.ToString();
        string effectiveMessage = string.IsNullOrWhiteSpace(rewardTextArg) ? rewardName : rewardTextArg;

        if (string.IsNullOrWhiteSpace(channelName))
        {
            cph.LogWarn("[VKVideoLive reward activate] Значение channel_name пустое.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(rewardName))
        {
            cph.LogWarn("[VKVideoLive reward activate] Значение rewardName пустое.");
            return false;
        }

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            string rewardId = ResolveRewardIdByName(cph, channelName, rewardName, authState.AccessToken);
            if (string.IsNullOrEmpty(rewardId))
            {
                cph.LogWarn("[VKVideoLive reward activate] Награда с именем \"" + rewardName + "\" не найдена ни в кэше, ни после обновления manage_info.");
                return false;
            }

            _vkVideoLiveApiService.ActivateReward(channelName, rewardId, authState.AccessToken, effectiveMessage);
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive reward activate] Ошибка при активации награды по имени, " + e.Message);
            return false;
        }
    }

    public bool GetRewardDemands()
    {
        return GetRewardDemandsInternal(CPH);
    }

    private bool GetRewardDemandsInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("channel_name", out string channelName) || string.IsNullOrWhiteSpace(channelName))
        {
            cph.LogWarn("[VKVideoLive reward demands] Значение channel_name пустое или не передано.");
            return false;
        }

        long limit = 200;
        if (cph.TryGetArg("limit", out long limitArg) && limitArg > 0)
            limit = Math.Min(limitArg, 200);

        long offset = 0;
        if (cph.TryGetArg("offset", out long offsetArg) && offsetArg >= 0)
            offset = offsetArg;

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            var page = _vkVideoLiveApiService.GetRewardDemands(
                channelName,
                authState.AccessToken,
                limit,
                offset);

            var demands = page?.Demands ?? new List<VKVideoLiveApiService.ChannelPointDemand>();
            ExportDemandListArguments(cph, demands);
            cph.SetArgument("demandsIsLast", page?.IsLast ?? true);
            cph.SetArgument("demandsOffset", page?.Offset ?? offset);

            cph.LogInfo(
                "[VKVideoLive reward demands] Канал: '"
                + channelName
                + "', получено запросов: "
                + demands.Count
                + ".");
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive reward demands] Ошибка при получении списка запросов наград, " + e.Message);
            return false;
        }
    }

    public bool RejectRewardDemand()
    {
        return ProcessRewardDemandStatusInternal(CPH, accept: false);
    }

    public bool AcceptRewardDemand()
    {
        return ProcessRewardDemandStatusInternal(CPH, accept: true);
    }

    private bool ProcessRewardDemandStatusInternal(IInlineInvokeProxy cph, bool accept)
    {
        string op = accept ? "accept" : "reject";

        if (!cph.TryGetArg("channel_name", out string channelName) || string.IsNullOrWhiteSpace(channelName))
        {
            cph.LogWarn("[VKVideoLive reward demand " + op + "] Значение channel_name пустое или не передано.");
            return false;
        }

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            if (!TryResolveDemandId(cph, channelName, authState.AccessToken, out long demandId))
                return false;

            if (accept)
                _vkVideoLiveApiService.AcceptRewardDemand(channelName, demandId, authState.AccessToken);
            else
                _vkVideoLiveApiService.RejectRewardDemand(channelName, demandId, authState.AccessToken);

            cph.SetArgument("demandId", demandId);
            cph.LogInfo(
                "[VKVideoLive reward demand " + op + "] Канал: '" + channelName + "', demandId: " + demandId + ".");
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive reward demand " + op + "] Ошибка: " + e.Message);
            return false;
        }
    }

    private bool TryResolveDemandId(
        IInlineInvokeProxy cph,
        string channelName,
        string accessToken,
        out long demandId)
    {
        demandId = 0;

        if (TryGetDemandIdArg(cph, out demandId))
            return true;

        if (!TryGetViewerUserIdArg(cph, out long userId))
        {
            cph.LogWarn(
                "[VKVideoLive reward demand] Нужен demandId либо userId + rewardId (или rewardName).");
            return false;
        }

        string rewardId;
        if (!TryGetRewardIdArg(cph, out rewardId))
        {
            if (!cph.TryGetArg("rewardName", out string rewardName) || string.IsNullOrWhiteSpace(rewardName))
            {
                cph.LogWarn("[VKVideoLive reward demand] Без demandId нужны rewardId (или rewardName) и userId.");
                return false;
            }

            rewardId = ResolveRewardIdByName(cph, channelName, rewardName, accessToken);
            if (string.IsNullOrEmpty(rewardId))
            {
                cph.LogWarn("[VKVideoLive reward demand] Награда \"" + rewardName + "\" не найдена.");
                return false;
            }
        }

        var matches = FindOpenDemands(cph, channelName, accessToken, userId, rewardId);
        if (matches.Count == 0)
        {
            cph.LogWarn(
                "[VKVideoLive reward demand] Открытый demand не найден (userId="
                + userId
                + ", rewardId="
                + rewardId
                + ").");
            return false;
        }

        demandId = matches.OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id).First().Id;
        return true;
    }

    private List<VKVideoLiveApiService.ChannelPointDemand> FindOpenDemands(
        IInlineInvokeProxy cph,
        string channelName,
        string accessToken,
        long userId,
        string rewardId)
    {
        var matches = new List<VKVideoLiveApiService.ChannelPointDemand>();
        long offset = 0;
        const long pageSize = 200;
        const int maxPages = 5;

        for (int pageIndex = 0; pageIndex < maxPages; pageIndex++)
        {
            var page = _vkVideoLiveApiService.GetRewardDemands(channelName, accessToken, pageSize, offset);
            var demands = page?.Demands ?? new List<VKVideoLiveApiService.ChannelPointDemand>();

            foreach (var demand in demands)
            {
                if (demand == null || demand.Id <= 0)
                    continue;
                if (demand.User?.Id != userId)
                    continue;
                if (!string.Equals(demand.Reward?.Id, rewardId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsClosedDemandStatus(demand.Status))
                    continue;

                matches.Add(demand);
            }

            if (matches.Count > 0)
                break;

            if (page == null || page.IsLast || demands.Count == 0 || demands.Count < pageSize)
                break;

            long nextOffset = offset + demands.Count;
            if (nextOffset <= offset)
                break;

            if (pageIndex == maxPages - 1)
            {
                cph.LogWarn(
                    "[VKVideoLive reward demand] Достигнут лимит страниц при поиске demand ("
                    + maxPages
                    + " x "
                    + pageSize
                    + ").");
                break;
            }

            offset = nextOffset;
        }

        return matches;
    }

    private static bool IsClosedDemandStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        switch (status.Trim().ToLowerInvariant())
        {
            case "accepted":
            case "rejected":
            case "canceled":
            case "cancelled":
            case "done":
            case "fulfilled":
            case "closed":
                return true;
            default:
                return false;
        }
    }

    private static void ExportDemandListArguments(
        IInlineInvokeProxy cph,
        List<VKVideoLiveApiService.ChannelPointDemand> demands)
    {
        var demandIds = new List<long>();
        var demandRewardIds = new List<string>();
        var demandUserIds = new List<long>();
        var demandUserNicks = new List<string>();
        var demandStatuses = new List<string>();

        foreach (var demand in demands)
        {
            if (demand == null)
                continue;

            demandIds.Add(demand.Id);
            demandRewardIds.Add(demand.Reward?.Id ?? string.Empty);
            demandUserIds.Add(demand.User?.Id ?? 0);
            demandUserNicks.Add(demand.User?.Nick ?? string.Empty);
            demandStatuses.Add(demand.Status ?? string.Empty);
        }

        cph.SetArgument("demandIds", demandIds);
        cph.SetArgument("demandRewardIds", demandRewardIds);
        cph.SetArgument("demandUserIds", demandUserIds);
        cph.SetArgument("demandUserNicks", demandUserNicks);
        cph.SetArgument("demandStatuses", demandStatuses);
        cph.SetArgument("demandsCount", demandIds.Count);
    }

    private static bool TryGetDemandIdArg(IInlineInvokeProxy cph, out long demandId)
    {
        if (cph.TryGetArg("demandId", out demandId) && demandId > 0)
            return true;
        if (cph.TryGetArg("demand_id", out demandId) && demandId > 0)
            return true;

        demandId = 0;
        return false;
    }

    private static bool TryGetRewardIdArg(IInlineInvokeProxy cph, out string rewardId)
    {
        foreach (string name in new[] { "rewardId", "reward_id", "minichat.Data.RewardID" })
        {
            if (cph.TryGetArg(name, out rewardId) && !string.IsNullOrWhiteSpace(rewardId))
                return true;
        }

        rewardId = null;
        return false;
    }

    public bool GetViewerInfo()
    {
        return GetViewerInfoInternal(CPH);
    }

    private bool GetViewerInfoInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
        {
            cph.LogWarn("[VKVideoLive get viewer info] Missing required argument channel_name.");
            return false;
        }

        string channelName = channelNameObj.ToString();

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            if (!TryResolveViewerUserId(cph, channelName, authState.AccessToken, out long userId))
                return false;

            string url = VKVideoLiveApiService.ServiceOfficialApiHost
                         + "/chat/member?channel_url=" + Uri.EscapeDataString(channelName)
                         + "&user_id=" + userId;

            if (!TryGetJson(url, authState.AccessToken, out JObject root, out string error))
            {
                cph.LogWarn("[VKVideoLive get viewer info] /chat/member failed: " + error);
                return false;
            }

            var data = AsObject(root["data"]);
            var user = AsObject(data?["user"]);
            var statistics = AsObject(data?["statistics"]);
            var channel = AsObject(data?["channel"]);

            string nick = user?["nick"]?.ToString() ?? string.Empty;
            long id = userId;
            if (!TryReadPositiveLong(user?["id"], out id))
                id = userId;

            bool isModerator = user?["is_moderator"]?.ToObject<bool>() ?? false;
            bool isOwner = user?["is_owner"]?.ToObject<bool>() ?? false;

            long registeredAt = 0;
            if (TryReadPositiveLong(user?["registered_at"], out registeredAt))
                cph.SetArgument("registeredAt", registeredAt);

            long chatMessagesCount = 0;
            TryReadNonNegativeLong(statistics?["chat_messages_count"], out chatMessagesCount);

            long permanentBansCount = 0;
            TryReadNonNegativeLong(statistics?["permanent_bans_count"], out permanentBansCount);

            long temporaryBansCount = 0;
            TryReadNonNegativeLong(statistics?["temporary_bans_count"], out temporaryBansCount);

            long totalWatchedTime = 0;
            TryReadNonNegativeLong(statistics?["total_watched_time"], out totalWatchedTime);

            var roleNames = new List<string>();
            if (user?["roles"] is JArray roles)
            {
                foreach (var role in roles)
                {
                    string roleName = role["name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(roleName))
                        roleNames.Add(roleName);
                }
            }

            var badgeNames = new List<string>();
            if (user?["badges"] is JArray badges)
            {
                foreach (var badge in badges)
                {
                    string badgeName = badge["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(badgeName))
                        badgeName = badge["achievement_name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(badgeName))
                        badgeNames.Add(badgeName);
                }
            }

            cph.SetArgument("userName", nick);
            cph.SetArgument("user", nick);
            cph.SetArgument("userId", id);
            cph.SetArgument("isModerator", isModerator);
            cph.SetArgument("isOwner", isOwner);
            cph.SetArgument("chatMessagesCount", chatMessagesCount);
            cph.SetArgument("permanentBansCount", permanentBansCount);
            cph.SetArgument("temporaryBansCount", temporaryBansCount);
            cph.SetArgument("totalWatchedTime", totalWatchedTime);
            cph.SetArgument("roleNames", roleNames);
            cph.SetArgument("badgeNames", badgeNames);
            cph.SetArgument("channelStatus", channel?["status"]?.ToString() ?? string.Empty);
            cph.SetArgument("channelUrl", channel?["url"]?.ToString() ?? string.Empty);

            cph.LogInfo("[VKVideoLive get viewer info] userId=" + id + ", nick='" + nick
                        + "', messages=" + chatMessagesCount
                        + ", watched=" + totalWatchedTime
                        + ", registeredAt=" + registeredAt);
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive get viewer info] Error fetching viewer, " + e.Message);
            return false;
        }
    }

    private bool TryGetJson(string url, string accessToken, out JObject root, out string error)
    {
        root = null;
        error = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using HttpResponseMessage response = _client.SendAsync(request).GetAwaiter().GetResult();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                error = "HTTP " + (int)response.StatusCode + ": " + TruncateForLog(responseBody);
                return false;
            }

            root = JObject.Parse(responseBody);
            if (AsObject(root["data"]) == null)
            {
                error = "empty data";
                root = null;
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private static JObject AsObject(JToken token)
    {
        return token is JObject jo ? jo : null;
    }

    private static bool TryReadPositiveLong(JToken token, out long value)
    {
        value = 0;
        return TryReadLong(token, out value, requirePositive: true);
    }

    private static bool TryReadNonNegativeLong(JToken token, out long value)
    {
        value = 0;
        return TryReadLong(token, out value, requirePositive: false);
    }

    private static bool TryReadLong(JToken token, out long value, bool requirePositive)
    {
        value = 0;
        if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            return false;

        try
        {
            long parsed;
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                parsed = token.Value<long>();
            }
            else if (!long.TryParse(token.ToString(), out parsed))
            {
                return false;
            }

            if (parsed < 0)
                return false;
            if (requirePositive && parsed == 0)
                return false;

            value = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Resolves user id from userId / user_id / id, or by nick via /chat/members (present list only).
    private bool TryResolveViewerUserId(IInlineInvokeProxy cph, string channelName, string accessToken, out long userId)
    {
        if (TryGetViewerUserIdArg(cph, out userId))
            return true;

        string nick = null;
        if (cph.TryGetArg("userName", out object userNameObj) && userNameObj != null)
            nick = userNameObj.ToString();
        else if (cph.TryGetArg("user", out object userObj) && userObj != null)
            nick = userObj.ToString();

        if (string.IsNullOrWhiteSpace(nick))
        {
            cph.LogWarn("[VKVideoLive get viewer] Missing userId (user_id / id / minichat.Data.UserID). Optionally pass userName/user to resolve from present viewers.");
            return false;
        }

        if (!TryFindUserIdByNick(channelName, accessToken, nick.Trim(), out userId))
        {
            cph.LogWarn("[VKVideoLive get viewer] Could not resolve userId for nick '" + nick + "' in present viewers (max 200).");
            return false;
        }

        cph.LogInfo("[VKVideoLive get viewer] Resolved nick '" + nick + "' → userId=" + userId);
        return true;
    }

    private static bool TryGetViewerUserIdArg(IInlineInvokeProxy cph, out long userId)
    {
        userId = 0;
        object userIdObj = null;
        if (cph.TryGetArg("userId", out userIdObj) && userIdObj != null)
        {
            // ok
        }
        else if (cph.TryGetArg("user_id", out userIdObj) && userIdObj != null)
        {
            // ok
        }
        else if (cph.TryGetArg("id", out userIdObj) && userIdObj != null)
        {
            // ok — same key as GetViewers users[].id
        }
        else if (cph.TryGetArg("minichat.Data.UserID", out userIdObj) && userIdObj != null)
        {
            // ok — MiniChat chat / event payload
        }
        else
        {
            return false;
        }

        return TryCoerceToPositiveLong(userIdObj, out userId);
    }

    private static bool TryCoerceToPositiveLong(object value, out long userId)
    {
        userId = 0;
        if (value == null)
            return false;

        switch (value)
        {
            case long l:
                userId = l;
                break;
            case int i:
                userId = i;
                break;
            case short s:
                userId = s;
                break;
            case uint ui:
                userId = ui;
                break;
            case ulong ul when ul <= long.MaxValue:
                userId = (long)ul;
                break;
            case double d:
                userId = (long)d;
                break;
            case float f:
                userId = (long)f;
                break;
            case decimal m:
                userId = (long)m;
                break;
            default:
                if (!long.TryParse(value.ToString(), out userId))
                    return false;
                break;
        }

        return userId > 0;
    }

    private bool TryFindUserIdByNick(string channelName, string accessToken, string nick, out long userId)
    {
        userId = 0;
        string url = VKVideoLiveApiService.ServiceOfficialApiHost
                     + "/chat/members?channel_url=" + Uri.EscapeDataString(channelName)
                     + "&limit=200";

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = _client.GetAsync(url).GetAwaiter().GetResult();
        string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            return false;

        var usersToken = JObject.Parse(responseBody)["data"]?["users"] as JArray;
        if (usersToken == null)
            return false;

        foreach (var user in usersToken)
        {
            string userNick = user["nick"]?.ToString();
            if (string.IsNullOrWhiteSpace(userNick))
                continue;
            if (!string.Equals(userNick, nick, StringComparison.OrdinalIgnoreCase))
                continue;

            long? id = user["id"]?.ToObject<long?>();
            if (id.HasValue && id.Value > 0)
            {
                userId = id.Value;
                return true;
            }
        }

        return false;
    }

    private static string TruncateForLog(string text, int maxLen = 300)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        text = text.Replace("\r", " ").Replace("\n", " ");
        return text.Length <= maxLen ? text : text.Substring(0, maxLen) + "...";
    }

    public bool GetViewers()
    {
        return GetViewersInternal(CPH);
    }

    private bool GetViewersInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
            return false;

        string channelName = channelNameObj.ToString();
        var viewersList = new List<Dictionary<string, object>>();

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            // Запрос к API
            string url = VKVideoLiveApiService.ServiceOfficialApiHost
                         + "/chat/members?channel_url=" + Uri.EscapeDataString(channelName)
                         + "&limit=200";

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
            using HttpResponseMessage response = _client.GetAsync(url).GetAwaiter().GetResult();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            response.EnsureSuccessStatusCode();

            // Текущий ответ API содержит data.users, а не data.members
            var rootToken = JObject.Parse(responseBody);
            var usersToken = rootToken["data"]?["users"] as JArray;
            int apiCount = usersToken?.Count ?? 0;

            cph.LogInfo("[VKVideoLive get viewers] Канал: '" + channelName + "', получено от API элементов: " + apiCount);

            if (usersToken != null)
            {
                foreach (var user in usersToken)
                {
                    string nick = user["nick"]?.ToString();
                    int id = user["id"]?.ToObject<int>() ?? 0;
                    string avatarUrl = user["avatar_url"]?.ToString();

                    var userData = new Dictionary<string, object>
                    {
                        { "userName", nick },
                        { "id", id },
                        { "avatarUrl", avatarUrl }
                    };
                    viewersList.Add(userData);
                }
            }

            if (viewersList.Count > 0)
            {
                var viewersNames = string.Join(", ", viewersList
                    .Select(v => v.ContainsKey("userName") && v["userName"] != null ? v["userName"].ToString() : string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
                cph.LogInfo("[VKVideoLive get viewers] Получен список зрителей (" + viewersList.Count + "): " + viewersNames);
            }
            else
            {
                cph.LogInfo("[VKVideoLive get viewers] Список зрителей пуст.");
            }

            cph.SetArgument("users", viewersList);
            cph.SetArgument("viewers_count", viewersList.Count);
            cph.SetArgument("isLive", true);
            cph.SetArgument("isTest", false);
            cph.TriggerCodeEvent("VKVideoLive_PresentViewers", true);
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive get viewers] Error fetching viewers list, " + e.Message);
            return false;
        }
    }

    public bool GetRandomViewer()
    {
        return GetRandomViewerInternal(CPH);
    }

    private bool GetRandomViewerInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
            return false;

        string channelName = channelNameObj.ToString();
        string lastRandomViewer = cph.GetGlobalVar<string>(VkLiveLastRandomViewerKey, true);

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            // Запрос к API для получения списка участников чата
            string url = VKVideoLiveApiService.ServiceOfficialApiHost
                         + "/chat/members?channel_url=" + Uri.EscapeDataString(channelName)
                         + "&limit=200";

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authState.AccessToken);
            using HttpResponseMessage response = _client.GetAsync(url).GetAwaiter().GetResult();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            response.EnsureSuccessStatusCode();

            var rootToken = JObject.Parse(responseBody);
            var usersToken = rootToken["data"]?["users"] as JArray;

            var viewers = new List<string>();
            if (usersToken != null)
            {
                foreach (var user in usersToken)
                {
                    string nick = user["nick"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(nick))
                    {
                        viewers.Add(nick);
                    }
                }
            }

            if (viewers.Count == 0)
            {
                cph.SetArgument("randomUserName0", "");
                return true;
            }

            var rnd = new Random();

            var candidates = string.IsNullOrEmpty(lastRandomViewer)
                ? viewers
                : viewers.Where(v => !string.Equals(v, lastRandomViewer, StringComparison.Ordinal)).ToList();

            if (candidates.Count == 0)
            {
                candidates = viewers;
            }

            var viewer = candidates[rnd.Next(candidates.Count)];

            cph.SetArgument("randomUserName0", viewer);
            cph.SetGlobalVar(VkLiveLastRandomViewerKey, viewer, true);
        }
        catch (Exception e)
        {
            cph.LogWarn("Error fetching viewers list, " + e.Message);
            return false;
        }

        return true;
    }

    public bool GetViewersCount()
    {
        return GetViewersCountInternal(CPH);
    }

    private bool GetViewersCountInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
            return false;

        string channelName = channelNameObj.ToString();

        try
        {
            var authState = EnsureValidAuth(cph);
            if (authState == null)
                return false;

            int viewers = _vkVideoLiveApiService.GetChannelViewersCount(channelName, authState.AccessToken);
            cph.SetArgument("viewers_count", viewers);
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive get viewers count] Error fetching viewers count, " + e.Message);
            return false;
        }
        return true;
    }

    public bool GetInOutViewers()
    {
        return GetInOutViewersInternal(CPH);
    }

    private bool GetInOutViewersInternal(IInlineInvokeProxy cph)
    {
        try
        {
            var previousPresent = cph.GetGlobalVar<HashSet<string>>(VkLivePreviousPresentViewersKey, true) ?? new HashSet<string>();
            var todaysViewers = cph.GetGlobalVar<HashSet<string>>(VkLiveTodaysViewersKey, true) ?? new HashSet<string>();

            if (!cph.TryGetArg("users", out object usersObj))
            {
                cph.LogDebug("[VKVideoLive GetInOutViewers] Аргумент users отсутствует.");
                return false;
            }

            var currentNames = new HashSet<string>();
            var currentViewers = usersObj as List<Dictionary<string, object>>;
            if (currentViewers != null)
            {
                foreach (var viewer in currentViewers)
                {
                    if (viewer.ContainsKey("userName") && viewer["userName"] != null)
                        currentNames.Add(viewer["userName"].ToString());
                }
            }

            var currentNamesForSaving = new HashSet<string>(currentNames);
            var newTodayNames = new HashSet<string>();

            foreach (var name in currentNames)
            {
                if (!todaysViewers.Contains(name))
                {
                    TriggerViewerPresence(cph, name, VkLiveViewerFirstTodayEvent);
                    todaysViewers.Add(name);
                    newTodayNames.Add(name);
                }
            }
            cph.SetGlobalVar(VkLiveTodaysViewersKey, todaysViewers, true);

            foreach (var name in currentNames)
            {
                if (newTodayNames.Contains(name))
                    continue;
                if (!previousPresent.Contains(name))
                    TriggerViewerPresence(cph, name, VkLiveViewerJoinedEvent);
            }

            foreach (var name in previousPresent)
            {
                if (!currentNames.Contains(name))
                    TriggerViewerPresence(cph, name, VkLiveViewerLeftEvent);
            }

            cph.SetGlobalVar(VkLivePreviousPresentViewersKey, new HashSet<string>(currentNamesForSaving), true);
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive GetInOutViewers] Ошибка, " + e.Message);
            return false;
        }
    }

    private void TriggerViewerPresence(IInlineInvokeProxy cph, string displayName, string eventName)
    {
        cph.SetArgument("userName", displayName);
        cph.SetArgument("user", displayName);
        cph.TriggerCodeEvent(eventName, true);
    }

    public bool GetNewViewers()
    {
        return GetNewViewersInternal(CPH);
    }

    private bool GetNewViewersInternal(IInlineInvokeProxy cph)
    {
        if (!cph.TryGetArg("users", out object usersObj) || usersObj is not List<Dictionary<string, object>> users || users.Count == 0)
        {
            cph.LogInfo("Список зрителей пуст или аргумент users не передан. Вызовите Get Viewers или привяжите экшен к триггеру Present Viewers (VK Video Live).");
            return true;
        }

        var todayViewers = cph.GetGlobalVar<HashSet<string>>(VkLiveTodaysViewersKey, true) ?? new HashSet<string>();

        try
        {
            for (int i = 0; i < users.Count; i++)
            {
                string displayName = users[i].ContainsKey("userName") ? users[i]["userName"]?.ToString() : null;
                if (string.IsNullOrEmpty(displayName))
                    continue;
                if (todayViewers.Contains(displayName))
                    continue;

                todayViewers.Add(displayName);
                cph.SetGlobalVar(VkLiveTodaysViewersKey, todayViewers, true);
                TriggerViewerPresence(cph, displayName, VkLiveViewerFirstTodayEvent);
                cph.LogInfo("Новый зритель: " + displayName);
            }
        }
        catch (Exception e)
        {
            cph.LogWarn("Error fetching new viewer, " + e.Message);
        }

        return true;
    }

    public bool AddFirstWordViewer()
    {
        return AddFirstWordViewerInternal(CPH);
    }

    private bool AddFirstWordViewerInternal(IInlineInvokeProxy cph)
    {
        var todayViewers = cph.GetGlobalVar<HashSet<string>>(VkLiveTodaysViewersKey, true) ?? new HashSet<string>();

        if (!cph.TryGetArg("userName", out object userNameObj) || userNameObj == null)
            return true;

        string userName = userNameObj.ToString();
        if (string.IsNullOrWhiteSpace(userName))
            return true;

        if (!todayViewers.Contains(userName))
        {
            todayViewers.Add(userName);
            cph.SetGlobalVar(VkLiveTodaysViewersKey, todayViewers, true);
        }

        return true;
    }

    public bool VkAuthShowUi()
    {
        return VkAuthShowUiInternal(CPH);
    }

    // Публичный метод — точка входа Streamer.bot (отдельный экшен / вызов без UI). Обновление токена также выполняется в EnsureValidAuth при обращении к API.
    public bool VkAuthRefresh()
    {
        return VkAuthRefreshInternal(CPH);
    }

    private bool VkAuthShowUiInternal(IInlineInvokeProxy cph)
    {
        try
        {
            if (_vkAuthService == null)
                _vkAuthService = new VkOAuthService(_client);

            var authState = _vkAuthService.LoadStateFromGlobals(cph);

            var window = new VkAuthWindow(_vkAuthService, authState, cph);
            window.ShowDialog();

            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive auth] Ошибка при показе окна авторизации, " + e.Message);
            return false;
        }
    }

    private bool VkAuthRefreshInternal(IInlineInvokeProxy cph)
    {
        try
        {
            if (_vkAuthService == null)
                _vkAuthService = new VkOAuthService(_client);

            var state = _vkAuthService.LoadStateFromGlobals(cph);
            if (string.IsNullOrEmpty(state.RefreshToken))
            {
                cph.LogWarn("[VKVideoLive auth] Нельзя обновить токен: refresh_token отсутствует");
                return false;
            }

            state = _vkAuthService.RefreshTokens(state, cph);
            _vkAuthService.SaveStateToGlobals(state, cph);
            return true;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive auth] Ошибка при обновлении авторизации, " + e.Message);
            return false;
        }
    }

    private VkAuthState EnsureValidAuth(IInlineInvokeProxy cph)
    {
        try
        {
            if (_vkAuthService == null)
                _vkAuthService = new VkOAuthService(_client);

            var state = _vkAuthService.LoadStateFromGlobals(cph);
            if (state == null || string.IsNullOrEmpty(state.AccessToken))
            {
                cph.LogWarn("[VKVideoLive auth] Нет access_token для API. Выполните первоначальную авторизацию.");
                return null;
            }

            if (state.ExpiresAtUtc.HasValue)
            {
                var now = DateTime.UtcNow;
                // Обновляем токен, если он уже истёк или истекает в ближайшую минуту
                if (state.ExpiresAtUtc.Value <= now.AddMinutes(1))
                {
                    state = _vkAuthService.RefreshTokens(state, cph);
                    _vkAuthService.SaveStateToGlobals(state, cph);
                }
            }

            return state;
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive auth] Не удалось проверить/обновить токен API, " + e.Message);
            return null;
        }
    }
}

public class VKVideoLiveApiService
{
    internal HttpClient Client { get; set; }
    // Official API host (to be used for OAuth/API-specific calls and current API calls)
    internal const string ServiceOfficialApiHost = "https://apidev.live.vkvideo.ru/v1";
    private const string EndpointChannelPoints = "/channel_point/rewards/manage_info";
    private const string EndpointRewardEnable = "/channel_point/reward/enable";
    private const string EndpointRewardDisable = "/channel_point/reward/disable";
    private const string EndpointRewardActivate = "/channel_point/reward/activate";
    private const string EndpointRewardDemands = "/channel_point/reward/demands";
    private const string EndpointRewardDemandReject = "/channel_point/reward/demand/reject";
    private const string EndpointRewardDemandAccept = "/channel_point/reward/demand/accept";

    public VKVideoLiveApiService(HttpClient client)
    {
        Client = client;
    }

    public int GetChannelViewersCount(string channelUrl, string token)
    {
        string url = ServiceOfficialApiHost + "/channel?channel_url=" + Uri.EscapeDataString(channelUrl);
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = Client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var root = JsonConvert.DeserializeObject<ChannelInfoRootResponse>(responseBody);

            // Текущее количество зрителей берём из data.stream.counters.viewers (активный стрим)
            int viewers = root?.Data?.Stream?.Counters?.Viewers ?? 0;

            return viewers;
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException("[VKVideoLive channel] Error fetching channel info for viewers count: " + e.Message, e);
        }
    }

    public ChannelPointResponse GetChannelPoints(string channelUrl, string token)
    {
        string url = ServiceOfficialApiHost + EndpointChannelPoints + "?channel_url=" + Uri.EscapeDataString(channelUrl);
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = Client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var root = JsonConvert.DeserializeObject<ChannelPointRootResponse>(responseBody);
            return root?.Data;
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException("[VKVideoLive points] Error fetching channel points: " + e.Message, e);
        }
    }

    public void EnableReward(string channelUrl, string rewardId, string token)
    {
        string url = ServiceOfficialApiHost
                     + EndpointRewardEnable
                     + "?channel_url=" + Uri.EscapeDataString(channelUrl)
                     + "&reward_id=" + Uri.EscapeDataString(rewardId);
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using HttpResponseMessage response = Client.PostAsync(url, content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException("[VKVideoLive points] Error enabling reward via API: " + e.Message, e);
        }
    }

    public void DisableReward(string channelUrl, string rewardId, string token)
    {
        string url = ServiceOfficialApiHost
                     + EndpointRewardDisable
                     + "?channel_url=" + Uri.EscapeDataString(channelUrl)
                     + "&reward_id=" + Uri.EscapeDataString(rewardId);
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            using HttpResponseMessage response = Client.PostAsync(url, content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException("[VKVideoLive points] Error disabling reward via API: " + e.Message, e);
        }
    }

    public void ActivateReward(string channelUrl, string rewardId, string token, string videoUrl)
    {
        string url = ServiceOfficialApiHost
                     + EndpointRewardActivate
                     + "?channel_url=" + Uri.EscapeDataString(channelUrl);

        object messagePart;

        if (!string.IsNullOrWhiteSpace(videoUrl)
            && Uri.TryCreate(videoUrl, UriKind.Absolute, out Uri uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            messagePart = new
            {
                link = new
                {
                    content = videoUrl,
                    url = videoUrl
                }
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                throw new InvalidOperationException("[VKVideoLive points] Error activating reward via API: message text is empty");
            }

            string textContent = videoUrl;
            messagePart = new
            {
                text = new
                {
                    content = textContent
                }
            };
        }

        var body = new
        {
            reward = new
            {
                id = rewardId,
                message = new
                {
                    parts = new[]
                    {
                        messagePart
                    }
                }
            }
        };

        string json = JsonConvert.SerializeObject(body);

        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = Client.PostAsync(url, content).GetAwaiter().GetResult();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException("[VKVideoLive points] Error activating reward via API. " +
                                                    "StatusCode: " + (int)response.StatusCode + " (" + response.StatusCode + "), Body: " + responseBody);
            }
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException("[VKVideoLive points] Error activating reward via API: " + e.Message, e);
        }
    }

    public ChannelPointDemandsPage GetRewardDemands(
        string channelUrl,
        string token,
        long limit,
        long offset)
    {
        string url = ServiceOfficialApiHost
                     + EndpointRewardDemands
                     + "?channel_url=" + Uri.EscapeDataString(channelUrl)
                     + "&limit=" + limit
                     + "&offset=" + offset;
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = Client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var root = JsonConvert.DeserializeObject<ChannelPointDemandsRootResponse>(responseBody);
            return new ChannelPointDemandsPage
            {
                Demands = root?.Data?.Demands ?? new List<ChannelPointDemand>(),
                IsLast = root?.Extra?.IsLast ?? true,
                Offset = root?.Extra?.Offset ?? offset
            };
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException(
                "[VKVideoLive points] Error fetching reward demands via API: " + e.Message,
                e);
        }
    }

    public void RejectRewardDemand(string channelUrl, long demandId, string token)
    {
        PostRewardDemandStatus(channelUrl, demandId, token, EndpointRewardDemandReject, "rejecting");
    }

    public void AcceptRewardDemand(string channelUrl, long demandId, string token)
    {
        PostRewardDemandStatus(channelUrl, demandId, token, EndpointRewardDemandAccept, "accepting");
    }

    private void PostRewardDemandStatus(
        string channelUrl,
        long demandId,
        string token,
        string endpoint,
        string actionLabel)
    {
        string url = ServiceOfficialApiHost
                     + endpoint
                     + "?channel_url=" + Uri.EscapeDataString(channelUrl);

        var body = new
        {
            demands = new[]
            {
                new { id = demandId }
            }
        };

        string json = JsonConvert.SerializeObject(body);

        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = Client.PostAsync(url, content).GetAwaiter().GetResult();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    "[VKVideoLive points] Error " + actionLabel + " reward demand via API. "
                    + "StatusCode: " + (int)response.StatusCode + " (" + response.StatusCode + "), Body: "
                    + responseBody);
            }
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException(
                "[VKVideoLive points] Error " + actionLabel + " reward demand via API: " + e.Message,
                e);
        }
    }

    public class ChannelInfoRootResponse
    {
        [JsonProperty("data")]
        public ChannelInfoData Data { get; set; }
    }

    public class ChannelInfoData
    {
        [JsonProperty("channel")]
        public ChannelInfoChannel Channel { get; set; }

        [JsonProperty("stream")]
        public ChannelInfoStream Stream { get; set; }
    }

    public class ChannelInfoChannel
    {
        [JsonProperty("counters")]
        public ChannelInfoCounters Counters { get; set; }
    }

    public class ChannelInfoCounters
    {
        [JsonProperty("viewers")]
        public int Viewers { get; set; }

        [JsonProperty("views")]
        public int Views { get; set; }
    }

    public class ChannelInfoStream
    {
        [JsonProperty("counters")]
        public ChannelInfoCounters Counters { get; set; }
    }

    public class ChannelPointRootResponse
    {
        [JsonProperty("data")]
        public ChannelPointResponse Data { get; set; }
    }

    public class ChannelPointResponse
    {
        [JsonProperty("balance")]
        public ChannelPointBalance Balance { get; set; }

        [JsonProperty("rewards")]
        public List<ChannelPointReward> Rewards { get; set; } = new List<ChannelPointReward>();
    }

    public class ChannelPointBalance
    {
        [JsonProperty("available")]
        public long Available { get; set; }

        [JsonProperty("total_earned")]
        public long TotalEarned { get; set; }

        [JsonProperty("total_spent")]
        public long TotalSpent { get; set; }
    }

    public class ChannelPointReward
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("price")]
        public long Price { get; set; }

        [JsonProperty("is_disabled")]
        public bool IsDisabled { get; set; }
    }

    public class ChannelPointDemandsRootResponse
    {
        [JsonProperty("data")]
        public ChannelPointDemandsData Data { get; set; }

        [JsonProperty("extra")]
        public ChannelPointDemandsExtra Extra { get; set; }
    }

    public class ChannelPointDemandsData
    {
        [JsonProperty("demands")]
        public List<ChannelPointDemand> Demands { get; set; } = new List<ChannelPointDemand>();
    }

    public class ChannelPointDemandsExtra
    {
        [JsonProperty("is_last")]
        public bool IsLast { get; set; }

        [JsonProperty("offset")]
        public long Offset { get; set; }
    }

    public class ChannelPointDemandsPage
    {
        public List<ChannelPointDemand> Demands { get; set; } = new List<ChannelPointDemand>();
        public bool IsLast { get; set; }
        public long Offset { get; set; }
    }

    public class ChannelPointDemand
    {
        [JsonProperty("created_at")]
        public long CreatedAt { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("reward")]
        public ChannelPointDemandReward Reward { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("user")]
        public ChannelPointDemandUser User { get; set; }
    }

    public class ChannelPointDemandReward
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class ChannelPointDemandUser
    {
        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("nick")]
        public string Nick { get; set; }

        [JsonProperty("nick_color")]
        public int NickColor { get; set; }
    }
}

public class VkOAuthService
{
    private readonly HttpClient _client;

    private const string AuthBaseUrl = "https://auth.live.vkvideo.ru/app/oauth2/authorize";
    private const string TokenUrl = "https://api.live.vkvideo.ru/oauth/server/token";
    private const string RevokeUrl = "https://api.live.vkvideo.ru/oauth/server/revoke";

    private const string DefaultScope =
        "channel:credentials,channel:stream:settings,chat:message:send,chat:settings,channel:points,channel:points:rewards,channel:points:rewards:demands,channel:roles";

    private const string GlobalAccessTokenKey = "VkLiveAuthAccessToken";
    private const string GlobalRefreshTokenKey = "VkLiveAuthRefreshToken";
    private const string GlobalExpiresAtKey = "VkLiveAuthExpiresAtUtc";

    private const string GlobalClientIdKey = "VkLiveAuthClientId";
    private const string GlobalClientSecretKey = "VkLiveAuthClientSecret";
    private const string GlobalRedirectUriKey = "VkLiveAuthRedirectUri";

    internal const string GlobalUserNickKey = "VkLiveAuthUserNick";
    internal const string GlobalUserAvatarUrlKey = "VkLiveAuthUserAvatarUrl";

    public VkOAuthService(HttpClient client)
    {
        _client = client;
    }

    public VkAuthState LoadStateFromGlobals(IInlineInvokeProxy cph)
    {
        var state = new VkAuthState
        {
            AccessToken = cph.GetGlobalVar<string>(GlobalAccessTokenKey, true),
            RefreshToken = cph.GetGlobalVar<string>(GlobalRefreshTokenKey, true)
        };

        string expiresAtRaw = cph.GetGlobalVar<string>(GlobalExpiresAtKey, true);
        if (!string.IsNullOrEmpty(expiresAtRaw) && DateTime.TryParse(expiresAtRaw, out DateTime expiresAt))
        {
            state.ExpiresAtUtc = expiresAt;
        }

        return state;
    }

    public void SaveStateToGlobals(VkAuthState state, IInlineInvokeProxy cph)
    {
        cph.SetGlobalVar(GlobalAccessTokenKey, state.AccessToken ?? string.Empty, true);
        cph.SetGlobalVar(GlobalRefreshTokenKey, state.RefreshToken ?? string.Empty, true);
        var expiresString = state.ExpiresAtUtc.HasValue ? state.ExpiresAtUtc.Value.ToString("o") : string.Empty;
        cph.SetGlobalVar(GlobalExpiresAtKey, expiresString, true);
    }

    private (string clientId, string clientSecret, string redirectUri, string scope) LoadConfig(IInlineInvokeProxy cph)
    {
        string clientId = cph.GetGlobalVar<string>(GlobalClientIdKey, true);
        string clientSecret = cph.GetGlobalVar<string>(GlobalClientSecretKey, true);
        string redirectUri = cph.GetGlobalVar<string>(GlobalRedirectUriKey, true);
        string scope = DefaultScope;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(redirectUri))
        {
            throw new InvalidOperationException("VK auth config is not set in global variables.");
        }

        return (clientId, clientSecret, redirectUri, scope);
    }

    public VkAuthState StartLoginFlow(IInlineInvokeProxy cph)
    {
        var config = LoadConfig(cph);
        string stateValue = Guid.NewGuid().ToString("N");

        string authorizeUrl = BuildAuthorizeUrl(config.clientId, config.redirectUri, config.scope, stateValue);
        OpenInBrowser(authorizeUrl);

        var code = WaitForAuthorizationCode(config.redirectUri, stateValue);
        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("Authorization code not received.");
        }

        var tokenState = ExchangeCodeForTokens(code, config.clientId, config.clientSecret, config.redirectUri);
        SaveStateToGlobals(tokenState, cph);
        TryUpdateProfileInfo(tokenState.AccessToken, cph);
        return tokenState;
    }

    public VkAuthState RefreshTokens(VkAuthState currentState, IInlineInvokeProxy cph)
    {
        var config = LoadConfig(cph);
        if (string.IsNullOrEmpty(currentState.RefreshToken))
        {
            throw new InvalidOperationException("Refresh token is empty.");
        }

        var refreshed = RefreshTokensInternal(currentState.RefreshToken, config.clientId, config.clientSecret);
        SaveStateToGlobals(refreshed, cph);
        return refreshed;
    }

    private void TryUpdateProfileInfo(string accessToken, IInlineInvokeProxy cph)
    {
        if (string.IsNullOrEmpty(accessToken))
            return;

        try
        {
            // current_user доступен через DevAPI-хост
            string url = VKVideoLiveApiService.ServiceOfficialApiHost + "/current_user";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using HttpResponseMessage response = _client.SendAsync(request).GetAwaiter().GetResult();
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                cph.LogWarn("[VKVideoLive auth] Не удалось получить current_user: " + response.StatusCode);
                return;
            }

            var root = JObject.Parse(body);
            var user = root["data"]?["user"];
            if (user == null)
                return;

            string nick = (string)user["nick"] ?? string.Empty;
            string avatarUrl = (string)user["avatar_url"] ?? string.Empty;

            cph.SetGlobalVar(GlobalUserNickKey, nick, true);
            cph.SetGlobalVar(GlobalUserAvatarUrlKey, avatarUrl, true);
        }
        catch (Exception e)
        {
            cph.LogWarn("[VKVideoLive auth] Не удалось обновить информацию о пользователе (current_user): " + e.Message);
        }
    }

    public void Logout(VkAuthState currentState, IInlineInvokeProxy cph)
    {
        try
        {
            if (!string.IsNullOrEmpty(currentState.AccessToken))
            {
                RevokeToken(currentState.AccessToken, "access_token", cph);
            }

            if (!string.IsNullOrEmpty(currentState.RefreshToken))
            {
                RevokeToken(currentState.RefreshToken, "refresh_token", cph);
            }
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("[VKVideoLive auth] Ошибка при отзыве токенов: " + e.Message, e);
        }

        var cleared = new VkAuthState();
        SaveStateToGlobals(cleared, cph);
    }

    private string BuildAuthorizeUrl(string clientId, string redirectUri, string scope, string state)
    {
        var uriBuilder = new StringBuilder();
        uriBuilder.Append(AuthBaseUrl);
        uriBuilder.Append("?client_id=").Append(Uri.EscapeDataString(clientId));
        uriBuilder.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        uriBuilder.Append("&response_type=code");
        if (!string.IsNullOrEmpty(scope))
        {
            uriBuilder.Append("&scope=").Append(Uri.EscapeDataString(scope));
        }
        if (!string.IsNullOrEmpty(state))
        {
            uriBuilder.Append("&state=").Append(Uri.EscapeDataString(state));
        }
        return uriBuilder.ToString();
    }

    private void OpenInBrowser(string url)
    {
        var psi = new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    private string WaitForAuthorizationCode(string redirectUri, string expectedState)
    {
        var uri = new Uri(redirectUri);
        string prefix = $"{uri.Scheme}://{uri.Host}:{uri.Port}{uri.AbsolutePath}";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix.EndsWith("/") ? prefix : prefix + "/");
        listener.Start();

        var context = listener.GetContext();
        var query = context.Request.Url.Query;
        var queryParams = System.Web.HttpUtility.ParseQueryString(query);
        string code = queryParams["code"];
        string state = queryParams["state"];

        if (!string.IsNullOrEmpty(expectedState) && !string.Equals(expectedState, state, StringComparison.Ordinal))
        {
            code = null;
        }

        string responseString = "<!DOCTYPE html><html><head><meta charset=\"utf-8\" />"
                                + "<title>VK Видео Live</title></head><body>"
                                + "<h2>Авторизация VK Видео Live завершена</h2>"
                                + "<p>Можно вернуться в Streamer.bot и закрыть это окно.</p>"
                                + "</body></html>";
        byte[] buffer = Encoding.UTF8.GetBytes(responseString);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Close();

        listener.Stop();
        return code;
    }

    private VkAuthState ExchangeCodeForTokens(string code, string clientId, string clientSecret, string redirectUri)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using HttpResponseMessage response = _client.SendAsync(request).GetAwaiter().GetResult();
        string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("VK token endpoint returned non-success status code: " + response.StatusCode);
        }

        var tokenResponse = JsonConvert.DeserializeObject<VkTokenResponse>(responseBody);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new InvalidOperationException("Token response is empty.");

        var state = new VkAuthState
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
        };

        return state;
    }

    private VkAuthState RefreshTokensInternal(string refreshToken, string clientId, string clientSecret)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using HttpResponseMessage response = _client.SendAsync(request).GetAwaiter().GetResult();
        string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("VK token endpoint returned non-success status code (refresh): " + response.StatusCode);
        }

        var tokenResponse = JsonConvert.DeserializeObject<VkTokenResponse>(responseBody);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            throw new InvalidOperationException("Token response is empty.");

        var state = new VkAuthState
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = string.IsNullOrEmpty(tokenResponse.RefreshToken) ? refreshToken : tokenResponse.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
        };

        return state;
    }

    private void RevokeToken(string token, string tokenType, IInlineInvokeProxy cph)
    {
        try
        {
            var config = LoadConfig(cph);

            var form = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("token", token),
                new KeyValuePair<string, string>("token_type_hint", tokenType)
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, RevokeUrl)
            {
                Content = new FormUrlEncodedContent(form)
            };

            string basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.clientId}:{config.clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

            using HttpResponseMessage response = _client.SendAsync(request).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("[VKVideoLive auth] Ошибка при отзыве токена: " + e.Message, e);
        }
    }

    private class VkTokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("token_type")]
        public string TokenType { get; set; }
    }
}

public class VkAuthState
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public bool IsAuthorized => !string.IsNullOrEmpty(AccessToken);
}

public class VkAuthWindow : Form
{
    private readonly VkOAuthService _service;
    private VkAuthState _state;
    private readonly IInlineInvokeProxy _cph;

    private readonly Label _statusText;
    private readonly Label _expiresText;
    private readonly Label _userLabel;
    private readonly PictureBox _avatarBox;
    private readonly Button _loginButton;
    private readonly Button _logoutButton;

    public VkAuthWindow(VkOAuthService service, VkAuthState initialState, IInlineInvokeProxy cph)
    {
        _service = service;
        _state = initialState ?? new VkAuthState();
        _cph = cph;

        Text = "VK Видео Live авторизация";
        Width = 420;
        Height = 220;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var statusLabel = new Label
        {
            Text = "Статус подключения:",
            AutoSize = true,
            Left = 12,
            Top = 12,
            Font = new Font(Font, FontStyle.Bold)
        };
        Controls.Add(statusLabel);

        _statusText = new Label
        {
            AutoSize = true,
            Left = 12,
            Top = statusLabel.Bottom + 4
        };
        Controls.Add(_statusText);

        _expiresText = new Label
        {
            AutoSize = true,
            Left = 12,
            Top = _statusText.Bottom + 4,
            ForeColor = Color.Gray
        };
        Controls.Add(_expiresText);

        _userLabel = new Label
        {
            AutoSize = true,
            Left = 12,
            Top = _expiresText.Bottom + 6
        };
        Controls.Add(_userLabel);

        _avatarBox = new PictureBox
        {
            Left = ClientSize.Width - 80,
            Top = 12,
            Width = 48,
            Height = 48,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.None
        };
        Controls.Add(_avatarBox);

        _loginButton = new Button
        {
            Text = "Login",
            Width = 90,
            Height = 28
        };
        _loginButton.Click += LoginButtonOnClick;

        _logoutButton = new Button
        {
            Text = "Logout",
            Width = 90,
            Height = 28
        };
        _logoutButton.Click += LogoutButtonOnClick;

        var closeButton = new Button
        {
            Text = "Закрыть",
            Width = 90,
            Height = 28
        };
        closeButton.Click += (sender, args) => Close();

        // Простейшая ручная раскладка снизу справа
        int buttonsTop = ClientSize.Height - 50;
        _loginButton.Left = ClientSize.Width - 3 * _loginButton.Width - 30;
        _loginButton.Top = buttonsTop;

        _logoutButton.Left = _loginButton.Right + 5;
        _logoutButton.Top = buttonsTop;

        closeButton.Left = _logoutButton.Right + 5;
        closeButton.Top = buttonsTop;

        Controls.Add(_loginButton);
        Controls.Add(_logoutButton);
        Controls.Add(closeButton);

        UpdateStatusUi();
    }

    private void UpdateStatusUi()
    {
        string nick = _cph.GetGlobalVar<string>(VkOAuthService.GlobalUserNickKey, true) ?? string.Empty;
        string avatarUrl = _cph.GetGlobalVar<string>(VkOAuthService.GlobalUserAvatarUrlKey, true) ?? string.Empty;

        if (_state != null && _state.IsAuthorized)
        {
            _statusText.Text = "Подключен (токен получен).";
            _statusText.ForeColor = Color.Green;

            if (_state.ExpiresAtUtc.HasValue)
            {
                _expiresText.Text = "Истекает: " + _state.ExpiresAtUtc.Value.ToLocalTime().ToString("g");
            }
            else
            {
                _expiresText.Text = string.Empty;
            }

            _loginButton.Enabled = true;
            _logoutButton.Enabled = true;
        }
        else
        {
            _statusText.Text = "Не авторизован.";
            _statusText.ForeColor = Color.Red;
            _expiresText.Text = string.Empty;

            _loginButton.Enabled = true;
            _logoutButton.Enabled = false;
        }

        if (!string.IsNullOrEmpty(nick))
            _userLabel.Text = "Пользователь: " + nick;
        else
            _userLabel.Text = string.Empty;

        if (!string.IsNullOrEmpty(avatarUrl))
        {
            try
            {
                using var webClient = new WebClient();
                byte[] data = webClient.DownloadData(avatarUrl);
                using var ms = new System.IO.MemoryStream(data);
                _avatarBox.Image = Image.FromStream(ms);
            }
            catch (Exception)
            {
                _avatarBox.Image = null;
            }
        }
        else
        {
            _avatarBox.Image = null;
        }
    }

    private void LoginButtonOnClick(object sender, EventArgs e)
    {
        try
        {
            _loginButton.Enabled = false;
            _logoutButton.Enabled = false;
            _statusText.Text = "Ожидание авторизации в браузере...";
            _statusText.ForeColor = Color.DarkOrange;

            _state = _service.StartLoginFlow(_cph);
            UpdateStatusUi();
        }
        catch (Exception ex)
        {
            _statusText.Text = "Ошибка авторизации: " + ex.Message;
            _statusText.ForeColor = Color.Red;
        }
        finally
        {
            _loginButton.Enabled = true;
            _logoutButton.Enabled = _state != null && _state.IsAuthorized;
        }
    }

    private void LogoutButtonOnClick(object sender, EventArgs e)
    {
        try
        {
            _service.Logout(_state, _cph);
            _state = new VkAuthState();
            UpdateStatusUi();
        }
        catch (Exception ex)
        {
            _statusText.Text = "Ошибка выхода: " + ex.Message;
            _statusText.ForeColor = Color.Red;
        }
    }
}