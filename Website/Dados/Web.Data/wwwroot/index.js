const loraIcon = "<img src='lora.svg' style='height:24px;margin-left:4px;vertical-align: middle;'>";
const noSigSvg = '<svg xmlns="http://www.w3.org/2000/svg" height="24px" viewBox="0 -960 960 960" width="24px" fill="#5f6368"><path d="M790-56 414-434q-47 11-87.5 33T254-346l-84-86q32-32 69-56t79-42l-90-90q-41 21-76.5 46.5T84-516L0-602q32-32 66.5-57.5T140-708l-84-84 56-56 736 736-58 56Zm-310-64q-42 0-71-29.5T380-220q0-42 29-71t71-29q42 0 71 29t29 71q0 41-29 70.5T480-120Zm236-238-29-29-29-29-144-144q81 8 151.5 41T790-432l-74 74Zm160-158q-77-77-178.5-120.5T480-680q-21 0-40.5 1.5T400-674L298-776q44-12 89.5-18t92.5-6q142 0 265 53t215 145l-84 86Z"/></svg>';

function montaTabelaEstacoes(lst) {
    fetch('/estacoes/ultimos')
        .then(response => response.json())
        .then(data => {
            const tableBody = document.querySelector('#ultimasLeituras tbody');
            tableBody.innerHTML = "";
            data.forEach(dado => {
                if (dado.nomeEstacao.startsWith('EX')) return;

                // Bloco Tabela
                if (dado.source == 3 && !dado.percentBateria && dado.tensaoBateria) { // LORA
                    if (dado.tensaoBateria < 3.2) dado.percentBateria = 0;
                    else if (dado.tensaoBateria < 3.4) dado.percentBateria = 10;
                    else if (dado.tensaoBateria < 3.6) dado.percentBateria = 40;
                    else if (dado.tensaoBateria < 3.7) dado.percentBateria = 60;
                    else if (dado.tensaoBateria < 4) dado.percentBateria = 80;
                    else dado.percentBateria = 90;
                }
                let bat = (dado.percentBateria || dado.percentBateria == 0) ? `<i class="bi ${iconeBateria(dado.percentBateria)}"></i>` : '';
                let wifiSigPerc = wifiSignalToPercent(dado.forcaSinal);
                let sig = dado.source == 3 ? loraIcon : `<i class="${iconeWifi(wifiSigPerc)}"></i>`;

                let temp = dado.temperaturaAr ? `<i class="bi-thermometer"></i> ${formatValue(dado.temperaturaAr, 1)}ºC` : ''
                let humd = dado.umidadeAr ? `<i class="bi-droplet"></i> ${formatValue(dado.umidadeAr, 0)}%` : ''
                let prss = dado.pressaoAr ? `<i class="bi-box-arrow-in-down"></i> ${formatValue(dado.pressaoAr, 0)} hPa` : ''

                let nivel = dado.nivelRio || dado.nivelRio == 0 ? `<i class="bi-water"></i> ${formatValueUnit(dado.nivelRio, 1, 'm')}` : '';
                let chuva = '';

                if (dado.precipitacao10min || dado.precipitacao10min == 0) {
                    let idCh = `ch_${dado.estacao}`;
                    chuva = `<i class="${dado.precipitacao10min && dado.precipitacao10min > 0 ? 'bi-cloud-rain' : 'bi-cloud'}"></i> <span id='${idCh}'>${formatValueUnit(dado.precipitacao10min, 1, 'mm/min')}</span>`;
                    carregaChuvaEstacao(dado.estacao, idCh);
                }

                const row = document.createElement('tr');
                row.innerHTML = `
                                        <td style='text-align: left; width: 90px'>${dado.nomeEstacao || dado.estacao}</td>
                                        <td style='text-align: left;'><span title="WiFi: ${wifiSigPerc}">${sig}</span> <span title="Bateria: ${(dado.percentBateria ?? 0).toFixed(0)}%">${bat}</span></td>
                                        <td><span>${humd}</span> <span>${temp}</span> <span>${prss}</span></td>
                                        <td><span>${nivel}</span> <span>${chuva}</span></td>
                                        <td><a class="btn" href="live.html?estacao=${dado.estacao}">Ver Estação</a></td>
                                     `;
                tableBody.appendChild(row);
                // Bloco Mapa

                let text = `-`;
                if (dado.nivelRio === null && dado.temperaturaAr === null) {
                    text = `?/?`;
                } else if (dado.precipitacao10min || dado.precipitacao10min == 0) {
                    text = `${formatValue(dado.temperaturaAr, 1)}ºC<br>${formatValue(dado.precipitacao10min, 1)}mm`;
                } else if (dado.nivelRio === null || dado.nivelRio === undefined) {
                    text = `${formatValue(dado.temperaturaAr, 1)}ºC`;
                } else {
                    text = `${formatValue(dado.temperaturaAr, 1)}ºC<br>${formatValue(dado.nivelRio, 1)}m`;
                }
                atualizaEstacao(lst, text, dado.estacao);
            });
        })
        .catch(error => {
            console.error('Erro ao carregar dados das estações:', error);
        });
}
function carregaHistoricoGrafico(idEstacao, canvasId, nivelNormal, nivelAlerta) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    fetch('/estacoes/lastHourly?lastHours=36&estacao=' + idEstacao)
        .then(response => response.json())
        .then(data => {
            // Criar o gráfico
            const labels = []; // Para os rótulos do gráfico
            const nivelRioData = [];
            const acumulado12Data = [];
            const acumulado24Data = [];
            const nivelRioDataNormal = [];
            const nivelRioDataAlerta = [];

            // Gera acumulado móvel
            for (let i = 0; i < data.length; i++) {
                if (!data[i]) continue;
                // 24h
                let acumulado = 0;
                let start = Math.max(0, i - 23); // i - 23 porque queremos incluir até i (24 horas no total)
                for (let j = start; j <= i; j++) {
                    if (!data[j]) continue;
                    acumulado += data[j].precipitacaoTotal_Hora || 0; // Soma o valor, tratando null/undefined como 0
                }
                data[i].acumulado24h = acumulado;
                // 12h
                acumulado = 0;
                start = Math.max(0, i - 12);
                for (let j = start; j <= i; j++) {
                    if (!data[j]) continue;
                    acumulado += data[j].precipitacaoTotal_Hora || 0;
                }
                data[i].acumulado12h = acumulado;
            }

            const validData = data.filter(dado => dado !== undefined && dado !== null);

            // Gerar todos os horários entre o primeiro e o último
            const now = new Date(); // Horário atual
            const endDate = new Date(now.getTime() - 1 * 60 * 60 * 1000); // Retira 1h
            const startDate = new Date(now.getTime() - 24 * 60 * 60 * 1000); // 24 horas atrás
            const allLabels = generateHourlyLabels(startDate, endDate);

            // Criar um mapa dos dados existentes para facilitar a busca
            const dataMap = new Map();
            validData.forEach(dado => {

                if (!dado.dataHoraDadosUTC.endsWith('Z')) dado.dataHoraDadosUTC = dado.dataHoraDadosUTC + 'Z';
                const current = new Date(dado.dataHoraDadosUTC);
                const label = getDateTimeForTimezone(current, -3);
                dataMap.set(label, dado);
            });
            let modoNivel = idEstacao != "5BA69743261D364A";
            let maiorAcumulado = 0;
            let maiorNivel = 0;

            // Preencher os arrays com os dados, inserindo null nas janelas
            allLabels.forEach(label => {
                labels.push(label);
                const dado = dataMap.get(label);

                if (dado) {

                    if (modoNivel) {
                        if (dado.nivelRio_AVG < 0) dado.nivelRio_AVG = 0;
                        nivelRioData.push(formatValue(dado.nivelRio_AVG, 2) || null);

                        if (dado.nivelRio_AVG > maiorNivel) maiorNivel = dado.nivelRio_AVG;
                    } else {
                        nivelRioData.push(formatValue(dado.precipitacaoTotal_Hora, 1) || null);
                        acumulado24Data.push(formatValue(dado.acumulado24h, 1) || null);
                        acumulado12Data.push(formatValue(dado.acumulado12h, 1) || null);

                        if (dado.acumulado12h > maiorAcumulado) maiorAcumulado = dado.acumulado12h;
                        if (dado.acumulado24h > maiorAcumulado) maiorAcumulado = dado.acumulado24h;
                    }

                    nivelRioDataNormal.push(formatValue(nivelNormal, 2) || null);
                    nivelRioDataAlerta.push(formatValue(nivelAlerta, 2) || null);
                } else {
                    nivelRioData.push(null); // Janela sem dados
                    nivelRioDataNormal.push(formatValue(nivelNormal, 2) || null);
                    nivelRioDataAlerta.push(formatValue(nivelAlerta, 2) || null);
                }
            });

            const ctx = canvas.getContext('2d');

            let chtData = {
                labels: labels, // Usar os rótulos preparados
                datasets: [{
                    type: modoNivel ? 'line' : 'bar',
                    pointStyle: modoNivel ? 'circle' : 'rect',
                    label: modoNivel ? 'Nível do Rio (m)' : 'Chuva (mm/h)',
                    data: nivelRioData, // Usar os dados preparados
                    borderColor: 'rgba(75, 192, 192, 1)',
                    backgroundColor: 'rgba(75, 192, 192, 0.2)',
                    borderWidth: 2,
                    pointRadius: 3,
                    pointBackgroundColor: 'rgba(75, 192, 192, 1)',
                    pointBorderColor: '#fff'
                }]
            };
            if (!modoNivel) {
                chtData.datasets.push(
                    {
                        type: 'line',
                        pointStyle: 'circle',
                        label: 'Acumulado 12h (mm)',
                        data: acumulado12Data,
                        borderColor: 'rgb(54, 162, 235)',
                        backgroundColor: 'rgb(54, 162, 235,0.5)',
                        borderWidth: 2,
                        pointRadius: 3,
                        pointBackgroundColor: 'rgb(54, 162, 235)',
                        pointBorderColor: '#fff',
                    })
                chtData.datasets.push(
                    {
                        type: 'line',
                        pointStyle: 'circle',
                        label: 'Acumulado 24h (mm)',
                        data: acumulado24Data,
                        borderColor: 'aquamarine',
                        backgroundColor: 'aquamarine',
                        borderWidth: 2,
                        pointRadius: 3,
                        pointBackgroundColor: 'aquamarine',
                        pointBorderColor: '#fff',
                        hidden: true,
                    })
            }
            if (nivelNormal) {
                chtData.datasets.push({
                    type: 'line',
                    pointStyle: 'rect',
                    label: 'Normal',
                    data: nivelRioDataNormal, // Usar os dados preparados
                    borderColor: 'gray',
                    backgroundColor: 'gray',
                    borderWidth: 2,
                    pointRadius: 1,
                    pointBackgroundColor: 'gray',
                    pointBorderColor: '#fff'
                });
            }
            if (nivelAlerta) {
                chtData.datasets.push({
                    type: 'line',
                    pointStyle: 'rect',
                    label: modoNivel ? 'Alerta (m)' : 'Alerta (mm)',
                    data: nivelRioDataAlerta, // Usar os dados preparados
                    color: 'yellow',
                    borderColor: 'yellow',
                    backgroundColor: 'yellow',
                    borderWidth: 2,
                    pointRadius: 1,
                    pointBackgroundColor: 'yellow',
                    pointBorderColor: '#fff',
                    hidden: modoNivel ? maiorNivel < (nivelAlerta / 3) : maiorAcumulado < (nivelAlerta / 3), // Se chuva: Apenas se passar de 1/3
                });
            }

            new Chart(ctx, {
                data: chtData,
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            labels: {
                                color: 'white', // Cor dos nomes das séries na legenda
                                usePointStyle: true,
                            },
                            display: true,
                            position: 'top',
                        },
                        tooltip: {
                            mode: 'index',
                            intersect: false
                        }
                    },
                    scales: {
                        x: {
                            title: {
                                display: true,
                                text: 'Hora',
                                color: 'white'
                            },
                            ticks: {
                                color: 'white' // Cor dos textos do eixo X
                            }
                        },
                        y: {
                            title: {
                                display: true,
                                text: modoNivel ? 'Nível do Rio (m)' : 'Chuva (mm/h)',
                                color: 'white'
                            },
                            ticks: {
                                color: 'white' // Cor dos textos do eixo X
                            },
                            beginAtZero: true
                        }
                    }
                }
            });

        })
        .catch(error => {
            console.error('Erro ao carregar dados das estações:', error);
        });
}

