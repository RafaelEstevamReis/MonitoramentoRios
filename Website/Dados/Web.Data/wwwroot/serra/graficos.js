/* ========================================================================
   GRAFICOS
   Le estado, escreve SVG. Nao decide regra de negocio.
   ======================================================================== */

import { APP } from "./estado.js";
import { HMAX, CH } from "./config.js";
import { $, fmt, clamp, smoothPath, diaCurto, sunTimesFor, horaDe } from "./util.js";
import { codeTail, kmTxt, VENTO_LIMIAR } from "./config.js";
import { rainPulse, rainNowOf } from "./dados.js";
import { pulseTxt, rainWord } from "./rotulos.js";
import { tipShow, tipHide } from "./tooltip.js";
import { svgText } from "./svg.js";

/* ===== chuva prevista por hora =================================================
   Sete blocos de desenho, cada um numa funcao: sem isso a leitura de um deles
   exigia rolar por todos. Cada um recebe a caixa (C) com as medidas do grafico
   e devolve marcacao; nenhum le estado global. */
const CAIXA_CHUVA={Wv:360,Hv:172,L:30,R:12,T:24,B:26};

/* O gráfico começa pelo fundo da noite; as faixas amarelas mostram o dia
   real entre nascer e pôr do sol em Rolante. Sol e lua ficam no começo de
   cada período visível, para situar o horário sem competir com a chuva. */
function iconeSol(x,y){
  return '<g fill="none" stroke="var(--day-deep)" stroke-width="1.2" stroke-linecap="round"><circle cx="'+x+'" cy="'+y+'" r="2.2"/><path d="M '+x+' '+(y-5)+' v1.4 M '+x+' '+(y+3.6)+' v1.4 M '+(x-5)+' '+y+' h1.4 M '+(x+3.6)+' '+y+' h1.4 M '+(x-3.5)+' '+(y-3.5)+' l1 1 M '+(x+2.5)+' '+(y+2.5)+' l1 1 M '+(x-3.5)+' '+(y+3.5)+' l1 -1 M '+(x+2.5)+' '+(y-2.5)+' l1 -1"/></g>';
}
function iconeLua(x,y){
  return '<path d="M '+(x+2.8)+' '+(y-5)+' A 5 5 0 1 0 '+(x+2.8)+' '+(y+5)+' A 3.6 3.6 0 0 1 '+(x+2.8)+' '+(y-5)+'" fill="none" stroke="var(--night-deep)" stroke-width="1.2" stroke-linecap="round"/>';
}
/* Vento forte: mesmo tracado do icone "wind" (Feather), reescalado. Centraliza
   em (x,y) porque o path original tem o desenho deslocado do (0,0) do viewBox. */
function iconeVento(x,y){
  const s=0.34;
  return '<g transform="translate('+x.toFixed(1)+','+y.toFixed(1)+') scale('+s+') translate(-11,-12)" fill="none" stroke="var(--alert)" stroke-width="3.8" stroke-linecap="round" stroke-linejoin="round"><path d="M9.59 4.59A2 2 0 1 1 11 8H2m10.59 11.41A2 2 0 1 0 14 16H2m15.73-8.27A2.5 2.5 0 1 1 19.5 12H2"/></g>';
}
function faixasDeDia(C, tt, n, timeToX){
  const y=C.T, h=C.Hv-C.B-C.T, limiteE=C.L+5, limiteD=C.Wv-C.R-5;
  const out=['<rect x="'+C.L+'" y="'+y+'" width="'+(C.Wv-C.L-C.R)+'" height="'+h+'" fill="var(--night)" opacity="0.22"/>'];
  const ini=new Date(tt[0]), fim=new Date(tt[n-1]);
  for(let dt=new Date(ini.getFullYear(),ini.getMonth(),ini.getDate()); dt<=fim; dt.setDate(dt.getDate()+1)){
    const sol=sunTimesFor(dt), x0=timeToX(sol.sunrise.getTime()), x1=timeToX(sol.sunset.getTime());
    if(x1<=x0) continue;
    out.push('<rect x="'+x0.toFixed(1)+'" y="'+y+'" width="'+(x1-x0).toFixed(1)+'" height="'+h+'" fill="var(--day)" opacity="0.24"/>');
    if(x0>=limiteE&&x0<=limiteD) out.push(iconeSol(x0+6,y+8));
    if(x1>=limiteE&&x1<=limiteD) out.push(iconeLua(x1+6,y+8));
  }
  return out;
}
function gradeHorizontal(C, yMax, ys){
  const out=[];
  for(let g=0;g<=2;g++){
    const val=yMax*g/2, yy=ys(val);
    out.push('<line x1="'+C.L+'" y1="'+yy.toFixed(1)+'" x2="'+(C.Wv-C.R)+'" y2="'+yy.toFixed(1)+'" stroke="var(--line)"/>');
    out.push(svgText(fmt(val,0),{x:C.L-5, y:(yy+3).toFixed(1), ancora:"fim", tam:8.5}));
  }
  return out;
}
function barrasDeChuva(C, pr, tt, n, xs, ys, bw){
  const out=[], agora=Date.now();
  for(let i=0;i<n;i++){
    if(pr[i]<=0) continue;
    const x=xs(i)-bw/2, y=ys(pr[i]), h=(C.Hv-C.B)-y;
    const cor=pr[i]>=8?"var(--alert)":pr[i]>=3?"var(--terra-deep)":"var(--teal)";
    const passou=new Date(tt[i]).getTime()<agora-3600000;
    out.push('<rect x="'+x.toFixed(1)+'" y="'+y.toFixed(1)+'" width="'+bw.toFixed(1)+'" height="'+Math.max(0.6,h).toFixed(1)+'" rx="1" fill="'+cor+'"'+(passou?' opacity="0.38"':'')+'/>');
  }
  return out;
}
/* So desenha o icone na hora que passa do limiar — as demais ficam sem
   marcacao nenhuma, e nao um icone "fraco" ou apagado. */
