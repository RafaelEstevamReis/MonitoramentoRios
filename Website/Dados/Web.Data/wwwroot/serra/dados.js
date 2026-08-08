/* ========================================================================
   ACESSO E NORMALIZACAO DE DADOS
   Busca na API, monta series, resume rede e bacias. Nao toca no DOM.
   ======================================================================== */

import { APP } from "./estado.js";
import { API, API_FC, HMAX, REG, REGBY, BACIAS, LEVEL_TRUST, LEVEL_CHECK,
         HUM_UNTRUSTED, LV_MAXAGE, LV_MINCOV } from "./config.js";
import { fmt, num, sum, tsOf, lastNonNull, nmin, nmax, hkLabel, fetchJSON,
         diaMes, horaMin } from "./util.js";
import { ffill, despike, smoothLevel, confirmSeries, robustSlope } from "./serie.js";
/* Umidade que a interface pode publicar. Duas regras num lugar so: leitura
   abaixo de 5 % e ruido de sensor, e estacao na lista de sensor descalibrado
   nao entra. Estava escrito duas vezes (serie horaria e leitura instantanea),
   com a mesma condicao copiada — se uma mudasse, a outra mentia. */
function umidadeValida(reg, valor){
  if(valor==null) return null;
  return (valor<5 || HUM_UNTRUSTED.has(reg.code)) ? null : valor;
}


/* ===== carregar estacoes (96 h) =============================================
   Era uma funcao de 96 linhas fazendo cinco coisas: buscar, alinhar a janela de
   horas, extrair series, tratar nivel e montar o objeto por estacao. Agora cada
   etapa tem nome; loadStations so encadeia. */

async function buscaUltimos(){
  try{ return await fetchJSON(API+"/estacoes/ultimos"); }catch(e){ return []; }
}
function buscaSerieHoraria(){
  return Promise.all(REG.map(s=>
    fetchJSON(API+"/estacoes/lastHourly?lastHours="+HMAX+"&estacao="+s.id)
      .then(d=>({id:s.id, rows:Array.isArray(d)?d.filter(Boolean):[]}))
      .catch(()=>({id:s.id, rows:[]}))));
}
/* A janela termina no bucket mais novo que QUALQUER estacao reportou; sem dado
   nenhum, cai no relogio. */
function ultimoBucket(hourly){
  let maxHK=0;
  hourly.forEach(h=>h.rows.forEach(r=>{ if(r.hourKey>maxHK) maxHK=r.hourKey; }));
  return maxHK || Math.floor(Date.now()/3600000);
}
/* Series cruas alinhadas na janela: um valor por hora, null onde nao houve
   leitura (nunca 0, que seria "medi e deu zero"). */
function seriesDaEstacao(reg, rows, startHK){
  const byHK={}; rows.forEach(r=>byHK[r.hourKey]=r);
  const chuva=[], nivel=[], temp=[], umid=[], press=[];
  for(let i=0;i<HMAX;i++){
    const r=byHK[startHK+i];
    chuva.push(r?num(r.precipitacaoTotal_Hora):null);
    let lv=r?num(r.nivelRio_AVG):null; if(lv!=null&&lv<0) lv=null;
    nivel.push(lv);
    temp.push(r?num(r.temperaturaAr_AVG):null);
    umid.push(umidadeValida(reg, r?num(r.umidadeAr_AVG):null));
    press.push(r?num(r.pressaoAr_AVG):null);
  }
  return {chuva, nivel, temp, umid, press};
}
/* Idade e cobertura da regua. Sem isso uma regua que reporta uma vez por semana
   comandava veredito com dado de dias atras. */