function carregaChuvaEstacao(estacao, id) {
    fetch('/estacoes/agregado?hour=2&estacao=' + estacao)
        .then(response => response.json())
        .then(data => {
            data = data;
            const span = document.querySelector('#' + id);
            span.innerHTML = `${formatValue(data.precipitacaoTotal_Hora / 2, 1) || '-'}mm/h`; // Em 4h
        });
}
function carregaPrevisao() {
    fetch('/weather')
        .then(response => response.json())
        .then(data => {
            // Criar cards visuais
            const cardsContainer = document.querySelector('#previsaoCards');
            cardsContainer.innerHTML = ''; // Limpar container

            if (data.length > 0) {
                const dataHora = new Date(data[0].coletaUTC + 'Z').toLocaleString('pt-BR');
                const spanDH = document.querySelector('#dataHoraColetaPrevisao');
                spanDH.textContent = `Coleta dos Dados: ${dataHora}`;
            }

            data.forEach(item => {
                const card = document.createElement('div');
                card.style.cssText = `
                        background-color: whitesmoke;
                        border: 2px solid white;
                        border-radius: 10px;
                        padding: 10px;
                        width: 90px;
                        min-height: 110px;
                        text-align: center;
                        box-shadow: 4px 2px 5px rgba(0,0,0,0.9);
                        font-size: 1.1em;
                    `;
                let dataHora = new Date(item.forecastUTC + 'Z').toLocaleString('pt-BR', {
                    hour: '2-digit',
                    minute: '2-digit'
                });

                let iconRain = 'bi-cloud-slash';
                if (item.precipitacao > 0.1) iconRain = 'bi-cloud';
                if (item.precipitacao > 0.5) iconRain = 'bi-cloud-drizzle';
                if (item.precipitacao > 4) iconRain = 'bi-cloud-rain';
                if (item.precipitacao > 9) iconRain = 'bi-cloud-rain-heavy';

                let tagProb = item.precipitacao > 0.5 ? `<small style="color: #2980b9;">(${item.precipitacaoProb.toFixed(0)}%)</small>` : ''
                // Na Beaufort Wind Scale
                //  "Brisa leve" é de 4 a 7km/h (Wind felt on face; leaves rustle; ordinary vanes moved by wind)
                //  "Brisa gentil" é de 8 a 12km/h (Leaves and small twigs in constant motion; wind extends light flag.)
                //  "Brisa moderada" é de 13 a 18km/h (Raises dust and loose paper; small branches are moved.)
                //  ...
                //  "Quase vendaval" é de 32 a 38km/h (Whole trees in motion; inconvenience felt when walking against the wind.)
                //  "Vendaval" é de 39 a 46km/h (Breaks twigs off trees; generally impedes progress.)
                //  Vou exibir a partir da faixa superior da leve, em 6
                let corVento = "teal";
                if (item.ventoVelocidade > 30) corVento = "red";
                let tagVento = item.ventoVelocidade > 6 ? `<div style="margin: 5px 0;"> <span style="color: ${corVento};"><i class="bi bi-wind"></i> ${item.ventoVelocidade.toFixed(0)} km/h</span> </div>` : ''

                let strTemp = item.temperatura < 10 ? item.temperatura.toFixed(1) : item.temperatura.toFixed(0);
                card.innerHTML = `
                        <div style="font-weight: bold; margin-bottom: 10px; color: black;">${dataHora}</div>
                        <div style="font-size: 1.5em; color: #d9534f;">${strTemp}°C</div>

                        ${tagVento}
                        <div style="margin: 5px 0;">
                            <span style="color: navy;"><i class="bi ${iconRain}"></i> ${item.precipitacao.toFixed(1)} mm</span>
                            <!-- ${tagProb} -->
                        </div>
                    `;
                cardsContainer.appendChild(card);
            });

            const btn = document.createElement('button');
            btn.textContent = "Carregar mais";
            btn.onclick = () => {
                // Limpa card
                cardsContainer.innerHTML = 'Consultando previsão estendida, aguarde ...';
                setTimeout(function () {
                    /*carregaPrevisao(true);*/
                    window.location = "/tempo.html"
                }, 5000); // Wait
            };
            cardsContainer.appendChild(btn);
        })
        .catch(error => {
            console.error('Erro ao buscar previsão:', error);
            document.querySelector('#previsaoTempo').innerHTML = '<p style="text-align: center; color: red; font-size: 1.2em;">Erro ao carregar previsão do tempo.</p>';
        });
}

function adicionaEstacao(lst, lat, lng, label, id) {
    let e = addCircleLabel(lat, lng, label, `/live.html?estacao=${id}`);
    e.id = `mcE_${id}`;
    lst.push(e);
}
function atualizaEstacao(lst, label, id) {
    // procurar o id na lst e setar o valor com label
    let e = lst.find(el => el.id === `mcE_${id}`);

    if (e === null || e === undefined) return;

    e._icon.innerHTML = label;
}
function addCircleLabel(lat, lng, label, url) {
    // Adiciona o texto dentro do círculo
    var circle = L.marker([lat, lng], {
        icon: L.divIcon({
            className: 'circle-label',
            html: label,
            iconSize: [55, 55],
            iconAnchor: [20, 20] // Centralizar o texto no ponto
        })
    }).addTo(map);

    if (url !== null) {
        circle.on('click', function () {
            window.open(url, '_blank');
        });
    }

    return circle;
}