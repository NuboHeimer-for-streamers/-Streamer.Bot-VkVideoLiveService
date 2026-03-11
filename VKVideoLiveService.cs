///----------------------------------------------------------------------------
///   Module:       VK Video Live Service
///   Author:       play_code (https://twitch.tv/play_code)
///   Refactored:   NuboHeimer (https://live.vkvideo.ru/nuboheimer)
///   Email:        info@play-code.live
///   WebSite:		https://docs.play-code.ru/minichat
///----------------------------------------------------------------------------

///----------------------------------------------------------------------------
///   Module:       GetNewViewers, RewardsManager, GetSeasonStatistics
///   Author:       NuboHeimer (https://live.vkvideo.ru/nuboheimer)
///   Email:        nuboheimer@yandex.ru
///   Help:         https://t.me/nuboheimersb/29
///----------------------------------------------------------------------------

///   Version:      3.2.0
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using System.Drawing;

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

    private readonly HttpClient Client = new();
    private Logger Logger;
    private VKVideoLiveApiService Service;
    private VkOAuthService VkAuthService;
    public void Init()
    {
        Logger = new Logger(CPH, "-- VKVideoLive Service:");
        Service = new VKVideoLiveApiService(Client, Logger);
        VkAuthService = new VkOAuthService(Client, Logger);

        if (CPH.GetGlobalVar<List<string>>(VkLiveTodaysViewersKey, true) == null)
            CPH.SetGlobalVar(VkLiveTodaysViewersKey, new List<string>(), true);
        if (CPH.GetGlobalVar<HashSet<string>>(VkLivePreviousPresentViewersKey, true) == null)
            CPH.SetGlobalVar(VkLivePreviousPresentViewersKey, new HashSet<string>(), true);

        if (CPH.GetGlobalVar<string>(VkLiveAuthAccessTokenKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthAccessTokenKey, string.Empty, true);
        if (CPH.GetGlobalVar<string>(VkLiveAuthRefreshTokenKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthRefreshTokenKey, string.Empty, true);
        if (CPH.GetGlobalVar<string>(VkLiveAuthExpiresAtUtcKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthExpiresAtUtcKey, string.Empty, true);

        if (CPH.GetGlobalVar<string>(VkLiveAuthClientIdKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthClientIdKey, string.Empty, true);
        if (CPH.GetGlobalVar<string>(VkLiveAuthClientSecretKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthClientSecretKey, string.Empty, true);
        if (CPH.GetGlobalVar<string>(VkLiveAuthRedirectUriKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthRedirectUriKey, string.Empty, true);
        if (CPH.GetGlobalVar<string>(VkLiveAuthUserNickKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthUserNickKey, string.Empty, true);
        if (CPH.GetGlobalVar<string>(VkLiveAuthUserAvatarUrlKey, true) == null)
            CPH.SetGlobalVar(VkLiveAuthUserAvatarUrlKey, string.Empty, true);

        if (CPH.GetGlobalVar<Dictionary<string, string>>(VkLiveRewardsCacheKey, true) == null)
            CPH.SetGlobalVar(VkLiveRewardsCacheKey, new Dictionary<string, string>(), true);

        CPH.RegisterCustomTrigger("Present Viewers (VkLive)", "VKVideoLive_PresentViewers", new[] { "VK Video Live" });
    }

    public bool ClearTodaysViewers()
    {
        CPH.SetGlobalVar(VkLiveTodaysViewersKey, new List<string>(), true);
        return true;
    }

    public bool ClearPreviousPresentViewers()
    {
        CPH.SetGlobalVar(VkLivePreviousPresentViewersKey, new HashSet<string>(), true);
        return true;
    }

    public bool OnReward()
    {
        try
        {
            if (!args.ContainsKey("channel_name"))
                return false;

            if (!args.ContainsKey("rewardName"))
            {
                Logger.Error("[VKVideoLive reward manager] Аргумент rewardName не передан.");
                return false;
            }

            string channelName = args["channel_name"].ToString();
            string rewardName = args["rewardName"].ToString();

            if (string.IsNullOrWhiteSpace(channelName))
            {
                Logger.Error("[VKVideoLive reward manager] Значение channel_name пустое.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(rewardName))
            {
                Logger.Error("[VKVideoLive reward manager] Значение rewardName пустое.");
                return false;
            }

            var authState = EnsureValidDevApiAuth(CPH);
            if (authState == null)
                return false;

            string rewardId = ResolveRewardIdByName(channelName, rewardName, authState.AccessToken);
            if (string.IsNullOrEmpty(rewardId))
            {
                Logger.Error("[VKVideoLive reward manager] Награда с именем \"" + rewardName + "\" не найдена ни в кэше, ни после обновления manage_info.");
                return false;
            }

            Service.EnableRewardDev(channelName, rewardId, authState.AccessToken);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive reward manager] Ошибка при включении награды по имени", e.Message);
            return false;
        }
    }

    private string ResolveRewardIdByName(string channelName, string rewardName, string accessToken)
    {
        var cache = CPH.GetGlobalVar<Dictionary<string, string>>(VkLiveRewardsCacheKey, true)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (cache.TryGetValue(rewardName, out string rewardId) && !string.IsNullOrEmpty(rewardId))
            return rewardId;

        var channelPoints = Service.GetChannelPoints(channelName, accessToken);
        if (channelPoints == null || channelPoints.Rewards == null || channelPoints.Rewards.Count == 0)
            return null;

        var updatedCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reward in channelPoints.Rewards)
        {
            if (!string.IsNullOrWhiteSpace(reward.Name) && !string.IsNullOrWhiteSpace(reward.Id))
                updatedCache[reward.Name] = reward.Id;
        }

        CPH.SetGlobalVar(VkLiveRewardsCacheKey, updatedCache, true);

        if (updatedCache.TryGetValue(rewardName, out rewardId) && !string.IsNullOrEmpty(rewardId))
            return rewardId;

        return null;
    }

    public bool OffReward()
    {
        try
        {
            if (!args.ContainsKey("channel_name"))
                return false;

            if (!args.ContainsKey("rewardName"))
            {
                Logger.Error("[VKVideoLive reward manager] Аргумент rewardName не передан.");
                return false;
            }

            string channelName = args["channel_name"].ToString();
            string rewardName = args["rewardName"].ToString();

            if (string.IsNullOrWhiteSpace(channelName))
            {
                Logger.Error("[VKVideoLive reward manager] Значение channel_name пустое.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(rewardName))
            {
                Logger.Error("[VKVideoLive reward manager] Значение rewardName пустое.");
                return false;
            }

            var authState = EnsureValidDevApiAuth(CPH);
            if (authState == null)
                return false;

            string rewardId = ResolveRewardIdByName(channelName, rewardName, authState.AccessToken);
            if (string.IsNullOrEmpty(rewardId))
            {
                Logger.Error("[VKVideoLive reward manager] Награда с именем \"" + rewardName + "\" не найдена ни в кэше, ни после обновления manage_info.");
                return false;
            }

            Service.DisableRewardDev(channelName, rewardId, authState.AccessToken);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive reward manager] Ошибка при выключении награды по имени", e.Message);
            return false;
        }
    }

    public bool SongRequest()
    {
        if (!args.ContainsKey("channel_name"))
            return false;

        if (!args.ContainsKey("rewardName"))
        {
            Logger.Error("[VKVideoLive song request] Аргумент rewardName не передан.");
            return false;
        }

        if (!args.ContainsKey("minichat.Data.MessageKit.1.Data.URL"))
        {
            Logger.Error("[VKVideoLive song request] Аргумент URL не передан.");
            return false;
        }

        string channelName = args["channel_name"].ToString();
        string rewardName = args["rewardName"].ToString();
        string videoUrl = args["minichat.Data.MessageKit.1.Data.URL"].ToString();

        if (string.IsNullOrWhiteSpace(channelName))
        {
            Logger.Error("[VKVideoLive song request] Значение channel_name пустое.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(rewardName))
        {
            Logger.Error("[VKVideoLive song request] Значение rewardName пустое.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            Logger.Error("[VKVideoLive song request] Значение URL пустое.");
            return false;
        }

        try
        {
            var authState = EnsureValidDevApiAuth(CPH);
            if (authState == null)
                return false;

            string rewardId = ResolveRewardIdByName(channelName, rewardName, authState.AccessToken);
            if (string.IsNullOrEmpty(rewardId))
            {
                Logger.Error("[VKVideoLive song request] Награда с именем \"" + rewardName + "\" не найдена ни в кэше, ни после обновления manage_info.");
                return false;
            }

            Service.ActivateRewardDev(channelName, rewardId, authState.AccessToken, videoUrl);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive song request] Ошибка при покупке награды по имени", e.Message);
            return false;
        }
    }

    public bool GetRewardsForManage()
    {
        return GetRewardsForManageInternal(CPH);
    }

    private bool GetRewardsForManageInternal(IInlineInvokeProxy cph)
    {
        try
        {
            if (!cph.TryGetArg("channel_name", out object channelNameObj) || channelNameObj == null)
            {
                Logger.Error("[VKVideoLive points] Аргумент channel_name не передан.");
                return false;
            }

            string channelName = channelNameObj.ToString();
            if (string.IsNullOrWhiteSpace(channelName))
            {
                Logger.Error("[VKVideoLive points] Значение channel_name пустое.");
                return false;
            }

            var authState = EnsureValidDevApiAuth(cph);
            if (authState == null)
                return false;

            var channelPoints = Service.GetChannelPoints(channelName, authState.AccessToken);
            if (channelPoints == null || channelPoints.Rewards == null || channelPoints.Rewards.Count == 0)
            {
                Logger.Debug("[VKVideoLive points] Награды для управления не найдены.");
                return true;
            }

             var rewardsCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var reward in channelPoints.Rewards)
            {
                bool isEnabled = !reward.IsDisabled;

                if (!string.IsNullOrWhiteSpace(reward.Name) && !string.IsNullOrWhiteSpace(reward.Id))
                {
                    rewardsCache[reward.Name] = reward.Id;
                }
            }

            cph.SetGlobalVar(VkLiveRewardsCacheKey, rewardsCache, true);

            return true;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive points] Ошибка при получении списка наград для управления", e.Message);
            return false;
        }
    }

    public bool GetViewers()
    {
        if (!args.ContainsKey("channel_name"))
            return false;

        string channelName = args["channel_name"].ToString();
        List<Dictionary<string, object>> listOfViewers = new List<Dictionary<string, object>>();

        try
        {
            var authState = EnsureValidDevApiAuth(CPH);
            if (authState == null)
                return false;

            var viewers = Service.GetChatMembers(channelName, authState.AccessToken, 200);

            for (int i = 0; i < viewers.Count; i++)
            {
                Dictionary<string, object> user = new Dictionary<string, object> {

                    { "userName", viewers[i].DisplayName },
                    { "id", viewers[i].ID },
                };
                listOfViewers.Add(user);
            }
            CPH.SetArgument("users", listOfViewers);
            CPH.SetArgument("viewers_count", viewers.Count);
            CPH.SetArgument("isLive", true);
            CPH.SetArgument("isTest", false);
            CPH.TriggerCodeEvent("VKVideoLive_PresentViewers", true);
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive get viewers] Error fetching viewers list", e.Message);
        }
        return true;
    }

    public bool GetRandomViewer()
    {
        if (!args.ContainsKey("channel_name"))
            return false;

        string channelName = args["channel_name"].ToString();
        string last_random_viewer = CPH.GetGlobalVar<string>(VkLiveLastRandomViewerKey, true);

        try
        {
            var authState = EnsureValidDevApiAuth(CPH);
            if (authState == null)
                return false;

            var viewers = Service.GetChatMembers(channelName, authState.AccessToken, 200);

            if (viewers.Count == 0)
            {
                CPH.SetArgument("viewer", "");
                return true;
            }

            var rnd = new Random();
            var viewer = viewers[rnd.Next(viewers.Count)];

            if (viewers.Count > 1)
            {
                while (viewer.DisplayName.Equals(last_random_viewer))
                {
                    viewers.Remove(viewer);
                    viewer = viewers[rnd.Next(viewers.Count)];
                }
            }

            CPH.SetArgument("viewer", viewer.DisplayName);
            CPH.SetGlobalVar(VkLiveLastRandomViewerKey, viewer.DisplayName, true);
        }
        catch (Exception e)
        {
            Logger.Error("Error fetching viewers list", e.Message);
        }

        return true;
    }

    public bool GetViewersCount()
    {
        if (!args.ContainsKey("channel_name"))
            return false;

        string channelName = args["channel_name"].ToString();

        try
        {
            var authState = EnsureValidDevApiAuth(CPH);
            if (authState == null)
                return false;

            int viewers = Service.GetChannelViewersCount(channelName, authState.AccessToken);
            CPH.SetArgument("viewers_count", viewers);
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive get viewers count] Error fetching viewers count", e.Message);
            return false;
        }
        return true;
    }

    public bool GetInOutViewers()
    {
        return GetInOutViewers(CPH);
    }

    private bool GetInOutViewers(IInlineInvokeProxy cph)
    {
        try
        {
            var previousPresent = cph.GetGlobalVar<HashSet<string>>(VkLivePreviousPresentViewersKey, true) ?? new HashSet<string>();
            var todaysViewers = cph.GetGlobalVar<List<string>>(VkLiveTodaysViewersKey, true);

            if (!cph.TryGetArg("users", out object usersObj))
            {
                Logger.Debug("[VKVideoLive GetInOutViewers] Аргумент users отсутствует.");
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
                    CreateViewerEvent(cph, name, "Обнаружен(а) впервые на текущей трансляции.");
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
                    CreateViewerEvent(cph, name, "Обнаружен(а) в списке зрителей.");
            }

            foreach (var name in previousPresent)
            {
                if (!currentNames.Contains(name))
                    CreateViewerEvent(cph, name, "Пропал(а) из списка зрителей.");
            }

            cph.SetGlobalVar(VkLivePreviousPresentViewersKey, new HashSet<string>(currentNamesForSaving), true);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive GetInOutViewers] Ошибка", e.Message);
            return false;
        }
    }

    private void CreateViewerEvent(IInlineInvokeProxy cph, string displayName, string messageText)
    {
        cph.SetArgument("service", "VKVideoLive");
        cph.SetArgument("title", displayName);
        cph.SetArgument("message", messageText);
        cph.ExecuteMethod("MiniChat Method Collection", "CreateCustomEvent");
        Thread.Sleep(200);
    }

    public bool GetNewViewers()
    {
        if (!CPH.TryGetArg("users", out List<Dictionary<string, object>> users) || users == null || users.Count == 0)
        {
            CPH.LogInfo("Список зрителей пуст или аргумент users не передан. Вызовите Get Viewers или привяжите экшен к триггеру Present Viewers (VK Video Live).");
            return true;
        }

        List<string> vkvideolive_todays_viewers = CPH.GetGlobalVar<List<string>>(VkLiveTodaysViewersKey, true);

        try
        {
            CPH.LogInfo("Попытка получить нового зрителя на вкпл");
            for (int i = 0; i < users.Count; i++)
            {
                string displayName = users[i].ContainsKey("userName") ? users[i]["userName"]?.ToString() : null;
                if (string.IsNullOrEmpty(displayName))
                    continue;
                if (vkvideolive_todays_viewers.Contains(displayName))
                    continue;

                vkvideolive_todays_viewers.Add(displayName);
                CPH.SetGlobalVar(VkLiveTodaysViewersKey, vkvideolive_todays_viewers, true);
                CPH.SetArgument("service", "VKVideoLive");
                CPH.SetArgument("title", "Новый зритель");
                CPH.SetArgument("message", displayName);
                CPH.ExecuteMethod("MiniChat Method Collection", "CreateCustomEvent");
                CPH.LogInfo("Новый зритель: " + displayName);
                Thread.Sleep(200); // Без задержки лента миничата пропускает часть событий.
            }
        }
        catch (Exception e)
        {
            Logger.Error("Error fetching new viewer", e.Message);
        }

        return true;
    }

    public bool AddFirstWordViewer()
    {
        List<string> vkvideolive_todays_viewers = CPH.GetGlobalVar<List<string>>(VkLiveTodaysViewersKey, true);
        vkvideolive_todays_viewers.Add(args["userName"].ToString());
        CPH.SetGlobalVar(VkLiveTodaysViewersKey, vkvideolive_todays_viewers, true);
        return true;
    }

    public bool GetTotalAverageViewrs()
    {
        string channelName = args["channel_name"].ToString();
        string token = args["token"].ToString();
        var json = Service.GetAllStatistics(channelName, token);
        JObject parsedJson = JObject.Parse(json);
        int totalAverageVKVideoLiveViewers = parsedJson["data"]["analytics"]["total"]["viewersAverage"].Value<int>();
        CPH.SetArgument("totalAverageVKVideoLiveViewers", totalAverageVKVideoLiveViewers);
        return true;
    }

    public bool VkAuthShowUi()
    {
        return VkAuthShowUiInternal(CPH);
    }

    public bool VkAuthRefresh()
    {
        return VkAuthRefreshInternal(CPH);
    }

    private bool VkAuthShowUiInternal(IInlineInvokeProxy cph)
    {
        try
        {
            if (VkAuthService == null)
                VkAuthService = new VkOAuthService(Client, Logger);

            var authState = VkAuthService.LoadStateFromGlobals(cph);

            var window = new VkAuthWindow(VkAuthService, authState, cph);
            window.ShowDialog();

            return true;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive auth] Ошибка при показе окна авторизации", e.Message);
            return false;
        }
    }

    private bool VkAuthRefreshInternal(IInlineInvokeProxy cph)
    {
        try
        {
            if (VkAuthService == null)
                VkAuthService = new VkOAuthService(Client, Logger);

            var state = VkAuthService.LoadStateFromGlobals(cph);
            if (string.IsNullOrEmpty(state.RefreshToken))
            {
                Logger.Error("[VKVideoLive auth] Нельзя обновить токен: refresh_token отсутствует");
                return false;
            }

            state = VkAuthService.RefreshTokens(state, cph);
            VkAuthService.SaveStateToGlobals(state, cph);
            return true;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive auth] Ошибка при обновлении авторизации", e.Message);
            return false;
        }
    }

    private VkAuthState EnsureValidDevApiAuth(IInlineInvokeProxy cph)
    {
        try
        {
            if (VkAuthService == null)
                VkAuthService = new VkOAuthService(Client, Logger);

            var state = VkAuthService.LoadStateFromGlobals(cph);
            if (state == null || string.IsNullOrEmpty(state.AccessToken))
            {
                Logger.Error("[VKVideoLive auth] Нет access_token для DevAPI. Выполните первоначальную авторизацию.");
                return null;
            }

            if (state.ExpiresAtUtc.HasValue)
            {
                var now = DateTime.UtcNow;
                // Обновляем токен, если он уже истёк или истекает в ближайшую минуту
                if (state.ExpiresAtUtc.Value <= now.AddMinutes(1))
                {
                    state = VkAuthService.RefreshTokens(state, cph);
                    VkAuthService.SaveStateToGlobals(state, cph);
                }
            }

            return state;
        }
        catch (Exception e)
        {
            Logger.Error("[VKVideoLive auth] Не удалось проверить/обновить токен DevAPI", e.Message);
            return null;
        }
    }
}

public class VKVideoLiveApiService
{
    internal HttpClient Client { get; set; }
    internal Logger Logger { get; set; }
    // Public/browser-facing API host (used for existing viewer/reward endpoints)
    internal const string ServiceBrowserApiHost = "https://api.live.vkvideo.ru/v1";
    // Official DevAPI host (to be used for OAuth/DevAPI-specific calls)
    internal const string ServiceOfficialApiHost = "https://apidev.live.vkvideo.ru/v1";
    private const string EndpointTplGetUserData = "/blog/{0}/public_video_stream/chat/user/";
    private const string EndpointSetRewardState = "/channel/{0}/manage/point/reward/{1}/enabled";
    private const string EndpointChannelPoints = "/channel_point/rewards/manage_info";
    private const string EndpointRewardEnableDev = "/channel_point/reward/enable";
    private const string EndpointRewardDisableDev = "/channel_point/reward/disable";
    private const string EndpointRewardActivateDev = "/channel_point/reward/activate";
    private const string EndpointGetSeasonStatistics = "/channel/{0}/support_program/season/{1}/statistic/{2}/daily/";
    private const string EndpointAllStatistics = "/channel/{0}/analytics?aggregate_interval=day&date_interval=30day";
    private const string EndpointSongRequest = "/channel/{0}/stream/slot/default/point/reward/{1}/activate";
    //    private const string EndpointGetSeasonDaysOnAir = "days_on_air/daily/";
    //    private const string EndpointGetSeasonRaidMembers = "raid_members/daily/";
    //    private const string EndpointGetSeasonViewTimes = "view_time/daily/";
    //    private const string EndpointGetSeasonChPRewardActivate = "cp_reward_activate/daily/";
    //    private const string EndpointGetSeasonLikes = "like/daily/";
    //    private const string EndpointGetSeasonTotalProfit= "total_profit/daily/";
    public VKVideoLiveApiService(HttpClient client, Logger logger)
    {
        Client = client;
        Logger = logger;
    }

    private UserInfoResponse GetUserDataAsync(string channelName)
    {
        string url = string.Format(ServiceBrowserApiHost + EndpointTplGetUserData, channelName);
        try
        {
            using HttpResponseMessage response = Client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<Dictionary<string, UserInfoResponse>>(responseBody)["data"] ?? null;
        }
        catch (HttpRequestException e)
        {
            Logger.Error("Error from client", e.Message);
            return null;
        }
    }

    public List<UserData> GetViewers(string channelName)
    {
        var userData = GetUserDataAsync(channelName);
        if (userData == null)
            return new();
        var users = userData.Users;
        foreach (var moderator in userData.Moderators)
            users.Add(moderator);
        return users;
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
            Logger.Error("[VKVideoLive channel] Error fetching channel info for viewers count", e.Message);
            return 0;
        }
    }

    public List<UserData> GetChatMembers(string channelUrl, string token, int limit)
    {
        if (limit <= 0 || limit > 200)
            limit = 200;

        string url = ServiceOfficialApiHost
                     + "/chat/members?channel_url=" + Uri.EscapeDataString(channelUrl)
                     + "&limit=" + limit.ToString();
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = Client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            var root = JsonConvert.DeserializeObject<ChatMembersRootResponse>(responseBody);
            return root?.Data?.Members ?? new List<UserData>();
        }
        catch (HttpRequestException e)
        {
            Logger.Error("[VKVideoLive chat] Error fetching chat members", e.Message);
            return new List<UserData>();
        }
    }

    public void ChangeRewardState(string channelName, string rewardId, string rewardState, string token)
    {
        string url = string.Format(ServiceBrowserApiHost + EndpointSetRewardState, channelName, rewardId);
        string stub = "";
        string jsonString = JsonConvert.SerializeObject(stub);
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpContent httpContent = new StringContent(jsonString);
            if (rewardState == "On")
            {
                using HttpResponseMessage response = Client.PutAsync(url, httpContent).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }

            if (rewardState == "Off")
            {
                using HttpResponseMessage response = Client.DeleteAsync(url).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }
        }
        catch (HttpRequestException e)
        {
            Logger.Error("Error from client", e.Message);
        }
    }

    public void SendSongRequest(string channelName, string rewardId, string token, string videoUrl)
    {
        string url = string.Format(ServiceBrowserApiHost + EndpointSongRequest, channelName, rewardId);
        string contentJson = "[\"" + videoUrl + "\",\"unstyled\",[]]";
        var requestBody = new List<object>
        {
            new
            {
                type = "link",
                content = contentJson,
                url = videoUrl
            },
            new
            {
                type = "text",
                content = "",
                modificator = "BLOCK_END"
            }
        };
        string jsonString = JsonConvert.SerializeObject(requestBody);
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var formData = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("text", jsonString)
            };
            using HttpContent httpContent = new FormUrlEncodedContent(formData);
            using HttpResponseMessage response = Client.PutAsync(url, httpContent).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (HttpRequestException e)
        {
            Logger.Error("Error from client", e.Message);
        }
    }

    public string GetSeasonStatistics(string channelName, string seasonNumber, string requestType, string token)
    {
        string response = "stub";
        return response;
    }

    public string GetAllStatistics(string channelName, string token)
    {
        string url = string.Format(ServiceBrowserApiHost + EndpointAllStatistics, channelName);
        try
        {
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = Client.GetAsync(url).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (HttpRequestException e)
        {
            Logger.Error("Error from client", e.Message);
            return ("Error from client");
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
            Logger.Error("[VKVideoLive points] Error fetching channel points", e.Message);
            return null;
        }
    }

    public void EnableRewardDev(string channelUrl, string rewardId, string token)
    {
        string url = ServiceOfficialApiHost
                     + EndpointRewardEnableDev
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
            Logger.Error("[VKVideoLive points] Error enabling reward via DevAPI", e.Message);
        }
    }

    public void DisableRewardDev(string channelUrl, string rewardId, string token)
    {
        string url = ServiceOfficialApiHost
                     + EndpointRewardDisableDev
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
            Logger.Error("[VKVideoLive points] Error disabling reward via DevAPI", e.Message);
        }
    }

    public void ActivateRewardDev(string channelUrl, string rewardId, string token, string videoUrl)
    {
        string url = ServiceOfficialApiHost + EndpointRewardActivateDev;

        var body = new
        {
            reward = new
            {
                id = rewardId,
                message = new
                {
                    parts = new[]
                    {
                        new
                        {
                            link = new
                            {
                                content = videoUrl,
                                url = videoUrl
                            }
                        }
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
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            Logger.Error("[VKVideoLive points] Error activating reward via DevAPI", e.Message);
        }
    }

    public class UserInfoResponse
    {
        [JsonProperty("owner")]
        public UserData Owner { get; set; }

        [JsonProperty("moderators")]
        public List<UserData> Moderators { get; set; }

        [JsonProperty("users")]
        public List<UserData> Users { get; set; }

        [JsonProperty("count")]
        public UserInfoCounts Count { get; set; }
    }

    public class UserInfoCounts
    {
        public int permanentBans;
        public int moderators;
        public int temporaryBans;
        public int users;
    }

    public class UserData
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("id")]
        public int ID { get; set; }

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; }

        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>
            {
                {
                    "displayName",
                    DisplayName
                },
                {
                    "id",
                    ID.ToString()
                },
                {
                    "avatarUrl",
                    AvatarUrl
                },
            };
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

    public class ChatMembersRootResponse
    {
        [JsonProperty("data")]
        public ChatMembersData Data { get; set; }
    }

    public class ChatMembersData
    {
        [JsonProperty("members")]
        public List<UserData> Members { get; set; } = new List<UserData>();
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
}

public class VkOAuthService
{
    private readonly HttpClient _client;
    private readonly Logger _logger;

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

    public VkOAuthService(HttpClient client, Logger logger)
    {
        _client = client;
        _logger = logger;
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
            _logger.Error("[VKVideoLive auth] Не заданы client_id, client_secret или redirect_uri в глобальных переменных Streamer.bot");
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
            _logger.Error("[VKVideoLive auth] Не удалось получить код авторизации");
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
            _logger.Error("[VKVideoLive auth] RefreshToken пуст, обновление невозможно");
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
                _logger.Error("[VKVideoLive auth] Ошибка получения профиля current_user",
                    (int)response.StatusCode, response.StatusCode, body);
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
            _logger.Error("[VKVideoLive auth] Не удалось обновить информацию о пользователе (current_user)", e.Message);
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
            _logger.Error("[VKVideoLive auth] Ошибка при отзыве токенов", e.Message);
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
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception e)
        {
            _logger.Error("[VKVideoLive auth] Не удалось открыть браузер", e.Message);
            throw;
        }
    }

    private string WaitForAuthorizationCode(string redirectUri, string expectedState)
    {
        try
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
                _logger.Error("[VKVideoLive auth] Значение state не совпало");
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
        catch (Exception e)
        {
            _logger.Error("[VKVideoLive auth] Ошибка при ожидании кода авторизации", e.Message);
            return null;
        }
    }

    private VkAuthState ExchangeCodeForTokens(string code, string clientId, string clientSecret, string redirectUri)
    {
        try
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
                _logger.Error("[VKVideoLive auth] Ошибка обмена кода на токен. StatusCode: " + (int)response.StatusCode, response.StatusCode, responseBody);
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
        catch (Exception e)
        {
            _logger.Error("[VKVideoLive auth] Ошибка при обмене кода на токен", e.Message);
            throw;
        }
    }

    private VkAuthState RefreshTokensInternal(string refreshToken, string clientId, string clientSecret)
    {
        try
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
                _logger.Error("[VKVideoLive auth] Ошибка обновления токенов. StatusCode: " + (int)response.StatusCode, response.StatusCode, responseBody);
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
        catch (Exception e)
        {
            _logger.Error("[VKVideoLive auth] Ошибка при обновлении токенов", e.Message);
            throw;
        }
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
            _logger.Error("[VKVideoLive auth] Ошибка при отзыве токена", e.Message);
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

public class Logger
{
    private IInlineInvokeProxy cph { get; set; }
    private string Prefix { get; set; }

    public Logger(IInlineInvokeProxy _CPH, string prefix)
    {
        cph = _CPH;
        Prefix = prefix;
    }

    public void WebError(WebException e)
    {
        var response = (HttpWebResponse)e.Response;
        var statusCodeResponse = response.StatusCode;
        int statusCodeResponseAsInt = ((int)response.StatusCode);
        Error("WebException with status code " + statusCodeResponseAsInt.ToString(), statusCodeResponse);
    }

    public void Error(string message)
    {
        message = string.Format("{0} {1}", Prefix, message);
        cph.LogWarn(message);
    }

    public void Error(string message, params object[] additional)
    {
        string finalMessage = message;
        foreach (var line in additional)
        {
            finalMessage += ", " + line;
        }

        Error(finalMessage);
    }

    public void Debug(string message)
    {
        message = string.Format("{0} {1}", Prefix, message);
        cph.LogDebug(message);
    }

    public void Debug(string message, params object[] additional)
    {
        string finalMessage = message;
        foreach (var line in additional)
        {
            finalMessage += ", " + line;
        }

        Debug(finalMessage);
    }
}