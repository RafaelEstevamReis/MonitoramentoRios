/* ========================================================================
   PAINEL
   Le estado, escreve DOM. Nao busca dado nem decide regra.
   ======================================================================== */

import { APP } from "./estado.js";
import { HMAX, CH, REG, REGBY, rioNome, rioArt, VENTO_LIMIAR } from "./config.js";
import { $, fmt, clamp, DOW, diaCurto, horaCurta, quandoTxt, fillGaps, smoothPath } from "./util.js";
import { statusRel, chuvaStatus, rainCls, trendTxt, cotaTxt, pulseTxt } from "./rotulos.js";
import { rainPulse } from "./dados.js";
import { sparkline, prepLines, escala, escalaValor } from "./svg.js";
import { drawBasinCut } from "./graficos.js";

export function renderNetCard(){
  const el=$("#net-card"); if(!el) return;
  const st=(k,v,u)=>'<div class="nc-st">'+k+'<b>'+v+(u?' <small>'+u+'</small>':'')+'</b></div>';
  el.className='netcard';
  el.innerHTML='<div class="nc-top"><div class="nc-ttl">Rede de estações</div>'+
    st("ativas", APP.NET.live, "de "+APP.NET.total)+
    st("medindo chuva", APP.NET.rain, "na análise")+
    st("réguas de nível", APP.NET.gauges.length, APP.NET.trusted.length+" validada"+(APP.NET.trusted.length===1?"":"s"))+
    st("sem sinal 4 h", APP.NET.offline.length, "")+
    '</div>';
}

/* ===== RENDER: risco =========================================================
   Eram 78 linhas escrevendo em quatro lugares do DOM. Separado em: selo,
   manchete e cards. Nenhuma decide regra — o veredito vem pronto de
   enchente.js; aqui so viram texto. */
/* Indice de risco 1-10 (ver enchente.js: escalaRisco/ESCALA_*): 5 faixas de cor,
   ceil(n/2) mapeia 1-2/3-4/5-6/7-8/9-10 pra nivel-1..nivel-5. O numero
   substitui TODA categoria em palavra ("baixo/atencao/alto", "enchente
   grande", "agua baixando") — inclusive quando ja e fato confirmado, por
   pedido do dono do projeto: consistencia visual em vez de dois vocabularios
   diferentes (fato vs. previsao) competindo no mesmo selo. A frase que diz
   O QUE esta acontecendo continua na manchete (function manchete(), abaixo),
   que fica de fato descritiva. */
function faixaDoIndice(n){ return "nivel-"+Math.min(5, Math.max(1, Math.ceil(n/2))); }
/* Nota de calibracao: a escala foi aferida com poucos eventos reais (o
   principal, 29/07/2026) — nao e probabilidade estatistica. O * aponta pro
   aviso no rodape (.disclaimer), sem duplicar o texto em cada card. */
const NOTA_ESCALA='<sup class="idx-nota" title="Escala com poucos dados reais para calibração — ver rodapé">*</sup>';
function renderSelo(R){
  $("#risk-badge").className="risk-badge "+faixaDoIndice(R.level);
  $("#risk-label").innerHTML=R.level+"/10"+NOTA_ESCALA;
  $("#risk-when").textContent = (R.drivenBy==="previsto"&&R.futWhen)
    ? ("motivado pela previsão · "+diaCurto(R.futWhen))
    : "situação agora";
}
/* R.raining tambem liga por rio subindo rapido (maxTrend>=8), nao so por chuva.
   Sem separar, a manchete dizia "Chovendo agora" com 0,0 mm/h — foi o caso as
   06h de 29/07, quando a chuva JA TINHA PARADO e o rio continuava subindo. E o
   aviso mais importante do evento: o pico da cheia chega depois da chuva, e foi
   nessa janela que a saida da cidade fechou. */
