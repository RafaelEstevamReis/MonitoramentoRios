function formatValue(value, decimals = 0) {
    if (value == null) return '-'; // Verifica null ou undefined
    return decimals === 0 ? Math.round(value) : value.toFixed(decimals);
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
function wifiSignalToPercent(signal) {
    if (signal == null || isNaN(signal)) return '-'; // Retorna '-' para valores inválidos

    const MIN_SIGNAL = -95; // Nível mínimo típico
    const MAX_SIGNAL = -50;  // Nível máximo típico

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