function frescorDaRegua(nivelCru){
  let idade=null, cobertura=0;
  for(let i=HMAX-1;i>=0;i--){ if(nivelCru[i]!=null){ idade=HMAX-1-i; break; } }
  for(let i=Math.max(0,HMAX-12);i<HMAX;i++) if(nivelCru[i]!=null) cobertura++;
  const fresca = idade!=null && idade<=LV_MAXAGE && cobertura>=LV_MINCOV;
  return {idade, cobertura, fresca};
}
/* Leitura instantanea so entra em regua limpa e se coerente com a ULTIMA
   LEITURA CRUA (media da hora em curso). Comparar com a serie filtrada
   garantia rejeicao justo durante subida rapida, que e quando a leitura
   fresca mais vale — por isso a folga de 0,20 m: cobre o pior caso real de
   subida dentro da mesma hora.
   Essa mesma folga deixa passar RUIDO de um pacote unico quando o rio esta
   comprovadamente caindo: 29/07 21h36 UTC a BE01 reportou 0,72 m (media da
   hora corrente ate ali 0,654 m, tendencia -2,1 cm/h, sem chuva ha horas) e
   o proximo pacote, 7 min depois, voltou a 0,648 m — o rio nao inverteu, foi
   leitura ruim. Sem checagem, isso sozinho virou "rio >= 100 % da cota,
   ponto" (regra terminal do canal rio, sem memoria nem recuo) e a pagina
   gritou enchente com o rio descendo ha horas. Quando a tendencia ja
   confirma queda (mesmo limiar do recuo em enchente.js: -2 cm/h), uma
   reversao para cima so entra com folga bem mais apertada — o suficiente
   para arredondamento de sensor, pequena demais para o ruido medido. Fora
   dessa situacao (subindo, estavel ou tendencia ainda desconhecida) a folga
   de 0,20 m continua igual — subida real nao pode esperar confirmacao. */
const REV_QUEDA=2, REV_TOL=0.03;
function nivelAgora(suave, nivelCru, instantaneo, trend){
  const base=lastNonNull(suave.series), cru=lastNonNull(nivelCru);
  const contraQueda = trend!=null && trend<=-REV_QUEDA && cru!=null && instantaneo>cru;
  const tol = contraQueda ? REV_TOL : 0.20;
  const aceita = !suave.win && instantaneo!=null && (cru==null || Math.abs(instantaneo-cru)<=tol);
  return aceita ? instantaneo : base;
}
/* Tendencia por Theil-Sen (m/h -> cm/h). Regua suja precisa de janela longa: em
   6 h o ruido de 35 cm/h vira "subindo 38 cm/h" do nada. */
function tendenciaDoNivel(suave){
  const janela=suave.win?12:6;
  const incl=robustSlope(suave.series.slice(Math.max(0,HMAX-janela)));
  return incl==null ? null : Math.round(incl*100*10)/10;
}
/* Fracao da cota SEM TETO. O motor antigo fazia clamp(nivel/cota,0,1) e so usava
   isso como desconto de ate 20 % no limiar de chuva: em 29/07 a BE01 chegou a
   1,24 m com cota de 0,70 m (177 %) e mexeu 20 %. Rio acima da cota E a
   enchente, nao um modificador dela. */
function fracaoDaCota(cota, nivelExibido, confirmada){
  const conf=lastNonNull(confirmada);
  /* vale o maior entre nivel exibido e nivel confirmado: o filtro pode atrasar
     o degrau, a confirmacao nao pode ser atrasada pelo filtro. */
  const melhor=Math.max(nivelExibido!=null?nivelExibido:-Infinity, conf!=null?conf:-Infinity);
  const agora = isFinite(melhor) ? melhor/cota : null;
  /* Pico da cota nas ultimas 6 h: agua de enchente escoa devagar, uma leitura
     mais baixa nao pode apagar o alarme (foi o que fez o card voltar de
     "acontecendo" para "improvavel" as 06h e 07h). */
  let pico6=agora;
  for(let i=Math.max(0,HMAX-6);i<HMAX;i++){
    if(confirmada[i]==null) continue;
    const f=confirmada[i]/cota;
    if(pico6==null||f>pico6) pico6=f;
  }
  return {agora, pico6};
}
/* Chuva acumulada desde o comeco do evento: soma para tras enquanto nao houver
   mais de 2 h seguidas sem chuva. */