function marcasDeVento(C, ve, n, xs){
  const y=C.T+12, out=[];
  for(let i=0;i<n;i++) if((ve[i]||0)>=VENTO_LIMIAR) out.push(iconeVento(xs(i),y));
  return out;
}
function eixoDeHoras(C, tt, n, xs){
  const out=[];
  for(let i=0;i<n;i+=12) out.push(svgText(horaDe(new Date(tt[i])),{x:xs(i).toFixed(1), y:C.Hv-9, ancora:"meio", tam:8}));
  return out;
}
/* Rotulo do dia centralizado no trecho, com linha tracejada na virada da
   meia-noite — sem isso o eixo so mostra hora e o leitor perde em que dia
   cada barra cai. */
function rotulosDeDia(C, tt, n, xs){
  const out=[];
  let ini=0;
  for(let i=1;i<=n;i++){
    const virou = i===n || new Date(tt[i]).getDate()!==new Date(tt[i-1]).getDate();
    if(!virou) continue;
    const fim=i-1, cx=(xs(ini)+xs(fim))/2;
    out.push(svgText(diaCurto(tt[ini]),{x:cx.toFixed(1), y:9, ancora:"meio", tam:8.5, peso:700, cor:"var(--ink-2)"}));
    if(i<n){
      const dx=(xs(fim)+xs(i))/2;
      out.push('<line x1="'+dx.toFixed(1)+'" y1="'+C.T+'" x2="'+dx.toFixed(1)+'" y2="'+(C.Hv-C.B)+'" stroke="var(--line-2)" stroke-dasharray="2,2"/>');
    }
    ini=i;
  }
  return out;
}
/* A serie leva 3 h de cauda passada (barras esmaecidas); a linha do "agora"
   cai na fracao exata da hora corrente e anda durante ela. */
function marcaAgora(C, tt, n, xs){
  const x=xs(clamp((Date.now()-new Date(tt[0]).getTime())/3600000,0,n-1));
  const naBorda=x>C.Wv-56;
  return [
    '<line class="now-line" x1="'+x.toFixed(1)+'" y1="'+C.T+'" x2="'+x.toFixed(1)+'" y2="'+(C.Hv-C.B)+'"/>',
    svgText("agora",{cls:"now-tag", x:(x+(naBorda?-3:3)).toFixed(1), y:C.T-4, ancora:naBorda?"fim":"inicio", viaCss:true})
  ];
}
function camadaDeHover(C){
  return [
    '<rect class="hover-band" x="0" y="'+C.T+'" width="0" height="'+(C.Hv-C.B-C.T)+'" rx="1" visibility="hidden"/>',
    '<line class="hover-line" x1="0" y1="'+C.T+'" x2="0" y2="'+(C.Hv-C.B)+'" visibility="hidden"/>',
    '<rect class="chart-hit" x="'+C.L+'" y="'+C.T+'" width="'+(C.Wv-C.L-C.R)+'" height="'+(C.Hv-C.B-C.T)+'" fill="transparent"/>'
  ];
}
export function chartRain(){
  const svg=$("#chart-rain"), C=CAIXA_CHUVA;
  if(!APP.FC||!APP.FC.hourly||!APP.FC.hourly.p.length){
    svg.innerHTML=svgText("previsão indisponível",{x:180,y:90,ancora:"meio",tam:11});
    return;
  }
  const pr=APP.FC.hourly.p, tt=APP.FC.hourly.t, ve=APP.FC.hourly.v||[], n=pr.length;
  const yMax=Math.max(2,Math.ceil(Math.max.apply(null,pr)*1.15));
  const xs=i=>C.L+i*(C.Wv-C.L-C.R)/(n-1);
  const ys=v=>(C.Hv-C.B)-v/yMax*(C.Hv-C.B-C.T);
  const bw=Math.max(1.4,(C.Wv-C.L-C.R)/n*0.66);
  const t0=new Date(tt[0]).getTime();
  const timeToX=ms=>xs(clamp((ms-t0)/3600000,0,n-1));
  svg.innerHTML=[].concat(
    faixasDeDia(C,tt,n,timeToX),
    gradeHorizontal(C,yMax,ys),
    barrasDeChuva(C,pr,tt,n,xs,ys,bw),
    eixoDeHoras(C,tt,n,xs),
    rotulosDeDia(C,tt,n,xs),
    marcaAgora(C,tt,n,xs),
    marcasDeVento(C,ve,n,xs),
    camadaDeHover(C)
  ).join("");
  hookRainHover(svg,{Wv:C.Wv,L:C.L,R:C.R,n:n,bw:bw,xs:xs,pr:pr,tt:tt,ve:ve});
}

