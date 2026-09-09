---
title: "Использование"
weight: 3
type: "docs"
description: "Руководство по настройке VkLiveService: создание приложения VK, авторизация в Streamer.bot и использование экшенов."
slug: "usage"
url: "usage/"
---

## Disclaimer

Создание приложения VK и авторизация в Streamer.bot описаны ниже и **не требуют** MiniChat. Приход/уход зрителей идут через кастомные триггеры Streamer.bot; MiniChat нужен только если вы сами повесите на эти триггеры (или на другие события) отправку в MiniChat.

## Создание приложения VK

Для работы этого модуля вам необходимо создать своё приложение в VK.
1. Переходим по ссылке [dev.live.vkvideo.ru/apps](https://dev.live.vkvideo.ru/apps) и нажимаем **Войти** справа вверху.

{{< img class="center" src="/images/usage/SignIn.png" alt="Вход в кабинет разработчика VK Video Live" >}}

2. Нажимаем **Создать приложение**:

{{< img class="center" src="/images/usage/CreateButton.png" alt="Кнопка «Создать приложение»" >}}

- Называем приложение как нам хочется.
- Добавляем описание.
- При желании загружаем иконку.
- URL для web-push оставляем пустым.
- В последнее поле вставляем `http://localhost:5000/vkvideoliveredirecturi`
- Нажимаем **Создать**.

{{< img class="center" src="/images/usage/NewApp.png" alt="Форма создания приложения" >}}

3. Записываем id приложения, секретный и публичный ключи. Они нам ещё пригодятся.
4. Нажимаем **Хорошо**.


## Настройка модуля и стримербота.
1. Находим экшен `[VKVideoLive] Set channelName`
2. В `Set argument` в **Sub-Actions** вписываем название своего канала так, как оно отображается в адресной строке браузера.  

{{< img class="center" src="/images/usage/ChannelName.png" alt="Экшен Set channelName, аргумент channel_name" >}}

3. Находим экшен `[VKVideoLive] Login`
4. В **Sub-Actions** заполняем ранее сохранённые значения:
- `VkLiveAuthClientId` -> ID приложения
- `VkLiveAuthClientSecret` -> Секретный ключ приложения
- `VkLiveAuthRedirectUri` -> URL для редиректа

{{< img class="center" src="/images/usage/Login.png" alt="Экшен Login, sub-actions с VkLiveAuthClientId и др." >}}

5. Запускаем тестовый триггер.  

{{< img class="center" src="/images/usage/TestTrigger.png" alt="Запуск тестового триггера для окна авторизации" >}}

6. В открывшемся окне нажимаем **Login**.  

{{< img class="center" src="/images/usage/Login2.png" alt="Окно авторизации VK Video Live, кнопка Login" >}}

7. Вас перебросит в браузер по умолчанию и попросит подтвердить разрешения приложения.
- Нажимаем **Разрешить**.
- Видим в браузере сообщение об успешной авторизации.
- В окне логина появится статус успешного подключения, после этого окно можно закрывать.

{{< img class="center" src="/images/usage/Login3.png" alt="Браузер, успешная авторизация приложения" >}}

## Работа с экшенами.

### Общие рекомендации по триггерам

>⚠ Важно
>
> Для корректной очистки списков и избежания ошибок в определении зрителей, используйте триггер: **OBS** -> **Streaming Started** или привязанную к запуску стрима горячую клавишу.

### \[VKVideoLive] Clear Previous Present Viewers.

Экшен очищает сохранённый список **Present Viewers**, чтобы исключить устаревшие данные перед началом трансляции.  

{{< img class="center" src="/images/usage/VkLive_Clear_Previous_Present_Viewers.png" alt="Экшен Clear Previous Present Viewers" >}}

- Очищайте список перед запуском стрима, иначе в нём могут храниться устаревшие данные и зрители будут определяться неверно. Рекомендации по триггеру на очистку смотрите в блоке "Важно" выше.

### \[VKVideoLive] Clear Todays Viewers.

Экшен очищает сохранённый список сегодняшних зрителей для корректного определения новых зрителей текущей трансляции.  

{{< img class="center" src="/images/usage/VkLive_Clear_Todays_Viewers.png" alt="Экшен Clear Todays Viewers" >}}

- Очищайте список перед запуском стрима, иначе в нём могут храниться устаревшие данные и зрители будут определяться неверно. Рекомендации по триггеру на очистку смотрите в блоке "Важно" выше.

### \[VKVideoLive] ActivateReward.

Экшен позволяет активировать любую награду канала от вашего имени.  

{{< img class="center" src="/images/usage/VkLiveActivateReward.png" alt="Экшен Activate Reward" >}}

Для работы экшена необходимо создать новый экшен, в нём задать аргументы:
- `rewardName` -- название награды как оно отображается для зрителя на сайте.
- `rewardText` -- текст награды, если она требует ввод текста.
После чего вызвать сам экшен **\[VKVideoLive] ActivateReward** через Run Action. 

Пример:

{{< img class="center" src="/images/usage/VkLiveExampleActivateReward.png" alt="Пример Run Action для Activate Reward" >}}

### \[VKVideoLive] AddFirstWordViewer

Экшен добавляет зрителя, впервые за трансляцию написавшего в чат, в список зрителей `VkLiveTodaysViewers`. Это позволяет не срабатывать триггеру **Viewer First Today (VkLive)** для уже увиденных в чате зрителей.

{{< img class="center" src="/images/usage/VkLive_Add_First_Words_Viewer.png" alt="Экшен Add First Word Viewer" >}}

- Триггер: **Custom** -> **MiniChat** -> **VkVideoLive** -> **First Words**
- Используйте, если не хотите событие «первый раз за трансляцию» для зрителей, которые уже написали в чат.

### \[VKVideoLive] Code

Служебный экшен с кодом. Также в нём можно узнать текущую версию (указана в комментарии в сабэкшенах и в самом коде).  

{{< img class="center" src="/images/usage/VkLiveVersion.png" alt="Экшен Code, версия скрипта" >}}


### \[VKVideoLive] Get In Out Viewers

Экшен сравнивает текущий список зрителей с предыдущим и вызывает кастомные триггеры прихода/ухода. В MiniChat сам ничего не отправляет — при необходимости повесьте на триггеры свой action (например MiniChat `CreateCustomEvent`).

{{< img class="center" src="/images/usage/VkLive_Get_In_Out_Viewers.png" alt="Экшен Get In Out Viewers" >}}

- Триггер экшена: **Custom** -> **Vk Video Live** -> **Present Viewers (VkLive)**.
- События (отдельные триггеры, аргумент `%userName%`):
  - **Viewer First Today (VkLive)** (`VKVideoLive_ViewerFirstToday`) — впервые за текущую трансляцию;
  - **Viewer Joined (VkLive)** (`VKVideoLive_ViewerJoined`) — снова появился в списке зрителей;
  - **Viewer Left (VkLive)** (`VKVideoLive_ViewerLeft`) — пропал из списка зрителей.
- Примечания: требует корректной настройки экшена **\[VKVideoLive] Get Viewers** и предварительной очистки списков перед началом трансляции.

### \[VKVideoLive] Get New Viewers

Экшен вызывает триггер **Viewer First Today (VkLive)** для зрителей, впервые попавших в список за текущую трансляцию. По умолчанию выключен. Если используете **Get In Out Viewers**, оставьте выключенным (иначе дублирование First Today).

{{< img class="center" src="/images/usage/VkLive_Get_New_Viewers.png" alt="Экшен Get New Viewers" >}}

- Триггер экшена: **Custom** -> **Vk Video Live** -> **Present Viewers (VkLive)**.
- Событие: **Custom** -> **Vk Video Live** -> **Viewer First Today (VkLive)** (`%userName%`).
- Примечания: требует корректной настройки экшена **\[VKVideoLive] Get Viewers** и предварительной очистки списков перед началом трансляции.

### \[VKVideoLive] Get Random Viewer

Экшен получает одного случайного зрителя и записывает его имя в аргумент `randomUserName0`.

{{< img class="center" src="/images/usage/VkLive_Random_Viewer.png" alt="Экшен Get Random Viewer" >}}

### \[VKVideoLive] Get Viewer Info

Экшен запрашивает у **официального** API детальные данные одного зрителя (`GET /chat/member`).

- Аргументы:
  - `channel_name` — URL канала, как для остальных экшенов;
  - `userId` / `user_id` / `id`, либо `minichat.Data.UserID` из MiniChat.
- Результат:
  - `userName` / `user`, `userId`;
  - `isModerator`, `isOwner`;
  - `registeredAt` (если API отдал ненулевое значение);
  - `chatMessagesCount`, `permanentBansCount`, `temporaryBansCount`, `totalWatchedTime` (секунды);
  - `roleNames`, `badgeNames`;
  - `channelStatus`, `channelUrl`.
- `avatarUrl` и `nickColor` не перезаписываются — берите из MiniChat.

### \[VKVideoLive] Get Viewers

Экшен получает список зрителей аналогично тому, как это делает родной **PresentViewers**. Список VK ограничен количеством в 200 зрителей.

{{< img class="center" src="/images/usage/VkLiveGetViewers.png" alt="Экшен Get Viewers" >}}

- Триггер: **Timed Action \[VkLive] PresentViewers**. По умолчанию таймер отключен. Рекомендуется настроить его включение при старте стрима и отключение при окончании, чтобы список зрителей не получался, когда стрим оффлайн. Ставить интервал таймера меньше минуты СТРОГО НЕ РЕКОМЕНДУЕТСЯ.
- Список зрителей записывается в аргумент `users`, как это делает родной **PresentViewers**.
- В аргумент `viewers_count` записывается размер полученного списка (не более 200). Чтобы получить фактическоен количество зрителей на канале, используйте экшен **\[VKVideoLive] Get Viewers Count** (см. ниже).

### \[VKVideoLive] Get Viewers Count

Запрашивает у API фактическое количество зрителей на канале и записывает его в аргумент `viewers_count`.

{{< img class="center" src="/images/usage/VkLiveGetViewersCount.png" alt="VkLiveGetViewersCount: экшен Get Viewers Count" >}}

- Аргумент: `channel_name` — URL канала, как для остальных экшенов.
- Имеет смысл вызывать, когда нужна именно общее число зрителей на трансляции; для списка ников по-прежнему используйте **\[VKVideoLive] Get Viewers**.

### \[VKVideoLive] Get Rewards

Экшен запрашивает у API список наград канала, обновляет кэш `VkLiveRewardsCache` и записывает имена в аргумент `rewardNames`.

{{< img class="center" src="/images/usage/GetRewards.png" alt="Экшен Get Rewards" >}}

- Аргумент: `channel_name` — URL канала, как для остальных экшенов (см. раздел **Настройка модуля и стримербота**).
- В список попадают все награды канала, включая отключённые.
- Результат:
  - `rewardNames` — список имён наград;
  - `rewardsCount` — количество имён в списке;
  - `minichat.Service` — `VKVideoLive` (для цепочки с MiniChat Trigger Manager).
- Для **On Reward**, **Off Reward** и **ActivateReward** отдельный вызов **Get Rewards** не обязателен: кэш `VkLiveRewardsCache` обновляется автоматически при обращении к награде по имени.

Пример цепочки для пререгистрации триггеров MiniChat (метод `RegisterRewardTriggers` в MiniChat Trigger Manager):

1. **\[VKVideoLive] Get Rewards**
2. **\[MiniChat Trigger Manager] RegisterRewardTriggers** — `rewardNames` и `minichat.Service` из предыдущего шага.

### \[VKVideoLive] Get Reward Demands

Экшен получает страницу запросов наград (demands) зрителей через `GET /channel_point/reward/demands`.

- Аргументы:
  - `channel_name` — URL канала;
  - `limit` — опционально, по умолчанию `200` (максимум API);
  - `offset` — опционально, по умолчанию `0`.
- Результат:
  - `demandIds`, `demandRewardIds`, `demandUserIds`, `demandUserNicks`, `demandStatuses` — параллельные списки;
  - `demandsCount` — число записей на странице;
  - `demandsIsLast`, `demandsOffset` — пагинация из `extra` ответа API.

MiniChat **не** отдаёт числовой `demandId` VK API (`%redemptionId%` — GUID события MiniChat). Для reject/accept либо берите id из этого списка, либо используйте резолв по `userId` + `rewardId` (см. ниже).

### \[VKVideoLive] Reject Reward Demand / Accept Reward Demand

Экшены отклоняют или принимают конкретный запрос награды (`POST …/demand/reject` или `…/demand/accept`).

Общие аргументы:

- `channel_name` — URL канала;
- **либо** `demandId` / `demand_id` — числовой id demand из API;
- **либо** (типично после триггера MiniChat Reward):
  - `userId` / `user_id` / `minichat.Data.UserID`;
  - `rewardId` / `reward_id` / `minichat.Data.RewardID` (или `rewardName` с резолвом через кэш наград).

При резолве без `demandId` сервис листает demands, ищет открытый матч по зрителю и награде и берёт самый свежий (`created_at`, затем больший `id`).

После успеха в аргумент `demandId` записывается использованный id.

Типичная цепочка: триггер MiniChat Reward → **Reject Reward Demand** (или Accept) с `%userId%` и `%rewardId%`.

### \[VKVideoLive] Off Reward

Экшен позволяет отключить любую награду канала.  

{{< img class="center" src="/images/usage/VkLiveOffReward.png" alt="Экшен Off Reward" >}}

- Для работы экшена необходимо создать новый экшен, в нём задать аргумент:
  - `rewardName` -- название награды как оно отображается для зрителя на сайте.
- После чего вызвать сам экшен **\[VKVideoLive] Off Reward** через Run Action. 

Пример:

{{< img class="center" src="/images/usage/VkLiveExampleOffReward.png" alt="Пример Run Action для Off Reward" >}}

### \[VKVideoLive] On Reward

Экшен позволяет включить любую награду канала.  

{{< img class="center" src="/images/usage/VkLiveOnReward.png" alt="Экшен On Reward" >}}

- Для работы экшена необходимо создать новый экшен, в нём задать аргумент:
  - `rewardName` -- название награды как оно отображается для зрителя на сайте.
- После чего вызвать сам экшен **\[VKVideoLive] On Reward** через Run Action.

 Пример:

{{< img class="center" src="/images/usage/VkLiveExampleOnReward.png" alt="Пример Run Action для On Reward" >}}
