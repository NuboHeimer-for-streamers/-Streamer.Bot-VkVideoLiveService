---
title: "Установка и обновление."
weight: 2
type: "docs"
slug: "installation"
url: "installation/"
---

## Зависимости (MiniChat — опционально)

Базовая работа модуля (авторизация, Get Viewers, приход/уход через кастомные триггеры) **не требует** MiniChat.

MiniChat нужен, если вы:
- сами отправляете события в MiniChat с триггеров Viewer First Today / Joined / Left (или других);
- используете **First Words** / пререгистрацию триггеров наград через MiniChat Trigger Manager.

Типичные точки интеграции:
- коллекция методов: `MiniChat Method Collection` (например `CreateCustomEvent` в вашем action);
- `MiniChat Trigger Manager` для наград / First Words.

## Установка.
1. Скачайте файл импорта `VkLiveService-<version>.txt` со страницы [Скачать / версии]({{< relref "DOWNLOAD.md" >}}).
2. Запустите стримербот.
3. В верхнем меню нажмите кнопку **Import**.

{{< img class="center" src="/images/installation/import_btn.png" alt="Кнопка Import в верхнем меню Streamer.bot" >}}

4. Перетащите скачанный ранее `VkLiveService.txt` в область `Import String`. Если перетащить не получается, откройте файл блокнотом, скопируйте текст и вставьте его в `Import String`.

5. Нажмите кнопку **Import** справа внизу.

{{< img class="center" src="/images/installation/import_btn2.png" alt="Кнопка Import внизу справа в диалоге импорта" >}}

5.1. Начиная с версии 1.0.0 Streamer.bot предупреждает, что вы импортируете кастомный C# код. Соглашаемся.

{{< img class="center" src="/images/installation/Warning.png" alt="Предупреждение Streamer.bot о кастомном C# коде при импорте" >}}

6. Установка завершена. Ознакомьтесь с [инструкцией по использованию]({{< relref "USAGE.md" >}})

## Обновление с версии 3.2.0.
1. Проделать все шаги выше.
2. Открыть **Global Variables** и удалить:
  - `vkvideolive_todays_viewers`
  - `last_random_viewer`
3. Открыть **Queues** и переименовать `NewViewers` на `[VkLive] Viewers`.
4. Открыть **Services** -> **Timers** и удалить `GetNewViewers`.