function hookRainHover(svg,C){
  const band=svg.querySelector(".hover-band"), line=svg.querySelector(".hover-line"), hit=svg.querySelector(".chart-hit");
  if(!hit) return;
  function at(ev){
    const r=svg.getBoundingClientRect(); if(!r.width) return;
    const vx=(ev.clientX-r.left)*C.Wv/r.width;
    const i=Math.round(clamp((vx-C.L)/((C.Wv-C.L-C.R)/(C.n-1)),0,C.n-1));
    const x=C.xs(i), mm=C.pr[i]||0, vento=(C.ve&&C.ve[i])||0, d=new Date(C.tt[i]);
    band.setAttribute("x",(x-C.bw/2-0.8).toFixed(1)); band.setAttribute("width",(C.bw+1.6).toFixed(1));
    line.setAttribute("x1",x.toFixed(1)); line.setAttribute("x2",x.toFixed(1));
    band.setAttribute("visibility","visible"); line.setAttribute("visibility","visible");
    const ventoTx=vento>=VENTO_LIMIAR?' · vento forte ~'+fmt(vento,0)+' km/h':'';
    tipShow('<b>'+diaCurto(C.tt[i])+' · '+String(d.getHours()).padStart(2,"0")+'h</b><br><span class="kv">'+fmt(mm,1)+' mm/h · '+rainWord(mm)+ventoTx+'</span>',ev);
  }
  function off(){ band.setAttribute("visibility","hidden"); line.setAttribute("visibility","hidden"); tipHide(); }
  hit.addEventListener("mousemove",at);
  hit.addEventListener("mouseleave",off);
  hit.addEventListener("touchstart",function(e){ if(e.touches[0]) at(e.touches[0]); },{passive:true});
  hit.addEventListener("touchmove",function(e){ if(e.touches[0]) at(e.touches[0]); },{passive:true});
  hit.addEventListener("touchend",off);
  hit.addEventListener("touchcancel",off);
}

/* ===== chuva medida: mapa de calor estacao x hora + faixa de abrangencia =====
   O grafico anterior desenhava so o maximo da rede por hora, o que apagava
   justamente a informacao procurada: uma estacao sob temporal ficava
   identica a rede inteira chovendo. Com uma linha por estacao, os tres
   casos viram formas distintas: coluna cheia = chuva geral, linha unica =
   chuva isolada, mancha diagonal = frente descendo a serra. */
const RAIN_WET=0.2;   // abaixo disso e traco/garoa; nao conta como abrangencia
const HM=[[0.05,"--hm-0"],[0.5,"--hm-1"],[2,"--hm-2"],[5,"--hm-3"],[10,"--hm-4"]];
function hmCol(v){ for(let i=0;i<HM.length;i++) if(v<HM[i][0]) return "var("+HM[i][1]+")"; return "var(--hm-5)"; }
function rainRowsOrdered(){
  const rank={areia:0,rolante:1};
  return APP.NET.rainSt.filter(s=>Array.isArray(s.rain)).slice()
    .sort((a,b)=>(rank[a.reg.bacia]-rank[b.reg.bacia])||(b.reg.alt-a.reg.alt));
}
function coverage(sts,off){
  const out=[];
  for(let i=0;i<CH;i++){ let n=0,t=0,mx=0;
    sts.forEach(s=>{ const v=s.rain[off+i]; if(v!=null){ t++; if(v>=RAIN_WET) n++; if(v>mx) mx=v; } });
    out.push({n:n,t:t,mx:mx}); }
  return out;
}
/* Medidas do mapa de calor, num lugar so. */
const CAIXA_MAPA={Wv:360,padL:70,padR:8,y0:3,sh:26,gapS:13,rowH:15,gap:2,grpGap:11,padB:18};
const NOME_BACIA={areia:"BACIA DO AREIA", rolante:"BACIA DO ROLANTE"};

