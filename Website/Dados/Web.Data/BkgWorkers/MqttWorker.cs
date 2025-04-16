namespace Web.Data.BkgWorkers;

using Microsoft.Extensions.Hosting;
using MQTTnet;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Web.Data.DAO;

public class MqttWorker : IHostedService, IDisposable
{
    private readonly ILogger logger;
    private readonly CancellationTokenSource tokenSrc;
    private readonly CancellationToken token;
    private readonly MqttClientOptions mqttClientOptions;
    private readonly DB db;

    public MqttWorker(ILogger logger, DB db, string? host, string? user, string? pass)
    {
        this.db = db;
        this.logger = logger;
        tokenSrc = new CancellationTokenSource();
        token = tokenSrc.Token;

        mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(host)
            .WithCredentials(user, pass)
            .Build();
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var mqttFactory = new MqttClientFactory();
            while (true)
            {
                if (token.IsCancellationRequested) break;
                logger.Information("[LoRaMQTT] Starting up...");

                await DoMqtt(mqttFactory);
                await Task.Delay(1000, token);
            }
        }, cancellationToken);
    }
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        tokenSrc.Cancel();
    }
    public void Dispose()
    {
        tokenSrc.Cancel();
    }

    private async Task DoMqtt(MqttClientFactory mqttFactory)
    {
        try
        {
            using var mqttClient = mqttFactory.CreateMqttClient();
            var r = await mqttClient.ConnectAsync(mqttClientOptions, token);
            if (r.ResultCode != MqttClientConnectResultCode.Success)
            {
                Log.Warning("[LoRaMQTT] Failed to Connect");
                return;
            }

            logger.Information("[LoRaMQTT] Connected");
            mqttClient.ApplicationMessageReceivedAsync += msgReceived;

            var mqttSubscribeOptions = mqttFactory
                .CreateSubscribeOptionsBuilder()
                .WithTopicFilter("#")
                .Build();
            await mqttClient.SubscribeAsync(mqttSubscribeOptions, token);

            while (true)
            {
                if (token.IsCancellationRequested) break;
                await Task.Delay(1000, token);
                if (!mqttClient.IsConnected)
                {
                    Log.Warning("[LoRaMQTT] Disconnected!");
                    return;
                }
            }

            await mqttClient.DisconnectAsync(new MqttClientDisconnectOptionsBuilder().WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection).Build());
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[LoRaMQTT] ERROR");
        }
    }
    private async Task msgReceived(MqttApplicationMessageReceivedEventArgs args)
    {
        string topic = "";
        byte[] payload = [];
        string msg = "";
        try
        {
            topic = args.ApplicationMessage.Topic;
            payload = args.ApplicationMessage.Payload.ToArray();
            msg = Encoding.UTF8.GetString(payload);

            // Fire-and-Forget
            processMessage(args, topic, args.ApplicationMessage.Payload.ToArray(), msg);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "[LoRaMQTT] msgReceived:ERROR T:{topic} Payload: {strMsg}|{pl}", topic, msg, BitConverter.ToString(payload).Replace("-", ""));
        }
        // Encerra
        await Task.CompletedTask;
    }
    private async void processMessage(MqttApplicationMessageReceivedEventArgs args, string topic, byte[] payload, string msg)
    {
        if (!topic.Contains("/json/")) return; // ignorar binários

        var jsonEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<JsonEvent>(msg);
        if (jsonEvent.type != "text") return;

        // O payload será:
        // a. um objeto JObject
        // b. um número quando o conteúdo da mensagem for um número
        if (jsonEvent.payload is not JObject jPayload) return; // Não é dado de estação

        // converte para eventText
        var jsonEventPayloadText = jPayload.ToObject<JsonEventPayloadText>();
        await processaMensagemTexto(args, topic, jsonEvent, jsonEventPayloadText?.text ?? "");
    }

    private async Task processaMensagemTexto(MqttApplicationMessageReceivedEventArgs args, string topic, JsonEvent jsonEvent, string text)
    {
        var lines = text.Replace("\r", "")
                        .Split('\n')
                        .Where(ehLinhaEstacao);
        await gravaDadosEstacoes(topic, jsonEvent, lines);
    }
    private bool ehLinhaEstacao(string l)
    {
        //   0         1         2         3         4         5
        //   012345678901234567890123456789012345678901234567890123456789
        // > RSRL-BE01:WL=0.1;vB=4.22;Tp=22.5;Hd=93.0;sg=-85.0;r=695
        if (l.Length < 30) return false;

        if (l[4] != '-') return false;
        if (l[9] != ':') return false;

        if (!l.Contains(";sg=-")) return false; // tem nível de sinal
        if (!l.Contains(";r=")) return false; // tem nonce

        return true;
    }
    private async Task gravaDadosEstacoes(string topic, JsonEvent jsonEvent, IEnumerable<string> linhas)
    {
        var estacoes = db.ListarEstacoes()
                         .OrderByDescending(o => o.UltimoEnvio)
                         .ToArray();
        var estacoesRegistrar = new List<DAO.DBModels.TBDadosEstacoes>();
        foreach (var linha in linhas)
        {
            string from = $"!{jsonEvent.from:x2}";
            logger.Information("[LoRa] Recebido dados de {from} {linha} ", from, linha);
            var dadosEstacao = serializaDadosEstacao(jsonEvent, estacoes, linha, from);
            dadosEstacao = dadosEstacao;
            estacoesRegistrar.Add(dadosEstacao);
        }
        if (estacoesRegistrar.Count == 0) return;

        // Verifica NONCE e grava
        var ultimosEnviosEstacoes = db.ListarDados();
        foreach (var e in estacoesRegistrar)
        {
            var nonces = ultimosEnviosEstacoes.Where(o => o.Estacao == e.Estacao)
                                              .Where(o => (DateTime.UtcNow - o.RecebidoUTC).TotalMinutes < 60)
                                              .Select(o => o.Nonce)
                                              .ToHashSet();
            if (nonces.Contains(e.Nonce))
            {
                logger.Information("[LoRa] NonceSkip {estacao} r={nonce} ", e.Estacao, e.Nonce);
                continue; // Já tem
            }
            // Registra
            db.Registra(e);
        }
    }

    private DAO.DBModels.TBDadosEstacoes serializaDadosEstacao(JsonEvent jsonEvent, DAO.DBModels.TBEstacoes[] estacoes, string linha, string nodeFrom)
    {
        var dRow = linha.Split(':');
        string nome = dRow[0];
        var partes = dRow[1].Split(";");

        var t = new DAO.DBModels.TBDadosEstacoes
        {
            // Base
            Id = 0,
            RecebidoUTC = DateTime.UtcNow,
            Estacao = estacoes.FirstOrDefault(o => o.NomeEstacao.Equals(nome, StringComparison.InvariantCultureIgnoreCase))?.Estacao ?? "",
            type = "loraMQTT",
            RawData = linha,
            IP_Origem = nodeFrom,
            DataHoraDadosUTC = DateTime.UnixEpoch.AddSeconds(jsonEvent.timestamp),
        };

        // WL=0.1;vB=4.22;Tp=22.5;Hd=93.0;sg=-85.0;r=695
        foreach (var p in partes)
        {
            var pair = p.Split('=');
            var decVal = pair[1].ToDecimal();
            // if (decVal == 0) continue; // 0.0m é nível razoável, há locais com 0.05

            switch (pair[0].ToUpper())
            {
                case "WL":
                    t.NivelRio = decVal;
                    break;
                case "VB":
                    t.TensaoBateria = decVal;
                    break;
                case "TEMP":
                case "TP":
                    t.TemperaturaAr = decVal;
                    break;
                case "HUMD":
                case "HD":
                    t.UmidadeAr = decVal;
                    break;
                case "SG":
                    t.ForcaSinal = decVal;
                    break;
                case "R":
                    t.Nonce = int.Parse(pair[1]);
                    break;
            }
        }
        return t;
    }

    public class JsonEvent
    {
        public int channel { get; set; }
        public long from { get; set; }
        public int hop_start { get; set; }
        public int hops_away { get; set; }
        public long id { get; set; }
        public object payload { get; set; }
        public decimal rssi { get; set; }
        public string sender { get; set; }
        public decimal snr { get; set; }
        public long timestamp { get; set; }
        public long to { get; set; }
        public string type { get; set; }
    }
    public class JsonEventPayloadText
    {
        public string text { get; set; }
    }
}
