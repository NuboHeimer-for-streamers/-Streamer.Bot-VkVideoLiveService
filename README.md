VKVideoLiveService — интеграция [VK Video Live](https://live.vkvideo.ru/) для [Streamer.bot](https://streamer.bot/).

[![License: CC BY-NC-SA 4.0](https://img.shields.io/badge/License-CC%20BY--NC--SA%204.0-lightgrey.svg)](https://creativecommons.org/licenses/by-nc-sa/4.0/)
[![Version](https://img.shields.io/badge/Version-3.2.0-blue.svg)](https://github.com/NuboHeimer-for-streamers/-Streamer.bot-VkLiveService)

Модуль позволяет получать список активных зрителей, работать с наградами VK Video Live и использовать эти данные в сценариях Streamer.bot.

## 📋 Содержание

- [Обзор](#-обзор)
- [Возможности](#-возможности)
- [Как пользоваться](#-как-пользоваться)
- [Поддержка](#-поддержка)
- [Лицензия](#-лицензия)

## 🎯 Обзор

VKVideoLiveService — это набор методов для Streamer.bot, которые упрощают работу с VK Video Live:

- получение списка активных зрителей и их количества;
- выбор случайного зрителя;
- управление наградами за баллы;

## ✨ Возможности

### Функционал

- **Get Viewers**
  - Получает список *активных* зрителей и записывает его в аргумент `users` (аналогично встроенному триггеру `PresentViewers` в Streamer.bot).
  - Api VkLive ограничивает этот список в 200 зрителей.

- **Get Viewers Count**
  - Возвращает количество текущих зрителей.
  - Записывает значение в аргумент `viewers_count`.

- **Get Random Viewer**
  - Возвращает случайного зрителя из списка *активных* зрителей.

- **Награды и статистика**
  - `OnReward` — включает награду за баллы.
  - `OffReward` — выключает награду за баллы.
  - `ActivateRevard` — активирует награду от вашего имени.


## 🆘 Поддержка

- **Автор**: NuboHeimer  
- **Личка Telegram**: [@nuboheimer](https://t.me/nuboheimer)  
- **Группа Telegram**: [@nuboheimersb](https://t.me/nuboheimersb/29)  
- **Email**: nuboheimer@yandex.ru  
- **VK**: [vk.com/nuboheimer](https://vk.com/nuboheimer)

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