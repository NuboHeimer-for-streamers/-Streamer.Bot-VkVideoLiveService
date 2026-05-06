---
title: "VkLiveService"
---

VKVideoLiveService — интеграция [VK Video Live](https://live.vkvideo.ru/) для [Streamer.bot](https://streamer.bot/).

Модуль позволяет получать список активных зрителей, работать с наградами VK Video Live и использовать эти данные в сценариях Streamer.bot.

## Быстрый старт

- [Установка и обновление]({{< relref "docs/INSTALLATION.md" >}})
- [Использование]({{< relref "docs/USAGE.md" >}})
- [Релизы на GitHub](https://github.com/NuboHeimer-for-streamers/-Streamer.Bot-VkVideoLiveService/releases)

## Возможности (кратко)

- Получение списка активных зрителей (`users`) и их количества (`viewers_count`)
- Получение фактического количества зрителей (**Get Viewers Count**)
- Выбор случайного зрителя (**Get Random Viewer**, аргумент `randomUserName0`)
- Управление наградами: **OnReward**, **OffReward**, **ActivateReward**

## Важно про MiniChat

Часть событий этого сервиса рассчитана на отображение в **MiniChat**. Если интеграция MiniChat не установлена или не подключена, экшены могут выполняться, но **события в MiniChat отображаться не будут**.