function fraseDeChuva(R){
  if(R.obsNow!=null&&R.obsNow>=0.2) return " Chovendo ~"+fmt(R.obsNow,1)+" mm/h medidos.";
  if(APP.FC&&APP.FC.nowMm>=0.3) return " Chovendo ~"+fmt(APP.FC.nowMm,1)+" mm/h pela previsão.";
  if(R.maxTrend>=3) return " A chuva parou, mas o rio ainda está subindo.";
  return "";
}
/* Quando a enchente esta acontecendo, a manchete e A ENCHENTE. Antes o titulo
   dizia "Chovendo agora" com o rio 77 % acima da cota — a chuva e causa, o
   alagamento e o fato, e e o fato que a pessoa precisa ler primeiro. A magnitude
   entra na propria manchete: em 29/07/2026 o centro alagou e a saida da cidade
   ficou trancada, e "acontecendo" nao dava conta de dizer isso.
   Nenhuma frase orienta conduta nem aponta local: a pagina informa o que as
   estacoes medem, e o aviso oficial e da Defesa Civil (dito uma vez no rodape). */
function manchete(R){
  const F=R.flood, chove=fraseDeChuva(R);
  if(F.ok&&F.level>=3) return (F.sev>=2?"Enchente grande na cidade.":"Enchente na cidade.")+chove;
  if(F.ok&&F.recuo&&F.rf>=1) return "A água está baixando.";
  if(R.raining){
    const quanto = R.obsNow ? (" (~"+fmt(R.obsNow,1)+" mm/h medidos)")
                 : (APP.FC&&APP.FC.nowMm) ? (" (~"+fmt(APP.FC.nowMm,1)+" mm/h pela previsão)")
                 : "";
    return "Chovendo agora"+quanto+".";
  }
  const pico=APP.FC&&APP.FC.peakDay;
  const vento=APP.FC&&APP.FC.windDay;
  /* So entra na frase quando passa do limiar (mesmo do grafico horario) —
     abaixo disso o vento nao muda em nada a leitura do dia. */
  const fraseVento = (vento&&vento.windMax>=VENTO_LIMIAR)
    ? " Rajada forte prevista: ~"+fmt(vento.windMax,0)+" km/h em "+diaCurto(vento.date)+"."
    : "";
  if(pico&&pico.mm>=15)
    return "Sem chuva agora. Previsão aponta ~"+fmt(pico.mm,0)+" mm em "+diaCurto(pico.date)+" (prob. "+fmt(pico.prob,0)+"%)."+fraseVento;
  return "Sem chuva agora. Sem chuva relevante prevista."+fraseVento;
}
/* A linha de apoio ficou com UMA informacao: onde choveu mais forte. Todo o
   resto (rio em relacao ao limite, solo, chuva medida, limite vigente) esta nos
   cards logo abaixo, em numero. Repetir em prosa so aumentava texto: "O Rio
   Areia está 54 cm acima do limite" dizia o mesmo que o card ao lado. */
function linhaDeApoio(R){
  const maisForte=APP.NET.rainSt[0];
  if(R.obs12!=null&&maisForte&&R.obs12>=1) return "A chuva mais forte caiu sobre "+rioArt(maisForte.reg)+".";
  return APP.NET.trusted.length ? "" : "Nenhuma régua de nível com leitura confiável agora.";
}
function cardDeSolo(F){
  const antes = APP.NET.rain72prior!=null ? (" · 3 dias antes: "+fmt(APP.NET.rain72prior,0)+" mm") : "";
  return qk("Chuva no solo", fmt(F.api,0),"mm", "solo "+F.solo+antes, F.sat>=0.5);
}
function cardDeRio(F){
  if(!F.rioSt) return qk("Rio","--","", "sem régua com leitura confiável", false);
  const cm=Math.round((F.rf-1)*F.rioSt.reg.cotaAlerta*100);
  return qk(cm>=0?"Rio acima do limite":"Rio abaixo do limite", String(Math.abs(cm)),"cm",
            rioNome(F.rioSt.reg)+" · "+trendTxt(F.rioSt), F.rf>=0.85);
}
/* JANELA FIXA DE 12 h em todos os cards de chuva. Antes a chuva medida vinha em
   12 h, a de comparacao em 24 h e o limite na duracao que o motor achou mais
   critica naquele instante (3, 6, 12 ou 24 h) — tres janelas na mesma linha.
   Numero de janela movel nao serve para comparar hoje com ontem nem medida com
   limite. O motor continua testando as quatro duracoes por dentro; o que fica
   fixo e o que a tela mostra. */
