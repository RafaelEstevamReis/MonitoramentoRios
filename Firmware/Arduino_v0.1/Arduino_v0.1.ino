#include <WiFi.h>
#include <DHT.h>
#include <HTTPClient.h>
#include "dados_estacao.h"

// Endpoint
const char* endpoint1 = "https://monitorarios.xyz/Up/upload";
const char* endpoint2 = nullptr; 

extern "C" {
  uint8_t temprature_sens_read();
}

// Objetos
DHT dht(DHT_PIN, DHT_TYPE);

float readAdcVoltage() {
  int rawValue = analogRead(ADC_PIN);
  return ((float)rawValue / 4095.0) * 3.3; // Supondo referência de 3.3V
}
float vBatToPercent(float vBat){
  if(vBat == NAN) return NAN;

  // Max/Min
  float max = vBatTable[0][1];
  float min = vBatTable[vBatTableRows-1][1];

  if(vBat >= max) return 100;
  if(vBat <= min) return 0;

  // Percorre a tabela até a penúltima linha (pois estamos comparando a linha atual com a próxima)
  for (int i = 0; i < vBatTableRows - 1; i++) {
    float currRow_vL = vBatTable[i+1][1];
    float currRow_vH = vBatTable[i][1];

    if (vBat >= currRow_vL && vBat <= currRow_vH) {
      return mapfloat(vBat, currRow_vL, currRow_vH, vBatTable[i+1][0], vBatTable[i][0]);
    }
  }
  return 0;
}

/* WIFI */

void connectToWiFi() {
  if (WiFi.status() == WL_CONNECTED) return;
  
  for (int i = 0; i < num_wifi_networks; i++) {
    WiFi.begin(wifi_ssids[i], wifi_passwords[i]);
    Serial.print("\nConectando a ");
    Serial.print(wifi_ssids[i]);

    int retry_count = 0;
    while (WiFi.status() != WL_CONNECTED && retry_count < 10) {
      delay(1000);
      Serial.print(".");
      retry_count++;
    }

    if (WiFi.status() == WL_CONNECTED) {
      Serial.println("\nConectado com sucesso!");
      return;
    }
  }
  Serial.println("\nFalha ao conectar a todas as redes WiFi.");
}
void disconnectWiFi() {
  Serial.println("Desconectando do WiFi...");
  WiFi.disconnect();
}

/* DATA UPLOAD */
void sendData() {
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi desconectado - DATA SKIP");
    return;
  }

  float vBatVoltage = readAdcVoltage() * vBatCal;
  float vBatPercent = vBatToPercent(vBatVoltage);
  float temperature = dht.readTemperature();
  float humidity = dht.readHumidity();
  float intTemp_celsius = readInternalTemp();
  
  String payload = "{";
  // Sensors
  if (isnan(vBatVoltage)) Serial.println("Erro ao ler a vBat!");
  else {
    payload += "\"TensaoBateria\": " + String(vBatVoltage) + ", ";
    payload += "\"PercentBateria\": " + String(vBatPercent) + ", ";
  }
  
  if (isnan(temperature)) Serial.println("Erro ao ler a temperatura!");
  else payload += "\"TemperaturaAr\": " + String(temperature) + ", ";

  if (isnan(humidity)) Serial.println("Erro ao ler a umidade!");
  else payload += "\"UmidadeAr\": " + String(humidity) + ", ";

  // Internals
  if (isnan(intTemp_celsius)) Serial.println("Erro ao ler a temp_int!");
  else payload += "\"TemperaturaInterna\": " + String(intTemp_celsius) + ", ";

  payload += "\"ForcaSinal\": " + String(WiFi.RSSI()) + ", ";
  payload += "\"ssid\": \"" + String(WiFi.SSID()) + "\", ";
  payload += "\"bssid\": \"" + String(WiFi.BSSIDstr()) + "\",";
  // Last
  payload += "\"type\": \""+ String(board_info) +"\"";
  payload += "}";
  Serial.printf("Payload %s\n", payload.c_str());

  if (endpoint1 != nullptr) sendPayloadTo(endpoint1, payload);
  else Serial.println("NULL ENDPOINT 1 - DO NOT SEND");
  
  if (endpoint2 != nullptr) sendPayloadTo(endpoint2, payload);
  else Serial.println("NULL ENDPOINT 2 - DO NOT SEND");
}
void sendPayloadTo(const char* endpoint, String payload) {
  Serial.printf("Sendind data to: %s\n", endpoint);
  
  HTTPClient http;
  http.begin(endpoint);
  http.addHeader("Content-Type", "application/json");
  http.addHeader("x-key", api_key);

  int httpResponseCode = http.POST(payload);

  if (httpResponseCode > 0) {
    Serial.printf("HTTP Response code: %d\n", httpResponseCode);
  } else {
    Serial.printf("Failed to send data: %s\n", http.errorToString(httpResponseCode).c_str());
  }

  http.end();
}

/* Internals */
float readInternalTemp(){
  uint8_t raw_int_temp;

  for(int i = 0; i < 10; i++) {
    raw_int_temp = temprature_sens_read();
    //Serial.printf("temprature_sens_read: %d\n", raw_int_temp);

    if(raw_int_temp != 128)  break;
    delay(10);
  }

  return raw_int_temp == 128 ? NAN : (raw_int_temp - 32) / 1.8f;
}

/* Helpers */ 
float mapfloat(float x, float in_min, float in_max, float out_min, float out_max)
{
  return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
}

void checkSleep(){
  if(sleep_after == 0) return; // do not sleep
  if(sleep_for == 0) return; // do not sleep

  unsigned long tempoDecorrido = millis();
  
  if (tempoDecorrido >= sleep_after * 1000) {    
    Serial.println("Entering deep sleep");
    delay(1000);
    esp_deep_sleep(sleep_for * 1000000); // 3 minutos em microsegundos
  }
}

void setup() {
  Serial.begin(9600);
  delay(1000);
  Serial.printf("\n\nBoot: %s\n", board_info);
  
  // Seta Pinos
  pinMode(ADC_PIN, INPUT);
  pinMode(PULSE_PIN, INPUT);
  // Objetos
  dht.begin();
}

void loop() {
  connectToWiFi();
  sendData();  
  disconnectWiFi();

  //delay(120000); // A cada 2 minutos
  
  checkSleep(); // Se entrar em sleep, não retorna daqui

  delay(10 * 1000); // wait 10s
}
