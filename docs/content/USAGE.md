---
title: "Использование"
weight: 2
type: "docs"
description: "Руководство по настройке VkLiveService: создание приложения VK, авторизация в Streamer.bot и использование экшенов."
slug: "usage"
url: "usage/"
---

## Disclaimer

Создание приложения VK и авторизация в Streamer.bot описаны ниже и **не требуют** MiniChat. Интеграция MiniChat нужна только для тех экшенов, которые отправляют пользовательские события в MiniChat (см. соответствующие разделы).

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

Экшен добавляет зрителя, впервые за трансляцию написавшего в чат, в список зрителей `VkLiveTodaysViewers`. Это позволяет не отправлять событие в MiniChat для уже увиденных в чате зрителей. 

{{< img class="center" src="/images/usage/VkLive_Add_First_Words_Viewer.png" alt="Экшен Add First Word Viewer" >}}

- Триггер: **Custom** -> **MiniChat** -> **VkVideoLive** -> **First Words**
- Используйте, если не хотите видеть событие в MiniChat для зрителей, написавших сообщение в чат.

### \[VKVideoLive] Code

Служебный экшен с кодом. Также в нём можно узнать текущую версию (указана в комментарии в сабэкшенах и в самом коде).  

{{< img class="center" src="/images/usage/VkLiveVersion.png" alt="Экшен Code, версия скрипта" >}}


### \[VKVideoLive] Get In Out Viewers

Экшен отправляет в MiniChat пользовательское событие о пришедшем или ушедшем зрителе.

{{< img class="center" src="/images/usage/VkLive_Get_In_Out_Viewers.png" alt="Экшен Get In Out Viewers" >}}

- Триггер: **Custom** -> **Vk Video Live** -> **Present Viewers (VkLive)**.
- Пишет, когда зритель впервые зашёл на трансляцию.
- Пишет, когда зритель просто появился в списке зрителей (пришёл на трансляцию).
- Пишет, когда зритель пропал из списка зрителей (ушёл с трансляции).
- Примечания: требует корректной настройки экшена **\[VKVideoLive] Get Viewers** и предварительной очистки списков перед началом трансляции.

### \[VKVideoLive] Get New Viewers

Экшен отправляет в MiniChat пользовательское событие о новом зрителе на текущей трансляции. По умолчанию выключен. Если используете **Get In Out Viewers**, то оставьте выключенным.

{{< img class="center" src="/images/usage/VkLive_Get_New_Viewers.png" alt="Экшен Get New Viewers" >}}

- Триггер: **Custom** -> **Vk Video Live** -> **Present Viewers (VkLive)**.
- Примечания: требует корректной настройки экшена **\[VKVideoLive] Get Viewers** и предварительной очистки списков перед началом трансляции.

### \[VKVideoLive] Get Random Viewer

Экшен получает одного случайного зрителя и записывает его имя в аргумент `randomUserName0`.

{{< img class="center" src="/images/usage/VkLive_Random_Viewer.png" alt="Экшен Get Random Viewer" >}}

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
