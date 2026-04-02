# VkLiveService - Руководство по установке и обновлению.

## Зависимости (обязательно для событий в MiniChat)
Если вы используете события, которые должны **отображаться в MiniChat**, у вас должна быть установлена/подключена интеграция **MiniChat** для Streamer.bot.

В коде сервиса события отправляются через вызов метода:
- коллекция методов: `MiniChat Method Collection`
- метод: `CreateCustomEvent`

Если этой коллекции/метода нет (интеграция MiniChat не установлена или не импортирована), то экшены сервиса будут выполняться, но **события в MiniChat отображаться не будут**.

## Установка.
1. Скачайте файл `VkLiveSerive.txt` из последнего [релиза](https://github.com/NuboHeimer-for-streamers/-Streamer.Bot-VkVideoLiveService/releases).
2. Запустите стримербот.
3. В верхнем меню нажмите кнопку `Import`.

<p align="center">
  <img src="./images/installation/import_btn.png" alt="Кнопка Import">
</p>
4. Перетащите скачанный ранее `VkLiveService.txt` в область `Import String`. Если перетащить не получается, откройте файл блокнотом, скопируйте текст и вставьте его в `Import String`.
5. Нажмите кнопку Import справа внизу.

<p align="center">
  <img src="./images/installation/import_btn2.png" alt="Кнопка Import внизу справа">
</p>
5.1. Начиная с версии 1.0.0 Streamer.bot предупреждает, что вы импортируете кастомный C# код. Соглашаемся.

<p align="center">
  <img src="./images/installation/Warning.png" alt="Предупреждение об импорте кастомного кода">
</p>

6. Установка завершена.

## Обновление с версии 1.0.3.
1. Запустите экшен **\[Twitch] Remove twitch_todays_viewers**

<p align="center">
  <img src="./images/installation/Remove_twitch_todays_viewers.png" alt="Экшен Remove twitch_todays_viewers">
</p>
2. Запустите экшен **\[Twitch] Remove twitchLastViewersNameList**

<p align="center">
  <img src="./images/installation/Remove_twitchLastViewersNameList.png" alt="Экшен Remove twitchLastViewersNameList">
</p>

Это нужно для удаления старых глобальных переменных, которые больше не используются.