function chuvaDoEvento(chuva){
  let total=0, seco=0;
  for(let i=HMAX-1;i>=0;i--){
    if(chuva[i]>0.1){ total+=chuva[i]; seco=0; }
    else { seco++; if(seco>2) break; }
  }
  return total;
}
function ultimoIndiceComDado(serie){
  for(let i=serie.length-1;i>=0;i--) if(serie[i]!=null) return i;
  return null;
}

/* Leitura instantanea (pacote MQTT mais recente da estacao), separada do
   registro por ser outro conceito: aqui e "o que a estacao disse agora", nao
   serie tratada. */
function leituraInstantanea(reg, ultimo, chuva, nowLevel){
  return {
    rainHora: chuva?(lastNonNull(chuva)||0):null,
    rain10: num(ultimo.precipitacao10min),
    ts: tsOf(ultimo.dataHoraDadosUTC),
    temp: num(ultimo.temperaturaAr),
    hum: umidadeValida(reg, num(ultimo.umidadeAr)),
    press: num(ultimo.pressaoAr),
    nivel: nowLevel
  };
}
/* temPacoteFresco vem explicito de quem chama: nao da para inferir de `ultimo`,
   porque a ausencia de registro chega aqui como objeto vazio. */
function montaEstacao(reg, rows, ultimo, temPacoteFresco, startHK, maxHK){
  const S=seriesDaEstacao(reg, rows, startHK);
  const temChuva = S.chuva.some(v=>v!=null) || num(ultimo.precipitacao10min)!=null;
  const temNivel = S.nivel.some(v=>v!=null);
  // null = estacao nao mede chuva (nao vira 0 mm falso)
  const chuva = temChuva ? S.chuva.map(v=>v==null?0:v) : null;
  /* despike (pico isolado) + suavizacao adaptativa (ruido continuo).
     A serie limpa alimenta grafico, min/max de 96 h e tendencia. */
  const suave = temNivel ? smoothLevel(despike(ffill(S.nivel))) : null;
  const nivel = suave ? suave.series : null;
  const regua = temNivel ? frescorDaRegua(S.nivel) : {idade:null, cobertura:0, fresca:false};
  /* Nivel de DECISAO: minimo de duas leituras cruas consecutivas.
     O despike Hampel compara cada ponto com a mediana da vizinhanca — e na
     BORDA da serie a vizinhanca e so passado, entao um degrau verdadeiro parece
     outlier e leva corte. Aconteceu no evento: 29/07 02h a BE01 mediu 1,15 m
     (164 % da cota) e a serie filtrada entregava 0,66 m (94 %), subestimando o
     rio em 74 % na hora em que ele estourava. O minimo de duas leituras seguidas
     resolve os dois lados: pico solto de telemetria morre (o vizinho baixo
     manda) e subida real passa inteira, porque nenhum valor entra sem ter sido
     medido duas vezes em sequencia. */
  const confirmada = temNivel ? confirmSeries(S.nivel) : null;
  const trend = (nivel && regua.fresca) ? tendenciaDoNivel(suave) : null;
  const nowLevel = regua.fresca ? nivelAgora(suave, S.nivel, num(ultimo.nivelRio), trend) : null;
  const cota = (regua.fresca && reg.cotaAlerta) ? fracaoDaCota(reg.cotaAlerta, nowLevel, confirmada) : {agora:null, pico6:null};
  return {
    reg,
    live: rows.some(r=>r.hourKey>=maxHK-3) || temPacoteFresco,
    hasRain: temChuva, hasLevel: temNivel,
    rainIdxLast: temChuva ? ultimoIndiceComDado(S.chuva) : null,
    levelTrust: temNivel&&LEVEL_TRUST.has(reg.code),
    levelCheck: temNivel&&LEVEL_CHECK.has(reg.code),
    lvAge: regua.idade, lvCov: regua.cobertura, lvFresh: regua.fresca,
    cotaFrac: cota.agora, cotaFrac6: cota.pico6,
    rain: chuva, level: nivel, tempH:S.temp, humH:S.umid, pressH:S.press,
    nowLevel, trend,
    rain12: chuva?sum(chuva.slice(HMAX-12)):null,
    rain24: chuva?sum(chuva.slice(HMAX-24)):null,
    rain72prior: chuva?sum(chuva.slice(Math.max(0,HMAX-84),HMAX-12)):null,
    sinceStart: chuva?chuvaDoEvento(chuva):null,
    lvMin: nmin(nivel), lvMax: nmax(nivel),
    now: leituraInstantanea(reg, ultimo, chuva, nowLevel)
  };
}
export async function loadStations(){
  const [ultimos, hourly]=await Promise.all([buscaUltimos(), buscaSerieHoraria()]);
  const porId={}; (ultimos||[]).forEach(u=>porId[u.estacao]=u);
  const maxHK=ultimoBucket(hourly), startHK=maxHK-(HMAX-1);
  APP.labels=[];
  for(let i=0;i<HMAX;i++) APP.labels.push(hkLabel(startHK+i));
  const out={}; let algumDado=false;
  hourly.forEach(h=>{
    const reg=REGBY[h.id], ultimo=porId[h.id]||{};
    const temPacoteFresco=!!porId[h.id];
    if(h.rows.length||temPacoteFresco) algumDado=true;
    out[reg.code]=montaEstacao(reg, h.rows, ultimo, temPacoteFresco, startHK, maxHK);
  });
  APP.ST=out; APP.ST.__anyData=algumDado;
  const d=new Date(); APP.gen=diaMes(d)+"/"+d.getFullYear()+" "+horaMin(d);
}

