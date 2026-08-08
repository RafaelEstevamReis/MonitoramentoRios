/* ========================================================================
   REGISTRO DA REDE E LIMIARES AFERIDOS
   Somente dado e leitura de dado. Nao calcula, nao desenha.
   ======================================================================== */

/* Duas bases distintas, porque as duas pontas tem qualidades opostas:
     - estacoes: producao tem a rede inteira e ao vivo; em dev o banco local
       so tem o que o MultiServerSync espelhou, entao aponta pra la.
     - previsao: producao ainda roda a copia antiga (Website/Dados), com
       LIMIT 0,96 cravado no SQL, e devolve 4 dias. A mesma origem entrega
       os 7 dias corretos em dev, e em producao aponta pra si mesma.
   ?api=<host> forca as duas pontas pro mesmo lugar (comparar com o ar);
   ?api= (vazio) forca as duas pra esta origem. */
const apiOverride = new URLSearchParams(location.search).get("api");
// Verifica ambiente de desenvolvimento, porém exclui o ambiente de validação
const isDev = (location.hostname === "localhost" || location.hostname === "127.0.0.1") && location.port != 5291;
export const API    = apiOverride ?? (isDev ? "https://rios.bitcoineaqui.com.br" : "");
export const API_FC = apiOverride ?? "";
export const HMAX=96, CH=36;                 // buckets carregados / mostrados no grafico
/* Vento forte, pedido explicito do dono do painel: acima disso vira simbolo
   no grafico horario e frase na manchete; abaixo fica oculto, sem poluir a
   leitura normal de chuva com um dado que quase nunca importa. */
export const VENTO_LIMIAR=40;

/* Registro: coords reais (map.html), limiares reais (rsrl2.html), bacia confirmada pelo usuario.
   bacia: "areia" | "rolante". cotaAlerta so existe onde ha regua de nivel aferida. */
export const REG=[
  {id:"A954066DFFE75CEB",code:"EXFP-RK01",lat:-29.500,lng:-50.405,klass:"serra",alt:880,rio:"Faxinal (cabeceira)",bacia:"rolante"},
  {id:"D251E57DD415F69E",code:"EXFP-AR01",lat:-29.490,lng:-50.570,klass:"serra",alt:820,rio:"Rio Areia (alto)",bacia:"areia"},
  {id:"9A6EE7B45495BB7F",code:"RSRL-GLLS",lat:-29.590,lng:-50.565,klass:"serra",alt:760,rio:"Rolantinho",bacia:"areia",cotaNormal:0.15,cotaAlerta:2.0},
  {id:"936F7F3769D0B500",code:"EXRL-IN01",lat:-29.550,lng:-50.570,klass:"serra",alt:690,rio:"Rio dos Indios",bacia:"areia"},
  {id:"D01D80E734592AFB",code:"EXRZ-CH01",lat:-29.575,lng:-50.420,klass:"serra",alt:600,rio:"Chuvisca (alto)",bacia:"rolante"},
  {id:"CEF2144E84EF82A0",code:"EXRL-MG01",lat:-29.590,lng:-50.530,klass:"serra",alt:830,rio:"Mascarada",bacia:"rolante"},
  {id:"91661F2504450922",code:"EXRL-BV01",lat:-29.640,lng:-50.460,klass:"serra",alt:470,rio:"Boa Vista",bacia:"rolante"},
  {id:"726A95A4D3247CB4",code:"EXFP-CP01",lat:-29.660,lng:-50.625,klass:"serra",alt:430,rio:"Corticeiras",bacia:"rolante"},
  {id:"48B1162D47EC0FE6",code:"RSRZ-CH01",lat:-29.565,lng:-50.420,klass:"local",alt:130,rio:"Rio Chuvisca",bacia:"rolante",cotaAlerta:1.5},
  {id:"80500E0214FFDAF4",code:"RSRL-CE01",lat:-29.650,lng:-50.575,klass:"local",alt:90,rio:"Rio Rolante",bacia:"rolante"},
  {id:"2CAED8D9CB62CEB5",code:"RSRL-BV01",lat:-29.640,lng:-50.560,klass:"local",alt:85,rio:"Boa Vista (baixo)",bacia:"rolante"},
  {id:"CF98FCFA7E9EE7C1",code:"RSRL-AR01",lat:-29.640,lng:-50.530,klass:"local",alt:78,rio:"Rio Areia",bacia:"areia"},
  {id:"04109F675953A131",code:"RSRL-BE01",lat:-29.565,lng:-50.535,klass:"local",alt:60,rio:"Rio Areia (norte)",bacia:"areia",cotaNormal:0.09,cotaAlerta:0.7},
  {id:"BB45660B199C5677",code:"RSRL-RB01",lat:-29.635,lng:-50.545,klass:"local",alt:52,rio:"Rolante (centro)",bacia:"rolante"},
  {id:"5BA69743261D364A",code:"RSRL-RB02",lat:-29.650,lng:-50.545,klass:"local",alt:44,rio:"Rolante (ponte)",bacia:"rolante"}
];
export const REGBY={}; REG.forEach(r=>REGBY[r.id]=r);
/* Referencia da cidade: ponte do Rolante (RSRL-RB02), centro urbano.
   Distancia em km inteiro so para situar a estacao no mapa mental. */
