---
title: "Installation"
weight: 1
---

# VkLiveService - Руководство по установке и обновлению.

## Зависимости (обязательно для событий в MiniChat)
Если вы используете события, которые должны **отображаться в MiniChat**, у вас должна быть установлена и подключена интеграция **MiniChat** для Streamer.bot.

В коде сервиса события отправляются через вызов метода:
- коллекция методов: `MiniChat Method Collection`
- метод: `CreateCustomEvent`

Если интеграция MiniChat не установлена или не подключена, то экшены сервиса будут выполняться, но **события в MiniChat отображаться не будут**.

## Установка.
1. Скачайте файл `VkLiveService.txt` из последнего [релиза](https://github.com/NuboHeimer-for-streamers/-Streamer.Bot-VkVideoLiveService/releases).
2. Запустите стримербот.
3. В верхнем меню нажмите кнопку **Import**.

{{< figure class="center" src="/images/installation/import_btn.png" alt="Кнопка Import в верхнем меню Streamer.bot" >}}

4. Перетащите скачанный ранее `VkLiveService.txt` в область `Import String`. Если перетащить не получается, откройте файл блокнотом, скопируйте текст и вставьте его в `Import String`.

5. Нажмите кнопку **Import** справа внизу.

{{< figure class="center" src="/images/installation/import_btn2.png" alt="Кнопка Import внизу справа в диалоге импорта" >}}

5.1. Начиная с версии 1.0.0 Streamer.bot предупреждает, что вы импортируете кастомный C# код. Соглашаемся.

{{< figure class="center" src="/images/installation/Warning.png" alt="Предупреждение Streamer.bot о кастомном C# коде при импорте" >}}

6. Установка завершена. Ознакомьтесь с [инструкцией по использованию](USAGE.md)

## Обновление с версии 3.2.0.
1. Проделать все шаги выше.
2. Открыть **Global Variables** и удалить:
  - `vkvideolive_todays_viewers`
  - `last_random_viewer`
3. Открыть **Queues** и переименовать `NewViewers` на `[VkLive] Viewers`.
4. Открыть **Services** -> **Timers** и удалить `GetNewViewers`.