const CHUVA_MIN=5;   // abaixo disso nao e chuva que se anuncia, e traco
/* O limite vigente e DADO (desce conforme o solo satura) e por isso fica em
   card, ao lado da chuva medida com que se compara — as duas em 12 h. Fora da
   prosa: em texto corrido virava promessa ("bastam X para transbordar"), e a
   pagina nao promete nada — publica medida. */
function cardDeChuvaMedida(R, F){
  const detalhe = R.obs12==null ? "sem estação de chuva"
    : (F.ok ? "limite com este solo: "+fmt(F.lim12,0)+" mm em 12 h" : "medida na rede");
  return qk("Chuva medida 12 h", R.obs12!=null?fmt(R.obs12,1):"--","mm", detalhe, R.obs12!=null&&R.obs12>=25);
}
/* Chuva prevista tambem em 12 h, mesma janela da medida. Abaixo de CHUVA_MIN o
   card diz "quase nada": anunciar "1 mm" como chuva prevista da peso ao que nao
   e chuva. Nao usar "traço" — e jargao de meteorologia, o dono do projeto leu na
   tela e perguntou o que era. */
function cardDeChuvaPrevista(){
  if(!APP.FC) return qk("Chuva prevista 12 h","--","mm","previsão indisponível",false);
  const mm=APP.FC.next12;
  if(mm<CHUVA_MIN) return qk("Chuva prevista 12 h","quase nada","", "menos de "+CHUVA_MIN+" mm em 12 h", false);
  return qk("Chuva prevista 12 h", fmt(mm,0),"mm", "pela previsão", mm>=20);
}
/* "Proxima chuva" so anuncia evento que soma CHUVA_MIN ou mais (o corte esta em
   dados.js/proximoEvento). Sem isso o card dizia "hoje 17h · ~0 mm no evento":
   ocupava o lugar mais visivel da previsao para avisar que nao vai chover. */
function cardDeProximaChuva(){
  const ev=APP.FC&&APP.FC.event;
  if(!ev) return qk("Próxima chuva","sem","evento","nada acima de "+CHUVA_MIN+" mm em 7 dias",false);
  return qk("Próxima chuva", diaCurto(ev.start), horaCurta(ev.start), "~"+fmt(ev.mm,0)+" mm no evento", ev.mm>=40);
}
/* Cards = dado puro, na ordem em que a pergunta se faz: veredito, quanto o solo
   ja guarda, onde esta o rio, quanto choveu, quanto vem. O painel "Como o app
   decidiu" foi retirado: explicar o metodo na cara do usuario nao ajuda quem
   quer o numero, e nao cabe a esta pagina orientar conduta. Os numeros dele
   continuam aqui, como dado. */
function cardsDeRisco(R){
  const F=R.flood, cards=[floodCard(F)];
  if(F.ok) cards.push(cardDeSolo(F), cardDeRio(F));
  cards.push(cardDeChuvaMedida(R,F));
  cards.push(cardDeChuvaPrevista());
  cards.push(cardDeProximaChuva());
  return cards;
}
export function renderRisk(){
  const R=APP.RISK;
  renderSelo(R);
  $("#risk-h").innerHTML=manchete(R);
  $("#risk-drivers").innerHTML=linhaDeApoio(R);
  $("#risk-quick").innerHTML=cardsDeRisco(R).join("");
}

/* Rodape do card diz apenas a BASE do veredito. Os numeros ficam nos cards ao
   lado ("Rio acima do limite · 54 cm", "Chuva no solo · 142 mm"), entao repetir
   em frase era texto a mais dizendo o mesmo. */