/* Faixa de abrangencia: altura = fracao das estacoes com chuva naquela hora. */
function faixaAbrangencia(M, cov, off, qtd, cw){
  const out=[
    svgText("abrangência",{x:M.padL-6, y:M.y0+11, ancora:"fim", tam:8, peso:700}),
    svgText("de "+qtd+" estações",{x:M.padL-6, y:M.y0+21, ancora:"fim", tam:7.4, cor:"var(--faint)"}),
    '<rect x="'+M.padL+'" y="'+M.y0+'" width="'+(M.Wv-M.padL-M.padR).toFixed(1)+'" height="'+M.sh+'" rx="3" fill="var(--hm-nd)" opacity="0.55"/>'
  ];
  cov.forEach((c,i)=>{
    if(!c.t||!c.n) return;
    const f=c.n/c.t, h=Math.max(1.6,M.sh*f);
    const cor = f>=0.8?"var(--alert)" : f>=0.5?"var(--terra-deep)" : "var(--teal)";
    out.push('<rect x="'+(M.padL+i*cw+0.4).toFixed(1)+'" y="'+(M.y0+M.sh-h).toFixed(1)+'" width="'+(cw-0.8).toFixed(1)+'" height="'+h.toFixed(1)+'" rx="1" fill="'+cor+'"><title>'+APP.labels[off+i]+': '+c.n+' de '+c.t+' estações com chuva</title></rect>');
  });
  out.push('<line x1="'+M.padL+'" y1="'+(M.y0+M.sh)+'" x2="'+(M.Wv-M.padR)+'" y2="'+(M.y0+M.sh)+'" stroke="var(--line-2)"/>');
  return out;
}
/* Uma linha por estacao, da cabeceira para a cidade dentro de cada bacia.
   Devolve tambem o y final, que o eixo de baixo precisa. */
function linhasPorEstacao(M, sts, off, cw){
  const out=[]; let bacia=null, y=M.padT;
  sts.forEach(s=>{
    if(s.reg.bacia!==bacia){
      if(bacia!==null) y+=M.grpGap;
      bacia=s.reg.bacia;
      out.push(svgText(NOME_BACIA[bacia],{x:0, y:(y-3.5).toFixed(1), tam:6.8, peso:700, espaco:"0.05em", cor:"var(--faint)"}));
    }
    // rotulo curto (MG01) + distancia da cidade: o prefixo da rede nao diz nada ao leitor
    out.push(svgText(codeTail(s.reg.code),{x:M.padL-6, y:(y+M.rowH*0.55).toFixed(1), ancora:"fim", tam:8, peso:700, cor:"var(--ink-2)"}));
    out.push(svgText(kmTxt(s.reg),{x:M.padL-6, y:(y+M.rowH*0.55+6.4).toFixed(1), ancora:"fim", tam:6.3, peso:600, cor:"var(--faint)"}));
    for(let i=0;i<CH;i++){
      const v=s.rain[off+i];
      out.push('<rect x="'+(M.padL+i*cw+0.35).toFixed(1)+'" y="'+y.toFixed(1)+'" width="'+(cw-0.7).toFixed(1)+'" height="'+M.rowH+'" rx="1.5" fill="'+(v==null?"var(--hm-nd)":hmCol(v))+'"><title>'+s.reg.code+' ('+s.reg.rio+') · '+APP.labels[off+i]+': '+(v==null?"sem dado":fmt(v,1)+" mm/h")+'</title></rect>');
    }
    y+=M.rowH+M.gap;
  });
  return {marcacao:out, yFim:y-M.gap};
}
function eixoDoMapa(M, off, cw, yb){
  const out=[0,9,18,27].map(i=>svgText(APP.labels[off+i],{x:(M.padL+i*cw+cw/2).toFixed(1), y:yb+11, ancora:"meio", tam:8.5}));
  out.push(svgText("agora",{x:M.Wv-M.padR, y:yb+11, ancora:"fim", tam:8.5, peso:600, cor:"var(--ink-2)"}));
  return out;
}
function legendaDoMapa(){
  const faixas=[["sw-0","sem chuva"],["sw-2","fraca"],["sw-3","moderada"],["sw-4","forte"],["sw-5","muito forte"]];
  return faixas.map(([cls,txt])=>'<span class="cl"><span class="sw '+cls+'"></span>'+txt+'</span>').join("")+
    '<span class="cl nota">cada linha é uma estação, da cabeceira para a cidade · cinza = sem dado</span>'+
    '<span class="cl nota">faixa de cima: altura = quantas estações estavam com chuva naquela hora</span>';
}
export function chartRainMap(){
  const svg=$("#chart-rainmap"), leg=$("#rainmap-legend"), read=$("#rainmap-read"); if(!svg) return;
  const off=HMAX-CH, sts=rainRowsOrdered();
  if(!sts.length){
    svg.setAttribute("viewBox","0 0 360 80");
    svg.innerHTML=svgText("nenhuma estação de chuva com sinal",{x:180,y:44,ancora:"meio",tam:11});
    if(leg) leg.innerHTML="";
    if(read) read.textContent="Sem estação de chuva reportando agora.";
    return;
  }
  const M=Object.assign({}, CAIXA_MAPA);
  M.padT=M.y0+M.sh+M.gapS;
  const cw=(M.Wv-M.padL-M.padR)/CH;
  /* cada troca de bacia abre um respiro para caber o rotulo do grupo */
  let grupos=0, vista=null;
  sts.forEach(s=>{ if(s.reg.bacia!==vista){ vista=s.reg.bacia; grupos++; } });
  const Hv=M.padT+sts.length*(M.rowH+M.gap)+(grupos-1)*M.grpGap+M.padB;
  const cov=coverage(sts,off);
  const linhas=linhasPorEstacao(M,sts,off,cw);
  svg.setAttribute("viewBox","0 0 "+M.Wv+" "+Hv.toFixed(0));
  svg.innerHTML=[].concat(
    faixaAbrangencia(M,cov,off,sts.length,cw),
    linhas.marcacao,
    eixoDoMapa(M,off,cw,linhas.yFim)
  ).join("");
  if(read) read.innerHTML=rainSpreadTxt(cov,sts.length);
  if(leg) leg.innerHTML=legendaDoMapa();
}
/* Le a faixa de abrangencia em palavras: e a resposta para "chove em toda a
   regiao ou so num ponto?", que o grafico de maximo nao conseguia dar. */
