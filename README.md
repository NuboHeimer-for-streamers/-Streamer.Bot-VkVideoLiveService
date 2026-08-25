VKVideoLiveService — интеграция [VK Video Live](https://live.vkvideo.ru/) для [Streamer.bot](https://streamer.bot/).

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc-sa/4.0/)
[![Version](https://img.shields.io/badge/Version-5.0.0__dev.3-blue.svg)](https://github.com/NuboHeimer-for-streamers/-Streamer.Bot-VkVideoLiveService)

Модуль позволяет получать список активных зрителей, работать с наградами VK Video Live и использовать эти данные в сценариях Streamer.bot.

## 📋 Содержание

- [Документация](#-документация)
- [Обзор](#-обзор)
- [Возможности](#-возможности)
- [Поддержка](#-поддержка)
- [Лицензия](#-лицензия)

## 📚 Документация

- [Скачать / версии](docs/content/DOWNLOAD.md)
- [Установка и обновление](docs/content/INSTALLATION.md)
- [Использование](docs/content/USAGE.md)
- [История изменений](CHANGELOG.md)

## 🎯 Обзор

VKVideoLiveService — это набор методов для Streamer.bot, которые упрощают работу с VK Video Live:

- получение списка активных зрителей, количества и **детальных данных одного зрителя**;
- выбор случайного зрителя;
- управление наградами за баллы;

## ✨ Возможности

### Функционал

- **Get Viewers**
  - Получает список *активных* зрителей и записывает его в аргумент `users` (аналогично встроенному триггеру `PresentViewers` в Streamer.bot).
  - В аргумент `viewers_count` попадает размер полученного списка. API VkLive отдаёт **не больше 200 зрителей** в этом списке. Для получения фактического количества зрителей следует использовать **Get Viewers Count**.

- **Get Viewers Count**
  - Запрашивает у API фактическое количество зрителей на канале и записывает его в аргумент `viewers_count`.

- **Get Viewer Info**
  - Запрашивает детальные данные зрителя по `userId` через официальный `GET /chat/member` (статистика, роли, значки). `avatarUrl` / `nickColor` — из MiniChat.

- **Get Random Viewer**
  - Выбирает случайного зрителя из списка *активных* зрителей (см. **Get Viewers**) и записывает его имя в аргумент `randomUserName0`.

- **Награды**
  - `GetRewards` — получает список наград канала (включая отключённые), обновляет глобальный кэш `VkLiveRewardsCache` и записывает результат в аргументы `rewardNames`, `rewardsCount` и `minichat.Service` (`VKVideoLive`).
  - `OnReward` — включает награду за баллы по имени (`rewardName`). Кэш наград подтягивается автоматически, отдельный вызов `GetRewards` не обязателен.
  - `OffReward` — выключает награду за баллы по имени (`rewardName`). Кэш наград подтягивается автоматически.
  - `ActivateReward` — активирует награду от вашего имени. Кэш наград подтягивается автоматически.

## 🔌 Зависимости (MiniChat — опционально)

Приход/уход и «новый зритель» больше **не** отправляются в MiniChat из кода сервиса. Для них используются кастомные триггеры Streamer.bot (`Viewer First Today` / `Viewer Joined` / `Viewer Left`); отправку в MiniChat при необходимости собираете отдельным action.

MiniChat по-прежнему нужен для сценариев вроде **First Words** и пререгистрации триггеров наград:

- коллекция методов: `MiniChat Method Collection` / `MiniChat Trigger Manager`
- для наград: **Get Rewards** → `RegisterRewardTriggers` (`rewardNames` и `minichat.Service` из предыдущего шага)

### Где взять интеграцию:
- [Оригинал в Telegram Bot Streamfony](https://t.me/StreamfonyBot/app?startapp=plugin_19-utm_share)
- [Оригинал в Яндекс.Диск](https://disk.yandex.ru/d/rU1yj5auUGPCNg)
- [Мой фикс в Telegram](https://t.me/nuboheimersb/702)
- [Мой фикс в Вк](https://vk.com/topic-236253647_57236854)
- [Мой фикс в Яндекс.Диск](https://disk.yandex.ru/d/GuCJvlnCJwZIeg)


## 🆘 Поддержка

- **Автор**: NuboHeimer  
- **Личка Telegram**: [@nuboheimer](https://t.me/nuboheimer)  
- **Группа Telegram**: [@nuboheimersb](https://t.me/nuboheimersb/29)  
- **Email**: nuboheimer@yandex.ru  
- **Личка VK**: [vk.com/nuboheimer](https://vk.com/nuboheimer)
- **Группа VK**: [https://vk.com/nuboguides](https://vk.com/nuboguides)

## 📄 Лицензия

Этот проект распространяется под лицензией [Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License](https://creativecommons.org/licenses/by-nc-sa/4.0/).

**Вы можете:**

- 🔄 **Делиться** — копировать и распространять код.
- 🔧 **Адаптировать** — изменять, трансформировать и улучшать код.

**При следующих условиях:**

- 📝 **Атрибуция** — вы должны указывать авторство.
- 🚫 **Некоммерческое использование** — материал нельзя использовать в коммерческих целях.
- 🔗 **ShareAlike** — при изменении кода вы должны распространять его под той же лицензией.

Полный текст лицензии доступен в файле [LICENSE](LICENSE).