function pickGauge(arr){ let g=null; arr.forEach(s=>{ const f=s.reg.cotaAlerta?s.nowLevel/s.reg.cotaAlerta:0, gf=g?(g.reg.cotaAlerta?g.nowLevel/g.reg.cotaAlerta:0):-1; if(f>gf) g=s; }); return g; }
function maxOf(arr,f){ return arr.length?Math.max.apply(null,arr.map(x=>f(x)||0)):null; }

/* Estado de chuva da estacao para o panorama.
   O bucket horario zera na virada da hora: sem uma janela de tolerancia o
   efeito sumia mesmo tendo chovido 20 min atras. Entao:
     h=0  -> chovendo agora (pacote fresco com 10 min de chuva, ou bucket da hora)
     h1..3 -> choveu ha pouco, efeito atenuado
   null  -> seco. Idade conta a partir do ultimo bucket com dado DA ESTACAO,
   nao do relogio da rede (estacao atrasada nao pode fingir chuva atual). */
const RAIN_MIN=0.05, RAIN_RECENT_H=3;
export function rainPulse(s){
  if(!s||!s.hasRain||!s.live||s.rainIdxLast==null) return null;
  const fresh=s.now.ts!=null&&(Date.now()-s.now.ts)<=30*60*1000;
  const r10=(fresh&&s.now.rain10!=null)?s.now.rain10*6:0;
  if(r10>RAIN_MIN) return { mm:r10, h:0, now:true };
  const lag=HMAX-1-s.rainIdxLast;
  for(let k=0;k<=RAIN_RECENT_H;k++){
    const i=s.rainIdxLast-k; if(i<0) break;
    const v=s.rain[i]||0;
    if(v>RAIN_MIN){ const h=k+lag; return h<=RAIN_RECENT_H? { mm:v, h, now:h===0 } : null; }
  }
  return null;
}
export function rainNowOf(s){ const q=rainPulse(s); return (q&&q.now)?q.mm:0; }

