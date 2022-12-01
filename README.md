# Crypto Telegram Bot
## Sobre

Esse projeto foi criado com o intuito de ser apenas um bot pessoal para trade/sniper de cryptomoedas em DEX via Telegram, mas foi ganhando varias features a ponto de virar uma ferramenta comercial para aluguel de NFTs, apostas no Prediction da PancakeSwap e um sniper para a Launchpad SafeLaunch.

*_( O projeto não está ativo desde de janeiro 2022 )_*

### Tech
- C#
- [Nethereum] - An open source .NET integration library for Ethereum.
- MongoDB

## Features
> Descrição mais detalhada sobre cada feature mais abaixo

- [Trading/Sniper bot (MultiDex)](#tradingsniper-bot)
- [Pancake Prediction bot](#pancake-prediction-bot)
- [SafeLaunch sniper bot](#SafeLaunch-sniper-bot)
- [Pegaxy rent bot](#Pegaxy-rent-bot)
- [Gerenciador de usuarios](#Gerenciador-de-usuarios)

### Trading/Sniper Bot
[Screenshots](screenshots/trade_sniper/screenshot.md)

Essa feature do bot conseguia comprar/vender tokens a certos preços, igual ordens em corretoras, ou comprar tokens assim que lançados em qualquer DEX, tambem conhecido como sniper bot.

### Pancake Prediction bot
[Screenshots](screenshots/prediction/screenshot.md)

Essa feature experimental do bot conseguia apostar em rounds do Prediction da PancakeSwap, seguindo uma estrategia criar por mim que tentava apostar no lado mais lucrativo após _x_ rounds consecutivos.

### SafeLaunch sniper bot
[Screenshots](screenshots/safelaunch/screenshot.md)

Essa feature visava comprar tokens assim que liberados na Launchpad da Safelaunch.

### Pegaxy rent bot
[Screenshots](screenshots/pegaxy/screenshot.md)

Essa feature visava alugar NFT's do pegaxy assim que postados por seus donos, implementar isso foi complicado pois o site tentava proteger a todo custo o site contra bots, utilizando captchas e até CloudFlare para bloquear requests sem fingerprint do navegador.

### Gerenciador de usuarios
[Screenshots](screenshots/gerenciador_usuarios/screenshot.md)

- Controle de planos de assinaturas _( ex: BASIC / VIP / PREMIUM )_
> Os planos de assinaturas serviam para limitar os comandos dependendo do plano do usuario.
- Cada usuario pode ter varias carteiras associadas
> O usuario poderia cadastrar varias carteiras para utilizar os serviços em cada uma facilmente. 



[Nethereum]: <https://nethereum.com/>