function rainSpreadTxt(cov,tot){
  const last=cov[CH-1];
  let peak={n:0,i:0,mx:0};
  cov.forEach((c,i)=>{ if(c.n>peak.n||(c.n===peak.n&&c.mx>peak.mx)) peak={n:c.n,i:i,mx:c.mx}; });
  if(!peak.n) return "Nenhuma estação passou de "+fmt(RAIN_WET,1)+" mm/h nas últimas 36 h — no máximo uma garoa fina.";
  const frac=peak.n/tot;
  const tipo=frac>=0.8?"<b>chuva geral</b> na região":frac>=0.4?"<b>chuva espalhada</b>, não em toda a rede":"<b>chuva isolada</b>, em poucos pontos";
  const forca=peak.mx>=10?"forte":peak.mx>=5?"moderada":peak.mx>=2?"fraca":"muito fraca";
  const agora=last.n?("Agora: "+last.n+" de "+tot+" com chuva."):"Agora nenhuma estação com chuva.";
  return tipo+", intensidade "+forca+" — no pico, "+peak.n+" de "+tot+" estações chovendo ao mesmo tempo, às "+APP.labels[HMAX-CH+peak.i]+" (máx. "+fmt(peak.mx,1)+" mm/h). "+agora;
}

/* ===== corte da bacia: encosta, chuva localizada, regua da cidade ==============
   Eram 85 linhas numa funcao so, desenhando cinco coisas diferentes. Agora cada
   camada tem nome e recebe o que precisa. */
const CAIXA_CORTE={Wv:360,Hv:156,yHigh:42,yLow:104,botGround:150,xL:24,xR:334,
                   gx:322,gw:13,gTop:112,gBed:146};
const CONTORNO_CLARO={contorno:true, peso:700};

function gradientesDoCorte(tg,wg){
  return '<defs><linearGradient id="'+tg+'" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="oklch(0.50 0.06 150)"/><stop offset="0.55" stop-color="oklch(0.40 0.055 156)"/><stop offset="1" stop-color="oklch(0.30 0.05 160)"/></linearGradient><linearGradient id="'+wg+'" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="oklch(0.66 0.10 205)"/><stop offset="1" stop-color="oklch(0.47 0.082 212)"/></linearGradient></defs>';
}
function encostaERio(K, pts, n, riverPts, tg){
  const cume=[[0,pts[0].y-3]].concat(pts.map(pp=>[pp.x,pp.y])).concat([[K.Wv,pts[n-1].y+5]]);
  const cumeD=smoothPath(cume), rioD=smoothPath(riverPts);
  return [
    '<path d="'+cumeD+' L '+K.Wv+' '+K.botGround+' L 0 '+K.botGround+' Z" fill="url(#'+tg+')"/>',
    '<path d="'+cumeD+'" fill="none" stroke="oklch(0.60 0.09 135 / 0.7)" stroke-width="1.4"/>',
    '<path d="'+rioD+'" fill="none" stroke="oklch(0.47 0.082 212 / 0.3)" stroke-width="5" stroke-linecap="round"/>',
    '<path class="pano-river" d="'+rioD+'" fill="none" stroke="var(--teal)" stroke-width="2.2" stroke-linecap="round"/>'
  ];
}
/* Pingos com posicao, duracao e atraso sorteados: chuva que pulsa em bloco
   parece animacao, nao chuva. */
