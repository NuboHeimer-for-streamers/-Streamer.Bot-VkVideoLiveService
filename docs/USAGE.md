## Disclaimer

Предполагается, что у вас уже есть установленная и настроенная интеграция с миничатом.

## Создание приложения вк.
Для работы этого модуля вам необходимо создать в вк своё приложение.
1. Переходим по ссылке https://dev.live.vkvideo.ru/apps и нажимаем "Войти" справа вверху.

<p align="center">
  <img src="./images/usage/SignIn.png" alt="Вход в кабинет разработчика">
</p>
2. Нажимаем "Создать приложение":

<p align="center">
  <img src="./images/usage/CreateButton.png" alt="Кнопка создания приложения">
</p>

- Называем приложение как нам хочется.
- Описание пишем так же какое угодно.
- При желании загружаем иконку.
- URL для web-push оставляем пустым.
- В последнее поле вставляем `http://localhost:5000/vkvideoliveredirecturi`
- Нажимаем "Создать"  

<p align="center">
  <img src="./images/usage/NewApp.png" alt="Создание прилжоения">
</p>

3. Записываем id приложения, секретный и публичный ключи. Они нам ещё пригодятся.
4. Нажимаем "Хорошо".


## Настройка модуля и стримербота.
1. Находим экшен `[VKVideoLive] Set channelName`
2. В set argument в sub-actions вписываем название своего канала так, как оно отображается в адресной строке браузера.  

<p align="center">
  <img src="./images/usage/ChannelName.png" alt="ChannelName">
</p>

3. Находим экшен `[VKVideoLive] Login`
4. В sab-actions заполняем ранее сохранённые значения:
- VkLiveAuthClientId -> ID приложения
- VkLiveAuthClientSecret -> Секретный ключ приложения
- VkLiveAuthRedirectUri -> URL для редиректа

<p align="center">
  <img src="./images/usage/Login.png" alt="Login">
</p>

5. Запускаем тестовый триггер.  

<p align="center">
  <img src="./images/usage/TestTrigger.png" alt="Login">
</p>

6. В открывшемся окне нажимаем `Login`.  

<p align="center">
  <img src="./images/usage/Login2.png" alt="Login">
</p>

7. Вас перебросит в браузер по умолчанию и попросит подтвердить разрешения приложения.
- нажимаем "разрешить".
- видим в браузере сообщение об успешной авторизации.
- окно логина можно закрывать. В нём так же будет отображаться успешное подключение.

<p align="center">
  <img src="./images/usage/Login3.png" alt="Login">
</p>

## Работа с экшенами.

### Общие рекомендации по триггерам

>⚠ Важно
>
> Для корректной очистки списков и избежания ошибок в определении зрителей, используйте триггер: **OBS -> Streaming Started** или привязанную к запуску стрима горячую клавишу.

### \[VKVideoLive] Clear Previous Present Viewers.

Экшен очищает сохранённый список **Present Viewers**, чтобы исключить устаревшие данные перед началом трансляции.  

<p align="center">
  <img src="./images/usage/VkLive_Clear_Previous_Present_Viewers.png" alt="Экшен Clear Previous Present Viewers">
</p>

- Очищайте список перед запуском стрима, иначе в нём могут храниться устаревшие данные и зрители будут определяться неверно. Рекомендации по триггеру на очистку смотрите в блоке "Важно" выше.

### \[VKVideoLive] Clear Todays Viewers.

Экшен очищает сохранённый список «сегодняшних» зрителей для корректного определения новых зрителей текущей трансляции.  

<p align="center">
  <img src="./images/usage/VkLive_Clear_Todays_Viewers.png" alt="Экшен Clear Todays Viewers">
</p>

- Очищайте список перед запуском стрима, иначе в нём могут храниться устаревшие данные и зрители будут определяться неверно. Рекомендации по триггеру на очистку смотрите в блоке "Важно" выше.

### \[VKVideoLive] ActivateRevard.

Экшен позволяет активировать любую награду канала от вашего имени.  

<p align="center">
  <img src="./images/usage/VkLiveActivateReward.png" alt="Экшен Clear Todays Viewers">
</p>

Для работы экшена необходимо создать новый экшен, в нём задать аргументы:
- rewardName -- название награды как оно отображается для зрителя на сайте.
- rewardText -- текст награды, если она требует ввод текста.
После чего вызвать сам экшен `[VKVideoLive] ActivateRevard` через Run Action. Пример:

<p align="center">
  <img src="./images/usage/VkLiveExampleActivateReward.png" alt="Экшен Clear Todays Viewers">
</p>

### \[VKVideoLive] AddFirstWordViewer