function floodCard(F){
  if(!F.ok) return qk("Enchente na cidade","--","","sem chuva medida ou prevista",false);
  /* O VALOR vira o mesmo indice 1-10 do selo (F.indice — fonte unica, ver
     cityFlood() em enchente.js), em vez da palavra-veredito
     (grande/acontecendo/iminente/possivel/improvavel). O "sub" (linha
     pequena de explicacao) continua igual — e descricao de fato, nao
     categoria de risco. O "u" (contexto de tempo: "agora"/"em Xh"/
     "nas proximas 48h") passou a vir SEMPRE preenchido, inclusive nos tres
     ramos de enchente confirmada, que antes deixavam "u" vazio quando a
     magnitude era grande pra a linha nao quebrar com o veredito longo por
     extenso; o valor curto "N/10" nao tem mais esse problema.
     `hl` (realce visual do card) passa a acompanhar o proprio indice
     (>=7, faixas laranja/vermelho) em vez de ligado por branch — antes TODO
     estado "preditivo" (ate "possivel", o mais fraco) ja vinha com hl=true;
     agora so realca quando o numero de fato justifica. */
  const v = F.indice+"/10"+NOTA_ESCALA, hl = F.indice>=7;
  if(F.rio>=3&&F.chuva>=3) return qk("Enchente na cidade",v,"agora", "rio acima do limite e chuva acima do que o solo aguenta", hl);
  if(F.rio>=3)             return qk("Enchente na cidade",v,"agora", "rio acima do limite", hl);
  if(F.chuva>=3)           return qk("Enchente na cidade",v,"agora", "chuva acima do que o solo aguenta", hl);
  if(F.recuo&&F.rf>=1)
    return qk("Enchente na cidade",v,"", "rio ainda acima do limite e descendo, sem chuva há 3 h", hl);
  if(F.etaRio!=null&&F.etaRio<=6&&F.chuva>=2)
    return qk("Enchente na cidade",v, quandoTxt(F.etaRio), "pelo ritmo de subida do rio", hl);
  if(F.eta!=null)
    return qk("Enchente na cidade",v, quandoTxt(F.eta),
      "pela chuva prevista · faltam "+fmt(F.now.falta,0)+" mm", hl);
  return qk("Enchente na cidade",v,"nas próximas 48 h", "chuva prevista abaixo do limite", false);
}

function qk(k,v,u,sub,hl){ return '<div class="qk'+(hl?' hl':'')+'"><div class="k">'+k+'</div><div class="v">'+v+' <small>'+u+'</small></div><div class="sub">'+sub+'</div></div>'; }

/* ===== RENDER: previsao ===== */
export function renderForecast(){
  if(!APP.FC){ $("#fc-next").innerHTML='<div class="tx">Previsão indisponível no momento.</div>'; $("#fc-days").innerHTML=""; return; }
  const ev=APP.FC.event;
  let tx;
  /* Numero, nao adjetivo: "1 mm no evento" informa; "Volume pequeno." era um
     juizo repetindo o mm que ja estava na frase. As barras do dia ja destacam
     volume alto por cor. */
  if(ev) tx='<div class="ic">'+dropSvg()+'</div><div class="tx">Próxima chuva <b>'+diaCurto(ev.start)+', '+horaCurta(ev.start)+'</b> · <span class="big">'+fmt(ev.mm,0)+' mm</span> no evento · ~'+fmt(APP.FC.next72,0)+' mm em 3 dias.</div>';
  else tx='<div class="ic">'+dropSvg()+'</div><div class="tx">Sem chuva relevante prevista · ~'+fmt(APP.FC.next72,0)+' mm em 3 dias.</div>';
  $("#fc-next").innerHTML=tx;
  const mx=Math.max(10,Math.max.apply(null,APP.FC.days.map(d=>d.mm)));
  $("#fc-days").innerHTML=APP.FC.days.map((d,i)=>{ const dt=new Date(d.date+"T12:00"); const h=Math.round((d.mm/mx)*52); const cls=d.mm>=80?"heavy":d.mm>=20?"wet":""; const today=diaCurto(d.date)==="hoje";
    const vento=d.windMax>=VENTO_LIMIAR?windSvg(d.windMax):"";
    return '<div class="fc-d '+cls+(today?' today':'')+'">'+vento+'<div class="dow">'+DOW[dt.getDay()]+'</div><div class="dnum">'+String(dt.getDate()).padStart(2,"0")+'/'+String(dt.getMonth()+1).padStart(2,"0")+'</div><div class="fc-bar"><div class="b" style="--h:'+Math.max(2,h)+'px"></div></div><div class="mm">'+fmt(d.mm,d.mm>=10?0:1)+'</div><div class="pb">'+fmt(d.prob,0)+'%</div></div>'; }).join("");
  $("#fc-note").innerHTML="Barras = mm/dia; % = probabilidade de chuva para Rolante.";
}
function dropSvg(){ return '<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true"><path d="M10 2 C 12.5 6,16 8.5,16 12 a6 6 0 1 1 -12 0 C 4 8.5,7.5 6,10 2 Z" fill="var(--teal)" opacity="0.9"/><path d="M10 2 C 12.5 6,16 8.5,16 12 a6 6 0 1 1 -12 0 C 4 8.5,7.5 6,10 2 Z" fill="none" stroke="var(--teal-deep)" stroke-width="1"/></svg>'; }
/* Mesmo simbolo do grafico horario, so que por dia: um card ganha o badge
   quando o vento maximo daquele dia passa de VENTO_LIMIAR. */