const CIDADE={lat:-29.650,lng:-50.545};

function kmCidade(reg){
  const dy=(reg.lat-CIDADE.lat)*111.32;
  const dx=(reg.lng-CIDADE.lng)*111.32*Math.cos(CIDADE.lat*Math.PI/180);
  return Math.round(Math.sqrt(dx*dx+dy*dy));
}
export function codeTail(code){ return code.split("-")[1]||code; }
export function kmTxt(reg){ const k=kmCidade(reg); return k<1?"no centro":k+" km da cidade"; }
/* Nome de rio para PROSA: tira o qualificador entre parenteses do registro.
   "Rio Areia (norte)" -> "Rio Areia", "Faxinal (cabeceira)" -> "Faxinal".
   Sem isso o texto saia com parentese dentro de parentese. O nome completo
   continua nos cards de estacao, onde serve para distinguir as reguas. */
export function rioNome(reg){ return reg.rio.replace(/\s*\(.*$/,"").trim(); }
/* Artigo certo na frase. O registro tem "Rio Areia (norte)" mas tambem
   "Boa Vista" e "Mascarada": sem tratar, o texto saia "sobre o Boa Vista". */
export function rioArt(reg){ const n=rioNome(reg); return /^Rio\s/i.test(n)?("o "+n):("o rio "+n); }

/* Confianca vale SO para altura de rio. Quem mede nivel sao as reguas RSRL:
   GLLS e BE01 sao as leituras validadas; AR01 esta em conferencia
   (aparece no painel, nao comanda o risco). CE02 esta fora: regua com dado
   nao confiavel, removida do registro ate o conserto. Todas as demais sao
   estacoes abertas de terceiros das quais so coletamos: medem CHUVA e entram
   normal na analise. Capacidade (chuva/nivel) vem do dado, nao de lista fixa. */
export const LEVEL_TRUST=new Set(["RSRL-GLLS","RSRL-BE01"]);
export const LEVEL_CHECK=new Set(["RSRL-AR01"]);
/* Sensor de umidade da RSRL-RB02 esta descalibrado: media de 16 % nas
   ultimas 24 h com 45,5 mm de chuva na ultima hora e vizinhas (RB01, BE01,
   GLLS) todas acima de 50 %. Nao e pico isolado (filtro <5% nao pega), e
   erro sistematico do sensor — exclui so a umidade dessa estacao, o resto
   dos dados (nivel, chuva) continua valendo. */
export const HUM_UNTRUSTED=new Set(["RSRL-RB02"]);
/* Regua velha nao decide nada: leitura precisa ter no maximo LV_MAXAGE horas
   e cobertura minima na ultima meia dia. A GLLS mandou 1 leitura em 121 h
   (2,04 m = 102 % da cota) e o back-fill espalhava esse valor unico por toda
   a janela: a regua "estava na cota" desde o comeco do grafico. */
export const LV_CARRY=3, LV_MAXAGE=3, LV_MINCOV=3;

export const BACIAS={ rolante:{key:"rolante",nome:"Rio Rolante",sub:"vem de Riozinho"}, areia:{key:"areia",nome:"Rio Areia",sub:"deságua no Rolante no fim da cidade"} };
