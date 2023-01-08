# Crypto Telegram Bot
## About

This project was initially created as a personal bot for cryptocurrency trading/sniper on DEX through Telegram, but it gained several features to the point of becoming a commercial tool for NFT rental, betting on PancakeSwap Prediction and a sniper for SafeLaunch Launchpad.

*_(The project has not been active since January 2022)_*

### Tech
- C#
- [Nethereum] - An open source .NET integration library for Ethereum.
- MongoDB

## Features
> More detailed description about each feature below

- [Trading/Sniper bot (MultiDex)](#tradingsniper-bot)
- [Pancake Prediction bot](#pancake-prediction-bot)
- [SafeLaunch sniper bot](#SafeLaunch-sniper-bot)
- [Pegaxy rent bot](#Pegaxy-rent-bot)
- [Gerenciador de usuarios](#Gerenciador-de-usuarios)

### Trading/Sniper Bot
[Screenshots](screenshots/trade_sniper/screenshot.md)

This feature of the bot was able to buy/sell tokens at certain prices, similar to orders on brokers, or buy tokens as soon as they were released on any DEX, also known as a sniper bot.

### Pancake Prediction bot
[Screenshots](screenshots/prediction/screenshot.md)

This experimental feature of the bot was able to bet on rounds of PancakeSwap Prediction, following a strategy created by me that tried to bet on the most profitable side after x consecutive rounds.

### SafeLaunch sniper bot
[Screenshots](screenshots/safelaunch/screenshot.md)

This feature aimed to buy tokens as soon as they were released on the SafeLaunch Launchpad.

### Pegaxy rent bot
[Screenshots](screenshots/pegaxy/screenshot.md)

This feature aimed to rent NFT's from pegaxy as soon as they were posted by their owners, implementing this was complicated as the site tried to protect the site against bots at all costs, using captchas and even CloudFlare to block requests without browser fingerprint.

### User manager
[Screenshots](screenshots/gerenciador_usuarios/screenshot.md)

- Control of subscription plans _( ex: BASIC / VIP / PREMIUM )_
> Subscription plans were used to limit commands depending on the user's plan.
- Each user can have multiple associated wallets
> The user could register multiple wallets to easily use the services on each one.



[Nethereum]: <https://nethereum.com/>