function windSvg(kmh){ return '<div class="fc-wind" title="Rajada forte prevista: ~'+fmt(kmh,0)+' km/h"><svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="var(--alert)" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9.59 4.59A2 2 0 1 1 11 8H2m10.59 11.41A2 2 0 1 0 14 16H2m15.73-8.27A2.5 2.5 0 1 1 19.5 12H2"/></svg></div>'; }

/* ===== RENDER: reguas ===== */

export function renderGauges(){
  const wrap=$("#gauges"), list=APP.NET.gauges;
  if(!list.length){
    wrap.innerHTML='<div class="gauge"><div class="g-nm">Nível dos rios</div><div class="g-num"><span class="big">--</span></div><div class="g-mm">nenhuma régua reportando agora</div></div>';
  } else {
    wrap.innerHTML=list.map((s,idx)=>{
      const lead=idx===0&&s.levelTrust;
      const stt=statusRel(s), trCls=s.trend==null?"calm":(s.trend>=1?"up":(s.trend<=-1?"down":"calm"));
      const tag=s.levelCheck?' <span class="chk">em conferência</span>':'';
      return '<div class="gauge'+(lead?' lead':'')+(s.levelCheck?' check':'')+'"><div class="g-nm">Nível <b>'+s.reg.code+'</b> <span class="lv-status st-'+stt.k+'">'+stt.label+'</span>'+tag+'<br><span class="g-rio">'+s.reg.rio+'</span></div>'+
        '<div class="g-num"><span class="big">'+fmt(s.nowLevel,2)+'</span><span class="u">m</span></div>'+
        '<div class="g-tr '+trCls+'">'+trendTxt(s)+'</div>'+
        '<div class="g-mm">'+cotaTxt(s)+' · 96 h '+fmt(s.lvMin,2)+'–'+fmt(s.lvMax,2)+' m</div>'+
        '<div class="g-spark">'+sparkline(s.level)+'</div></div>';
    }).join("");
  }
}

/* ===== RENDER: bacias ========================================================
   A ordem das bacias estava escrita duas vezes no corpo da funcao: se alguem
   trocasse uma, o desenho saia numa ordem e o corte SVG em outra. */
const ORDEM_BACIAS=["rolante","areia"];
const CLASSE_STATUS={inund:"st-inund", alerta:"st-alerta", atencao:"st-atencao",
                     observa:"st-observa", normal:"st-normal"};

/* Barra por estacao na mesma janela dos cards (12 h): a barra e o numero ao lado
   dela precisam medir a mesma coisa que "Chuva 12 h medida" logo acima, senao a
   estacao com barra cheia nao e a que aparece no total. */
