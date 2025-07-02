function formatValue(value, decimals = 0) {
    if (value == null) return '-'; // Verifica null ou undefined
    if (isNaN(value)) return '-'; // Verifica null ou undefined
    return decimals === 0 ? Math.round(value) : value.toFixed(decimals);
}
function formatValueUnit(value, decimals, unit) {
    if (value == null) return '-'; // Verifica null ou undefined
    return (decimals === 0 ? Math.round(value) : value.toFixed(decimals)) +""+ unit;
}
function timeSince(date) {
    const now = new Date();
    const seconds = Math.round((now - new Date(date)) / 1000);

    const intervals = [
        { unit: 'year', seconds: 60 * 60 * 24 * 365 },
        { unit: 'month', seconds: 60 * 60 * 24 * 30 },
        { unit: 'day', seconds: 60 * 60 * 24 },
        { unit: 'hour', seconds: 60 * 60 },
        { unit: 'minute', seconds: 60 },
        { unit: 'second', seconds: 1 }
    ];

    for (const { unit, seconds: intervalSeconds } of intervals) {
        const count = Math.floor(seconds / intervalSeconds);
        if (count >= 1) {
            const rtf = new Intl.RelativeTimeFormat('pt-BR', { numeric: 'auto' });
            return rtf.format(-count, unit); // Negativo porque é no passado
        }
    }
    return 'agora';
}

function getDateTimeForTimezoneHour(currentString, tz) {
    // Ajustar o horário para o timezone especificado (tz é em horas)
    const offsetMs = tz * 60 * 60 * 1000; // Converte horas para milissegundos

    if (!currentString.endsWith('Z')) currentString = currentString + 'Z';

    const current = new Date(currentString);
    const adjustedTime = new Date(current.getTime() + offsetMs);

    // Extrair componentes da data e hora em UTC (já ajustado)
    const year = adjustedTime.getUTCFullYear();
    const month = String(adjustedTime.getUTCMonth()+1).padStart(2, '0');
    const day = String(adjustedTime.getUTCDate()).padStart(2, '0');
    const hours = String(adjustedTime.getUTCHours()).padStart(2, '0');

    if (hours == NaN || hours === NaN) debugger;

    // Formatar como "YYYY-MM-DD HHh" (sem minutos)
    const formattedDateTime = `${day}/${month} ${hours}h`;

    return formattedDateTime;
}

function wifiSignalToPercent(signal) {
    if (signal == null || isNaN(signal)) return '-'; // Retorna '-' para valores inválidos
    if (signal === undefined) return '-';
    if (signal === 0) return '-';

    const MIN_SIGNAL = -90; // Nível mínimo
    const MAX_SIGNAL = -50;  // Nível máximo

    // Garante que o nível esteja dentro do intervalo esperado
    const clampedSignal = Math.min(Math.max(signal, MIN_SIGNAL), MAX_SIGNAL);

    // Converte o nível para percentual
    const percent = ((clampedSignal - MIN_SIGNAL) / (MAX_SIGNAL - MIN_SIGNAL)) * 100;
    return Math.round(percent) + '%';
}

function getQueryParam(name) {
    const queryString = window.location.search;
    const urlParams = new URLSearchParams(queryString);
    return urlParams.get(name);
}

function nullSlice(value, size, def) {
    if (value === null) return def;
    if (value === undefined) return def;

    return value.slice(size);
}
function abrirEstacao(estacao) {
    window.open('/live.html?estacao=' + estacao, '_blank');
}