function pingos(cx, half, top, bot, quantos, dim){
  const out=[];
  for(let k=0;k<quantos;k++){
    const dx=(cx-half+Math.random()*half*2).toFixed(1), t=top+Math.random()*7, queda=bot-t,
          dur=(0.5+Math.random()*0.35).toFixed(2), atraso=(-Math.random()*dur).toFixed(2);
    out.push('<line class="pano-drop" x1="'+dx+'" y1="'+t.toFixed(1)+'" x2="'+dx+'" y2="'+(t+3.4).toFixed(1)+'" opacity="'+(0.72*dim).toFixed(2)+'" style="--fall:'+queda.toFixed(1)+'px; --dur:'+dur+'s; --delay:'+atraso+'s"/>');
  }
  return out;
}
/* Onde esta chovendo: efeito cheio agora, atenuado se foi ha pouco. */
function chuvaLocalizada(pts, n, riverPts){
  const out=[];
  pts.map((pp,i)=>({pp,i,q:rainPulse(pp.s)})).filter(o=>o.q).forEach(o=>{
    const cx=o.pp.x, cy=o.pp.y, q=o.q, mm=q.mm, agora=q.now;
    const inten=clamp(mm/8,0,1), dim=agora?1:0.45;
    // trecho do rio sob a chuva, realcado
    const a=riverPts[Math.max(0,o.i-1)], c=riverPts[o.i], e=riverPts[Math.min(n-1,o.i+1)];
    const m1=[(a[0]+c[0])/2,(a[1]+c[1])/2], m2=[(c[0]+e[0])/2,(c[1]+e[1])/2];
    out.push('<path'+(agora?' class="pano-wetseg"':'')+' d="'+smoothPath([m1,c,m2])+'" fill="none" stroke="var(--teal-soft)" stroke-width="'+((agora?3.2:2.4)+inten*1.6).toFixed(1)+'" stroke-linecap="round" opacity="'+((0.5+inten*0.35)*dim).toFixed(2)+'"'+(agora?'':' stroke-dasharray="4 3"')+'/>');
    // nuvem + pingos so na faixa daquela estacao
    const half=(agora?13:10)+inten*9, top=Math.max(6,cy-30-inten*8), bot=cy+4;
    out.push('<ellipse'+(agora?' class="pano-cloud"':'')+' cx="'+cx.toFixed(1)+'" cy="'+(top-3).toFixed(1)+'" rx="'+(half*0.95).toFixed(1)+'" ry="'+((agora?3.6:2.6)+inten*2.6).toFixed(1)+'" fill="oklch(0.52 0.035 240)" opacity="'+((0.42+inten*0.35)*dim).toFixed(2)+'"/>');
    out.push.apply(out, pingos(cx, half, top, bot, agora?Math.round(clamp(5+mm*5,5,16)):3, dim));
    // pulso no ponto (so quando esta chovendo agora) + leitura
    if(agora){
      out.push('<circle class="pano-halo" cx="'+cx.toFixed(1)+'" cy="'+cy.toFixed(1)+'" r="4.2" fill="none" stroke="var(--teal-soft)" stroke-width="1.5"/>');
      out.push('<circle class="pano-halo d2" cx="'+cx.toFixed(1)+'" cy="'+cy.toFixed(1)+'" r="4.2" fill="none" stroke="var(--teal-soft)" stroke-width="1.1"/>');
    }
    out.push(svgText(pulseTxt(q), Object.assign({}, CONTORNO_CLARO,
      {cls:agora?"pano-mm":null, x:cx.toFixed(1), y:(cy+13).toFixed(1), ancora:"meio", tam:6.4,
       cor:agora?"oklch(0.96 0.06 205)":"oklch(0.86 0.03 205)"})));
  });
  return out;
}
function reguaDaCidade(K, B, n, riverPts, wg){
  const g=B.gauge||B.levelSt[0]||null, emConferencia=!!(g&&g.levelCheck);
  let frac=0;
  if(g&&g.nowLevel!=null){
    frac = g.reg.cotaAlerta
      ? clamp(g.nowLevel/g.reg.cotaAlerta,0,1)
      : clamp((g.nowLevel-(g.lvMin||0))/(((g.lvMax||g.nowLevel)-(g.lvMin||0))||1),0.08,1);
  }
  const wH=(K.gBed-K.gTop)*frac, ponta=riverPts[n-1], meio=K.gx+K.gw/2;
  const out=[
    '<rect x="'+(K.gx-3)+'" y="'+K.gTop+'" width="3" height="'+(K.gBed-K.gTop+2)+'" rx="1.5" fill="oklch(0.56 0.014 235 / 0.8)"/>',
    '<rect x="'+(K.gx+K.gw)+'" y="'+K.gTop+'" width="3" height="'+(K.gBed-K.gTop+2)+'" rx="1.5" fill="oklch(0.56 0.014 235 / 0.8)"/>',
    '<rect x="'+(K.gx-3)+'" y="'+K.gBed+'" width="'+(K.gw+6)+'" height="3" rx="1.5" fill="oklch(0.44 0.02 235 / 0.85)"/>'
  ];
  if(g&&g.nowLevel!=null){
    out.push('<path class="pano-river" d="M '+ponta[0].toFixed(1)+' '+ponta[1].toFixed(1)+' C '+(ponta[0]+2)+' '+(ponta[1]+6)+', '+meio+' '+(K.gTop-10)+', '+meio+' '+K.gTop+'" fill="none" stroke="var(--teal)" stroke-width="2.2" stroke-linecap="round"/>');
    out.push('<rect class="pano-water" x="'+K.gx+'" y="'+(K.gBed-wH).toFixed(1)+'" width="'+K.gw+'" height="'+wH.toFixed(1)+'" fill="url(#'+wg+')"/>');
    if(g.reg.cotaAlerta){
      out.push('<line x1="'+(K.gx-5)+'" y1="'+K.gTop+'" x2="'+(K.gx+K.gw+5)+'" y2="'+K.gTop+'" stroke="var(--alert)" stroke-width="1.2" stroke-dasharray="3 3"/>');
      out.push(svgText("cota",{x:K.gx+K.gw+7, y:K.gTop+3, tam:7.5, peso:700, cor:"var(--alert)"}));
    }
    out.push(svgText(g.reg.code.replace("RSRL-",""),{x:meio, y:K.gTop-5, ancora:"meio", tam:7, peso:700,
      cor:emConferencia?"oklch(0.86 0.10 82)":"var(--teal-soft)"}));
    if(emConferencia) out.push(svgText("em conferência",{x:meio, y:K.gBed+9, ancora:"meio", tam:5.6, peso:700, cor:"oklch(0.86 0.10 82)"}));
  } else {
    out.push(svgText("sem régua",{x:meio, y:((K.gTop+K.gBed)/2).toFixed(0), ancora:"meio", tam:6.5, cor:"oklch(0.92 0.02 150)"}));
  }
  out.push(svgText("SERRA",{x:26, y:K.botGround-4, tam:8.5, peso:600, cor:"oklch(0.96 0.02 150)",
    fonte:"var(--font-display)", espaco:"0.4"}));
  out.push(svgText("cidade",{x:meio, y:K.botGround-4, ancora:"meio", tam:8, peso:700, cor:"oklch(0.96 0.02 150)"}));
  return out;
}
/* Alvo de toque invisivel + abertura do grupo: os quatro tipos de ponto
   repetiam isso. Quem chama fecha com o <title> e </g>. */
