# MonitoramentoRios

Repositório do Projeto de Monitoramento de Rios

Acesse o sistema que monitora Rolante no Rio Grande do Sul: [Acessar](https://rios.bitcoineaqui.com.br/)

## Objetivo

Este repositório tem por objetivo desenvolver uma stack de Software e Hardware para monitoramento atmosférico, pluvial e fluvial, com o objetivo de publicar de forma aberta dados precisos a todos que precisarem.

## Arquitetura

O é Sistema altamente integrável, permitindo tanto o uso do Hardware desenvolvido aqui quanto integração de fontes externas, além de ser distribuído replicado em locais geograficamente afastados para redundância e proteção. 

Focando em baixo custo a Placa-mãe foi desenvolida pelo projeto para utilização de sensores amplamente disponíveis

### Detalhes do Hardware

Hardware baseado em ESP32 utilizando PCB customizada para o ambiente úmido da beira de rios mas compatível com DevKits como NodeMCU

Firmware baseado na Stack ESP-Home pela melhor curva de aprendizado de novos colaboradores

### Detalhes do Software

Backend redundante e integrado, recebe dados diretamente de estações, de servidor MQTT externo, de outros Backends, e integrações de sistemas externos

Frontend em Html/JS puro para leveza a simplicidade de treinamento e manutenção

## Agradecimentos Especiais

* A todas as doações em Bitcoin para o projeto [Bitcoin é Aqui](https://bitcoineaqui.com.br) que financiaram a pesquisa e desenvolvimento do projeto
* Ao [Geoge Silva](https://github.com/nitroxgas) por lançar a ideia e desenvolver o primeiro protótipo

## Posso usar o software ou hardware que este repositório desenvolve?

SIM!, mas com ressalvas.

O objetivo deste projeto é compartilhar informações, porém há muitas entidades que coletam os dados e os fecham, tornando-os inúteis para um amplo acesso.
Portanto, se você compartilha seus dados, você pode usar os nossos; se você restringe seus dados, você não pode usar os nossos.

Leia mais abaixo o texto completo do licenciamento antes de contribuir ou utilizar este repositório.

## Como replicar na minha cidade?

Você pode tanto replicar o sistema aqui desenvolvido por conta como integrar ao sistema já mantido pelo Projeto

Para integrar sua cidade no sistema já existente entre em contato com o [Bitcoin é Aqui](https://bitcoineaqui.com.br) ou com a [ACISA de Rolante](https://acisarolante.com/)

## Licença

Este repositório está licenciado sob uma dupla licença (`MIT` e `Licença Restrita`). Leia atenciosamente antes de contribuir e usar os dados gerados por estes softwares e hardwares.

A licença geral é MIT. No entanto, se você coleta, fornece e/ou processa dados de monitoramento atmosférico, manancial, fluvial, pluvial ou semelhantes, está sujeito às seguintes condições: Se os dados que você coleta são fechados, privados, compartilhados com limitações de acesso ou licenciamento, possuem mecanismos de controle de acesso, e/ou limitações na forma de acesso, então você não pode acessar, visualizar, compartilhar, utilizar, ou divulgar os dados coletados e gerados por este repositório e suas derivações. Nestes casos, você está sujeito à `Licença Restrita`.

Adicionalmente, se você é órgão governamental ou à serviço do serviço público, você precisa pedir autorização prévia para o uso dos dados.

### Licença Restrita

~~~
Termos de Uso da Licença Restrita

Esta licença estabelece os termos sob os quais os dados coletados e processados  podem ser acessados. Ao acessar ou utilizar estes dados, você concorda com os seguintes termos:

1. Proibição de Uso:

Os dados fornecidos por este repositório não podem ser utilizados para qualquer finalidade, seja ela comercial ou não comercial.

2. Proibição de Modificação:

Você não tem permissão para modificar, adaptar, ou criar obras derivadas dos dados fornecidos por este repositório.

3. Proibição de Redistribuição:

Você não pode redistribuir, compartilhar, publicar, ou de qualquer forma disponibilizar os dados a terceiros.

4. Proibição de Acesso Automatizado:

O acesso automatizado aos dados (por exemplo, via scripts, bots, ou outras ferramentas de automação) é estritamente proibido.

5. Proibição de Análise e Processamento:

Qualquer forma de análise ou processamento adicional dos dados coletados por este repositório é estritamente proibida.

6. Confidencialidade:

Você deve manter a confidencialidade dos dados acessados e não deve divulgar, publicar, ou de qualquer forma tornar públicos esses dados.

7. Penalidades

Qualquer violação dos termos desta licença pode resultar em medidas legais, incluindo, mas não se limitando a, ações civis e criminais.

8. Acordo

Ao acessar ou utilizar os dados fornecidos por este repositório, você reconhece que leu, entendeu e concorda em cumprir todos os termos desta licença. Se você não concorda com estes termos, não deve acessar ou utilizar os dados.
~~~