Экшен добавляет зрителя, написавшего в чат впервые за сегодняшнюю трансляцию, в список «впервые зашедших» зрителей. Это позволяет не отправлять событие в MiniChat для уже увиденных в чате зрителей.  

<p align="center">
  <img src="./images/usage/VkLive_Add_First_Words_Viewer.png" alt="Экшен Add First Word Viewer">
</p>

- Триггер: **Custom -> MiniChat -> -> VkVideoLive -> First Words**
- Используйте, если не хотите видеть событие в MiniChat для зрителей, написавших сообщение в чат. Если событие нужно — отключите этот экшен.

### \[VKVideoLive] Code

Служебный экшен с кодом. Так же в нём можно узнать текущую версию (указана в комментарии в сабэкшенах и в самом коде).  

<p align="center">
  <img src="./images/usage/VkLiveVersion.png" alt="Экшен Code с версией скрипта">
</p>


### \[VKVideoLive] Get In Out Viewers

Экшен отправляет в MiniChat пользовательское событие о пришедшем или ушедшем зрителе.

<p align="center">
  <img src="./images/usage/VkLive_Get_In_Out_Viewers.png" alt="Экшен Get In Out Viewers">
</p>

- Триггер: **Custom -> Vk Video Live -> Present Viewers (VkLive)**.
- Пишет, когда зритель впервые зашёл на трансляцию.
- Пишет, когда зритель просто появился в списке зрителей (пришёл на трансляцию).
- Пишет, когда зритель пропал из списка зрителей (ушёл с трансляции).
- Примечания: требует корректной настройки экшена **[VKVideoLive] Get Viewers** и предварительной очистки списков перед началом трансляции.

### \[VKVideoLive] Get New Viewers

Экшен отправляет в MiniChat пользовательское событие о новом зрителе на текущей трансляции. По умолчанию выключен. Если используете Get In Out Viewers, то оставьте выключенным.

<p align="center">
  <img src="./images/usage/VkLive_Get_New_Viewers.png" alt="Экшен Get New Viewers">
</p>

- Триггер: **Custom -> Vk Video Live -> Present Viewers (VkLive)**.
- Примечания: требует корректной настройки экшена **[VKVideoLive] Get Viewers** и предварительной очистки списков перед началом трансляции.

### \[VKVideoLive] Get Random Viewer

Экшен получает одного случайного зрителя и записывает его в аргумент `viewer`. Будет исправлено на `randomUserName0`.

<p align="center">
  <img src="./images/usage/VkLive_Random_Viewer.png" alt="Экшен Get New Viewers">
</p>

### \[VKVideoLive] Get Viewers

Экшен получает список зрителей аналогично тому, как это делает родной PresentViewers. Список вк ограничен количеством в 200 зрителей.

<p align="center">
  <img src="./images/usage/VkLiveGetViewers.png" alt="Экшен Get New Viewers">
</p>

- Триггер: Timed Action `[VkLive] PresentViewers`. По умолчанию таймер отключен. Рекоментуется настроить его включение при старте стрима и отключение при окончании, чтобы список зрителей не получался, когда стрим оффлайн. Ставить интервал таймера меньше минуты СТРОГО НЕ РЕКОМЕНДУЕТСЯ.
- Список зрителей записывается в аргумент `users`, как это делает родной PresentViewers.
- Количество зрителей записывается в аргумент `viewers_count`.

### \[VKVideoLive] Off Reward

Экшен позволяет отключить любую награду канала.  

<p align="center">
  <img src="./images/usage/VkLiveOffReward.png" alt="Экшен Clear Todays Viewers">
</p>

Для работы экшена необходимо создать новый экшен, в нём задать аргумент:
- `rewardName` -- название награды как оно отображается для зрителя на сайте.
После чего вызвать сам экшен `[VKVideoLive] Off Reward` через Run Action. Пример:

<p align="center">
  <img src="./images/usage/VkLiveExampleOffReward.png" alt="Экшен Clear Todays Viewers">
</p>

### \[VKVideoLive] On Reward

Экшен позволяет включить любую награду канала.  

<p align="center">
  <img src="./images/usage/VkLiveOnReward.png" alt="Экшен Clear Todays Viewers">
</p>

Для работы экшена необходимо создать новый экшен, в нём задать аргумент:
- `rewardName` -- название награды как оно отображается для зрителя на сайте.
После чего вызвать сам экшен `[VKVideoLive] On Reward` через Run Action. Пример:

<p align="center">
  <img src="./images/usage/VkLiveExampleOnReward.png" alt="Экшен Clear Todays Viewers">
</p>