function pontoBase(x, y, raio, rotulo, cls){
  return '<g class="pano-pt'+(cls?" "+cls:"")+'" tabindex="0" role="img" aria-label="'+rotulo+'">'+
         '<circle cx="'+x+'" cy="'+y+'" r="'+raio+'" fill="oklch(1 0 0 / 0)"/>';
}
function pontoSemSinal(s, x, y){
  return pontoBase(x,y,7,s.reg.code+" sem sinal")+
    '<circle cx="'+x+'" cy="'+y+'" r="2.4" fill="none" stroke="oklch(0.80 0.02 235 / 0.55)" stroke-width="1" stroke-dasharray="1.5 1.5"/>'+
    '<title>'+s.reg.code+': sem sinal nas últimas 4 h</title></g>';
}
/* Regua de nivel = losango. Tracejado e vazado quando esta em conferencia. */
function pontoDeRegua(s, pp, x, y, rotulo){
  const cor=s.levelCheck?"oklch(0.72 0.135 78)":"var(--teal-soft)";
  const leitura=s.nowLevel!=null?fmt(s.nowLevel,2)+' m':'sem leitura';
  return pontoBase(x,y,8,s.reg.code+" régua de nível")+
    '<path d="M '+x+' '+(pp.y-4).toFixed(1)+' L '+(pp.x+4).toFixed(1)+' '+y+' L '+x+' '+(pp.y+4).toFixed(1)+' L '+(pp.x-4).toFixed(1)+' '+y+' Z" fill="'+(s.levelCheck?"none":cor)+'" stroke="'+cor+'" stroke-width="1.5"'+(s.levelCheck?' stroke-dasharray="2 1.5"':'')+'/>'+
    '<title>'+s.reg.code+' ('+s.reg.rio+'): nível '+leitura+(s.levelCheck?' — em conferência':'')+'</title></g>'+
    rotulo(8, s.levelCheck?"oklch(0.90 0.12 82)":"oklch(0.98 0.03 200)");
}
/* Estacao de chuva = circulo; o raio cresce com o acumulado de 12 h, a mesma
   janela dos cards e das barras da bacia. */