export function aggregateBasins(){
  APP.BAS={};
  Object.values(BACIAS).forEach(b=>{
    const all=REG.filter(r=>r.bacia===b.key).map(r=>APP.ST[r.code]).filter(Boolean);
    const live=all.filter(s=>s.live);
    const rainSt=live.filter(s=>s.hasRain).sort((a,c)=>(c.rain24||0)-(a.rain24||0));
    const levelSt=live.filter(s=>s.hasLevel&&s.nowLevel!=null)
                      .sort((a,c)=>(c.levelTrust?1:0)-(a.levelTrust?1:0));
    const trusted=levelSt.filter(s=>s.levelTrust);
    const gauge=pickGauge(trusted)||pickGauge(levelSt.filter(s=>!s.levelCheck))||null;
    const pulses=rainSt.map(s=>({s,q:rainPulse(s)})).filter(o=>o.q);
    const rainingNow=pulses.filter(o=>o.q.now).map(o=>o.s).sort((a,c)=>rainNowOf(c)-rainNowOf(a));
    const rainRecent=pulses.filter(o=>!o.q.now).map(o=>o.s);
    APP.BAS[b.key]={ meta:b, all, live, rainSt, levelSt, trusted, gauge, rainingNow, rainRecent,
      rainNowMax:maxOf(rainSt,s=>rainNowOf(s)),
      rain12:maxOf(rainSt,s=>s.rain12), rain24:maxOf(rainSt,s=>s.rain24),
      offline:all.filter(s=>!s.live), totalCount:all.length };
  });
}

export function netSummary(){
  const all=REG.map(r=>APP.ST[r.code]).filter(Boolean);
  const live=all.filter(s=>s.live);
  const rainSt=live.filter(s=>s.hasRain).sort((a,c)=>(c.rain24||0)-(a.rain24||0));
  const gauges=live.filter(s=>s.hasLevel&&s.nowLevel!=null)
                   .sort((a,c)=>(c.levelTrust?1:0)-(a.levelTrust?1:0));
  return { total:REG.length, live:live.length, rainSt, rain:rainSt.length, gauges,
    trusted:gauges.filter(s=>s.levelTrust), check:gauges.filter(s=>s.levelCheck),
    offline:all.filter(s=>!s.live),
    rain12:maxOf(rainSt,s=>s.rain12), rain24:maxOf(rainSt,s=>s.rain24),
    rain72prior:maxOf(rainSt,s=>s.rain72prior),
    rainNow:maxOf(rainSt,s=>rainNowOf(s)), rainingNow:rainSt.filter(s=>rainNowOf(s)>0.05) };
}

/* ===== previsao (endpoint proprio, alimentado pelo WeatherOpenMeteo worker) ===
   Chuva abaixo de PREVISAO_MIN e traco, nao evento. E um punhado de horas de
   0,1 a 0,2 mm somando menos de EVENTO_MIN tambem nao e evento: o painel
   anunciava "Proxima chuva hoje 17h · ~0 mm no evento", que ocupa o card mais
   visivel da previsao para dizer que nao vai chover. So conta chuva que da para
   sentir. */
const PREVISAO_MIN=0.2, EVENTO_PAUSA_H=6, EVENTO_MIN=5;

/* Primeira linha que ainda vale como "agora": a hora corrente ou a anterior. */
function primeiraLinhaValida(horas, agora){
  for(let i=0;i<horas.length;i++) if(new Date(horas[i]).getTime()>=agora-3600000) return i;
  return 0;
}
/* Proxima chuva significativa: cada candidato comeca na primeira hora molhada e
   segue somando ate dar EVENTO_PAUSA_H horas secas seguidas; candidato que nao
   alcanca EVENTO_MIN e descartado e a busca continua na hora seguinte a ele. */
function proximoEvento(horas, mm, inicio){
  let i=inicio;
  while(i<horas.length){
    while(i<horas.length && (mm[i]||0)<PREVISAO_MIN) i++;
    if(i>=horas.length) return null;
    let total=0, seco=0, fim=i, j=i;
    for(;j<horas.length;j++){
      const p=mm[j]||0;
      if(p>=PREVISAO_MIN){ total+=p; fim=j; seco=0; }
      else { seco++; if(seco>=EVENTO_PAUSA_H) break; }
    }
    if(total>=EVENTO_MIN) return {start:horas[i], end:horas[fim], mm:total};
    i=fim+1;
  }
  return null;
}
/* Agrega por dia em horario local (BRT, UTC-3), igual ao
   "timezone=America/Sao_Paulo" da chamada antiga.
   168 h convertidas para BRT caem em 8 datas (a primeira e a ultima parciais).
   O painel mostra hoje + 6 dias: corta a cauda parcial.
   Comeca em `inicio` (primeira hora ainda valida), nao em 0: a API devolve a
   janela toda, com horas de HOJE que ja passaram. Somar a partir de 0 juntava
   chuva ja caida com chuva prevista — "hoje" acumulava a chuva da manha (ja
   medida, ja no card de "chuva ate agora") e o painel dizia "previsao aponta
   36 mm hoje" as 18h, quando nao sobrava nenhuma hora de chuva no dia. */