function barraDeEstacao(s, maxMm){
  const mm=s.rain12||0, fracao=clamp(mm/maxMm,0,1);
  return '<div class="sbar'+(mm<0.2?' dry':'')+'">'+
         '<div class="snm">'+s.reg.code+' <i>'+s.reg.rio+'</i></div>'+
         '<div class="track"><div class="f '+rainCls(mm)+'" style="--w:'+(fracao*100).toFixed(0)+'%"></div></div>'+
         '<div class="val">'+fmt(mm,1)+' mm</div></div>';
}
function legendaDeChuvaDaBacia(B){
  if(B.rainingNow.length)
    return '<b class="chovendo">Chovendo agora em '+B.rainingNow.map(s=>s.reg.code).join(", ")+'.</b>';
  if(B.rainRecent.length)
    return '<b>Parou de chover.</b> Choveu há pouco em '+
           B.rainRecent.map(s=>s.reg.code+" ("+rainPulse(s).h+" h)").join(", ")+'.';
  return 'Nenhuma estação da bacia com chuva nas últimas 3 h.';
}
function numerosDaBacia(B, g){
  const complemento = B.rainNowMax ? (' · '+fmt(B.rainNowMax,1)+' mm/h')
                    : (B.rainRecent.length ? (' · '+B.rainRecent.length+' há pouco') : '');
  const bn=(k,v)=>'<div class="bn">'+k+'<b>'+v+'</b></div>';
  return '<div class="basin-nums">'+
    bn("Chovendo agora", B.rainingNow.length+' <small>de '+B.rainSt.length+complemento+'</small>')+
    bn("Chuva 12 h medida", (B.rain12!=null?fmt(B.rain12,1):'-')+' <small>mm (máx.)</small>')+
    bn("Régua", g?fmt(g.nowLevel,2)+' <small>m ('+g.reg.code+')</small>':'-')+
    '</div>';
}
function cardDeBacia(key){
  const B=APP.BAS[key], meta=B.meta, g=B.gauge;
  /* Rotulo de reserva quando a bacia nao tem regua: continua lido em 24 h porque
     as faixas (15/40/80 mm) foram aferidas nessa duracao e nao ha medida de
     campo para reaferir em 12 h. Produz palavra, nunca numero na tela, entao nao
     entra na comparacao de janelas. */
  const stt = g ? statusRel(g) : chuvaStatus(B.rain24);
  const cls = CLASSE_STATUS[stt.k]||"st-normal";
  const maxMm=Math.max(10,B.rain12||0);
  const barras = B.rainSt.length
    ? B.rainSt.map(s=>barraDeEstacao(s,maxMm)).join("")
    : '<p class="basin-empty">Nenhuma estação de chuva desta bacia com sinal agora.</p>';
  return '<div class="basin"><div class="basin-head"><div class="nm">'+meta.nome+
    '<small>'+meta.sub+'</small></div><span class="basin-verd '+cls+'">'+stt.label+'</span></div>'+
    '<div class="basin-body">'+
    '<div class="basin-pano"><svg id="cut-'+key+'" viewBox="0 0 360 156" role="img" aria-label="Encosta da bacia do '+meta.nome+' com as estações de chuva, onde chove agora e a régua de nível."></svg></div>'+
    '<p class="basin-cap">'+legendaDeChuvaDaBacia(B)+'</p>'+
    numerosDaBacia(B,g)+
    '<div class="sbars">'+barras+'</div>'+
    '</div></div>';
}
export function renderBasins(){
  $("#basins").innerHTML=ORDEM_BACIAS.map(cardDeBacia).join("");
  ORDEM_BACIAS.forEach(k=>drawBasinCut($("#cut-"+k), APP.BAS[k], k));
}

/* Qual estacao representa "o ar da cidade". E uma DECISAO, nao desenho: fica
   numa funcao propria para o renderizador abaixo so montar DOM.
   Preferimos a regua local RSRL-RB02 (dentro da cidade); na falta dela, a
   estacao com dado completo mais proxima dela, nao a mais proxima na lista REG
   (que tinha estacoes de serra, longe da cidade).

   SO ESTACAO DE BAIXADA (klass "local") entra aqui. Temperatura cai perto de
   6 C por km de altitude, entao estacao de serra mede outro clima, nao o da
   cidade: a EXRL-MG01 (Morro Grande, 830 m) apareceu no card marcando 12,3 C
   como se fosse o ar de Rolante (44 m). Ela entrou porque a RB02 saiu da lista
   de estacoes completas quando o sensor de umidade dela foi excluido, e a serra
   virou "a mais proxima com dado completo" — proximidade no mapa nao compensa
   800 m de diferenca de altura. Sem estacao de baixada com sinal, o card diz
   que nao tem, em vez de publicar a temperatura do morro. */