function pontoDeChuva(s, x, y, maxMm, rotulo){
  const mm12=s.rain12||0, mmAgora=rainNowOf(s), chovendo=mmAgora>0.05;
  const raio=(chovendo?3.0:2.2)+clamp(mm12/maxMm,0,1)*3.4;
  const detalhe=(chovendo?('chovendo agora, '+fmt(mmAgora,1)+' mm/h · '):'')+fmt(mm12,1)+' mm em 12 h';
  return pontoBase(x,y,8,s.reg.code+(chovendo?" chovendo agora":" estação de chuva"), chovendo?"pano-wet":null)+
    '<circle cx="'+x+'" cy="'+y+'" r="'+raio.toFixed(1)+'" fill="'+(chovendo?"oklch(0.80 0.13 205)":"var(--teal)")+'" fill-opacity="'+(chovendo?"1":"0.85")+'" stroke="oklch(0.98 0.02 200)" stroke-width="'+(chovendo?"1.6":"1.2")+'"/>'+
    '<title>'+s.reg.code+' ('+s.reg.rio+'): '+detalhe+'</title></g>'+
    rotulo(7, chovendo?"oklch(0.99 0.06 205)":"oklch(0.98 0.03 200)");
}
function pontoSoTelemetria(s, x, y){
  return pontoBase(x,y,7,s.reg.code+" telemetria")+
    '<circle cx="'+x+'" cy="'+y+'" r="2.2" fill="oklch(0.92 0.02 150 / 0.55)"/>'+
    '<title>'+s.reg.code+': envia só temperatura/pressão</title></g>';
}
/* Codigo curto (MG01) repetido em duas bacias ganha seta de serra para nao
   ficarem dois rotulos iguais no mesmo corte. */
function rotuladorCurto(sts){
  const vistos={};
  sts.forEach(s=>{ const k=codeTail(s.reg.code); vistos[k]=(vistos[k]||0)+1; });
  return s=>{
    const cauda=codeTail(s.reg.code);
    return vistos[cauda]>1 ? (s.reg.klass==="serra"?"↑":"")+cauda : cauda;
  };
}
function pontosDeEstacao(B, pts, sts){
  const maxMm=Math.max(10,B.rain12||0);
  const curtoDe=rotuladorCurto(sts);
  return pts.map(pp=>{
    const s=pp.s, x=pp.x.toFixed(1), y=pp.y.toFixed(1), curto=curtoDe(s);
    const rotulo=(dy,cor)=>svgText(curto, Object.assign({}, CONTORNO_CLARO,
      {x:x, y:(pp.y-dy).toFixed(1), ancora:"meio", tam:7, cor:cor}));
    if(!s.live)     return pontoSemSinal(s,x,y);
    if(s.hasLevel)  return pontoDeRegua(s,pp,x,y,rotulo);
    if(s.hasRain)   return pontoDeChuva(s,x,y,maxMm,rotulo);
    return pontoSoTelemetria(s,x,y);
  });
}
export function drawBasinCut(svg,B,key){
  if(!svg) return;
  const K=CAIXA_CORTE;
  const sts=B.all.slice().sort((a,b)=>b.reg.alt-a.reg.alt), n=sts.length;
  if(!n){ svg.innerHTML=""; return; }
  const altMax=Math.max.apply(null,sts.map(s=>s.reg.alt)), altMin=Math.min.apply(null,sts.map(s=>s.reg.alt));
  const altParaY=a=>K.yHigh+(altMax-a)/((altMax-altMin)||1)*(K.yLow-K.yHigh);
  const xEm=i=>K.xL+i*(K.xR-K.xL)/((n-1)||1);
  const pts=sts.map((s,i)=>({s:s, x:xEm(i), y:altParaY(s.reg.alt)}));
  const riverPts=pts.map(pp=>[pp.x,pp.y+4]);
  const tg="tg_"+key, wg="wg_"+key;
  svg.innerHTML=[gradientesDoCorte(tg,wg)].concat(
    encostaERio(K,pts,n,riverPts,tg),
    chuvaLocalizada(pts,n,riverPts),
    reguaDaCidade(K,B,n,riverPts,wg),
    pontosDeEstacao(B,pts,sts)
  ).join("");
}

export function buildContours(){ const svg=$("#topo"); if(!svg||svg.dataset.done) return; svg.dataset.done="1"; const W=1200,Hh=800,lines=14,p=[];
  for(let i=0;i<lines;i++){ const base=40+i*(Hh-60)/(lines-1),amp=26+(i%3)*16,ph=i*0.9,pts=[]; for(let x=-60;x<=W+60;x+=60){ pts.push([x,base+amp*Math.sin(x/230+ph)+(amp*0.4)*Math.sin(x/97+ph*1.7)]); } p.push('<path d="'+smoothPath(pts)+'" fill="none" stroke="'+((i%4===0)?"var(--pine)":"var(--granite)")+'" stroke-width="1.1" opacity="'+(0.05+(i%2)*0.018).toFixed(3)+'"/>'); } svg.innerHTML=p.join(""); }