function agregaPorDia(horas, mm, rows, inicio){
  const porDia=new Map();
  for(let k=inicio;k<horas.length;k++){
    const dia=new Date(new Date(horas[k]).getTime()-3*3600000).toISOString().slice(0,10);
    if(!porDia.has(dia)) porDia.set(dia,{mm:0,prob:0,windMax:0});
    const e=porDia.get(dia);
    e.mm += mm[k]||0;
    e.prob = Math.max(e.prob, rows[k].precipitacaoProb||0);
    e.windMax = Math.max(e.windMax, rows[k].ventoVelocidade||0);
  }
  return Array.from(porDia,([date,v])=>({date, mm:v.mm, prob:v.prob, windMax:v.windMax})).slice(0,7);
}
function diaDePico(dias){
  let pico=null;
  dias.slice(0,6).forEach(d=>{ if(!pico||d.mm>pico.mm) pico=d; });
  return pico;
}
/* Mesmo horizonte do pico de chuva (6 dias): dia com o vento mais forte
   previsto. Quem decide se isso vira frase e o chamador (compara com
   VENTO_LIMIAR) — aqui e so o dado. */
function diaDeVento(dias){
  let d=null;
  dias.slice(0,6).forEach(x=>{ if(!d||x.windMax>d.windMax) d=x; });
  return d;
}
/* "chuva agora pela previsao" so vale se a linha casada estiver mesmo na hora
   corrente. Sem essa guarda, tabela de previsao adiantada ou parada fazia o
   indice cair na linha 0 e a pagina anunciava "Chovendo agora" com o valor de
   outro dia — e ainda subia o nivel de risco por isso. */
function chuvaAgora(horas, mm, inicio, agora){
  return Math.abs(new Date(horas[inicio]).getTime()-agora)<=5400000 ? (mm[inicio]||0) : 0;
}
export async function loadForecast(){
  try{
    const rows=await fetchJSON(API_FC+"/weather/ext?hours=168");
    if(!rows || !rows.length){ APP.FC=null; return; }
    const horas=rows.map(r=>r.forecastUTC.endsWith("Z")?r.forecastUTC:r.forecastUTC+"Z");
    const mm=rows.map(r=>r.precipitacao||0);
    /* item.ventoVelocidade ja vem do /weather/ext (mesmo endpoint que
       tempo.html/rsrl.js ja usam) — so precisa entrar na janela horaria. */
    const vento=rows.map(r=>r.ventoVelocidade||0);
    const agora=Date.now();
    const inicio=primeiraLinhaValida(horas, agora);
    const dias=agregaPorDia(horas, mm, rows, inicio);
    /* o grafico horario leva 3 h de cauda passada: sem isso a linha do "agora"
       cai colada no eixo, ja que a serie comeca na hora corrente. */
    const cauda=Math.max(0,inicio-3);
    APP.FC={
      event: proximoEvento(horas, mm, inicio),
      /* 12 h e a janela FIXA dos cards: medida e prevista na mesma duracao, para
         dar numero comparavel de um dia para o outro. next24 e next72 ficam para
         o motor de risco, que e calibrado em outras duracoes. */
      next12: sum(mm.slice(inicio, inicio+12)),
      next24: sum(mm.slice(inicio, inicio+24)),
      next72: sum(mm.slice(inicio, inicio+72)),
      days: dias,
      peakDay: diaDePico(dias),
      windDay: diaDeVento(dias),
      nowMm: chuvaAgora(horas, mm, inicio, agora),
      hourly: { t:horas.slice(cauda, inicio+48), p:mm.slice(cauda, inicio+48), v:vento.slice(cauda, inicio+48) }
    };
  }catch(e){ APP.FC=null; }
}