const REF_CIDADE="5BA69743261D364A"; // RSRL-RB02, ponte no centro, 44 m
function escolheReferenciaDoAr(){
  const cand=REG.map(r=>APP.ST[r.code])
                .filter(s=>s&&s.live&&s.now.temp!=null&&s.reg.klass==="local");
  const completas=cand.filter(s=>s.now.hum!=null&&s.now.press!=null);
  const alvo=REGBY[REF_CIDADE];
  const dist2=s=>{ const dlat=s.reg.lat-alvo.lat, dlng=s.reg.lng-alvo.lng; return dlat*dlat+dlng*dlng; };
  const maisPerto=arr=>arr.length?arr.slice().sort((a,c)=>dist2(a)-dist2(c))[0]:null;
  const naCidade=arr=>arr.find(s=>s.reg.code===alvo.code);
  const ref = naCidade(completas)||maisPerto(completas)||naCidade(cand)||maisPerto(cand)||null;
  /* Umidade tem fonte propria: so 2 das 5 estacoes atmosfericas medem pressao,
     entao exigir hum+press juntos quase sempre sobra so a RB02 — e foi o sensor
     dela de umidade que quebrou (media de 16 % com chuva forte caindo). Sem isso
     o card ficava com "-" em vez de buscar a proxima estacao com umidade valida. */
  const comUmidade=cand.filter(s=>s.now.hum!=null);
  const refHum = naCidade(comUmidade)||maisPerto(comUmidade)||ref;
  return {ref, refHum};
}
function miniGrafico(arr, cor){
  const W=110, Ht=34, pad=3, limpo=arr.filter(v=>v!=null);
  if(!limpo.length) return "";
  const ff=fillGaps(arr);
  const xs=escala(ff.length,W,pad);
  const ys=escalaValor(Math.min.apply(null,limpo), Math.max.apply(null,limpo), Ht, pad);
  return '<svg viewBox="0 0 '+W+' '+Ht+'" preserveAspectRatio="none" aria-hidden="true"><path class="chart-line" d="'+
         smoothPath(ff.map((v,i)=>[xs(i),ys(v)]))+'" fill="none" stroke="'+cor+
         '" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>';
}
export function renderContext(){
  const {ref, refHum}=escolheReferenciaDoAr();
  const grid=$("#ctx-grid");
  if(!ref){
    $("#ctx-h").textContent="Ar (sem estação na baixada com sinal)";
    grid.innerHTML='<p class="ctx-vazio">Nenhuma estação da baixada com sinal agora. As estações de serra ficam entre 430 m e 880 m de altitude e medem outro clima.</p>';
    return;
  }
  const outraFonte = refHum && refHum.reg.code!==ref.reg.code;
  $("#ctx-h").textContent="Ar em "+ref.reg.code+(outraFonte?" (umidade: "+refHum.reg.code+")":"");
  const off=HMAX-CH;
  const items=[
    {k:"Temperatura", u:"C",   arr:ref.tempH.slice(off),                 now:ref.now.temp,              cor:"var(--terra)",   d:1},
    {k:"Umidade",     u:"%",   arr:refHum?refHum.humH.slice(off):[],     now:refHum?refHum.now.hum:null, cor:"var(--teal)",    d:0},
    {k:"Pressão",     u:"hPa", arr:ref.pressH.slice(off),                now:ref.now.press,             cor:"var(--granite)", d:0}
  ];
  grid.innerHTML=items.map(it=>
    '<div class="ctx"><div class="k">'+it.k+'</div><div class="v">'+fmt(it.now,it.d)+
    ' <small>'+it.u+'</small></div>'+miniGrafico(it.arr,it.cor)+'</div>').join("");
  grid.querySelectorAll("svg").forEach(prepLines);
}
