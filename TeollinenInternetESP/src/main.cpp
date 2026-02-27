#include <Arduino.h>
#include <HTTPClient.h>
#include <WiFi.h>

#define WIFI_SSID "Koiranruokanetti"
#define WIFI_PASSWORD "Maaritaverkkoyhteys22"
#define API_ENDPOINT "http://ipaddress:3001/api/data"
#define SOUND_SPEED (0.034)

typedef struct {
  int x_pin, //EI
  y_pin, //Ei
  sw_pin; //Ei
} Joystick;

typedef struct {
  int pin; 
  double value;
} Potentiometer;

typedef struct {
  int tq_pin,     //Ei
  echo_pin;       //EI
  double distance;
} URM;

typedef struct {
  int jx_value,
      jy_value,
      jsw_value;
  double potentiometer_value,
      urm_distance;
} DeviceState;

Joystick joystick = {34, 35, 5};
Potentiometer potentiometer = {33, 0.0};
URM urm = {26, 32, 0.0};

//------------------- Initialization Functions ------------------//

void init_joystick() {
  pinMode(joystick.x_pin, INPUT);
  pinMode(joystick.y_pin, INPUT);
  pinMode(joystick.sw_pin, INPUT_PULLUP);
}

void init_potentiometer() {
  pinMode(potentiometer.pin, INPUT);
}

void init_urm() {
  if (!digitalPinCanOutput(urm.tq_pin)) {
    Serial.println("URM trigger pin is not output-capable");
    return;
  }
  pinMode(urm.tq_pin, OUTPUT);
  pinMode(urm.echo_pin, INPUT);
}

long read_urm(){
  if (!digitalPinCanOutput(urm.tq_pin)) {
    return 0;
  }
  digitalWrite(urm.tq_pin, LOW);
  delayMicroseconds(2);
  digitalWrite(urm.tq_pin, HIGH);
  delayMicroseconds(10);
  digitalWrite(urm.tq_pin, LOW);

  long duration = pulseIn(urm.echo_pin, HIGH);
  return (duration * SOUND_SPEED) / 2;

}

void init_wifi() {
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  int max_attempts = 20;

  Serial.print("Connecting to WiFi");
  while (WiFi.status() != WL_CONNECTED) {
    if(max_attempts-- <= 0) {
      Serial.println("Failed to connect to WiFi");
      return;
    }
    delay(500);
    Serial.print(".");
  }
  Serial.println("WiFi connected");
}

//---------------------- Utility Functions ------------------//

void post_data(DeviceState * state) {
  if(WiFi.status() == WL_CONNECTED) {
    HTTPClient http;
    
    if(!http.begin(API_ENDPOINT))
    {
      Serial.println("Unable to connect to API endpoint");
      return;
    }

    http.addHeader("Content-Type", "application/json");
    String json_payload = "{";
    json_payload += "\"x\":" + String(state->jx_value) + ",";
    json_payload += "\"y\":" + String(state->jy_value) + ",";
    json_payload += "\"sw\":" + String(state->jsw_value) + ",";
    json_payload += "\"pot\":" + String(state->potentiometer_value, 5) + ",";
    json_payload += "\"urm\":" + String(state->urm_distance, 5);
    json_payload += "}";

    //int httpResponseCode = http.POST(json_payload);
    int httpResponseCode = 0;
    if(httpResponseCode > 0) {
      Serial.println("Data posted successfully");
    } else {
      Serial.println("Error posting data: " + String(httpResponseCode));
      Serial.println("Payload: " + json_payload);
    }
    http.end();
  } else {
    Serial.println("WiFi not connected, cannot post data");
  }
}

//------------------- Main loop and setup ------------------//

void setup() {
  Serial.begin(115200);
  init_joystick();
  init_potentiometer();
  init_urm();
  delay(1000);
  Serial.println("Initializing WiFi...");
  init_wifi();
}

void loop() {
  DeviceState state;
  int x_value = analogRead(joystick.x_pin);
  int y_value = analogRead(joystick.y_pin);
  //int y_value = 0; // Placeholder since y_pin is not used
  int sw_value = !digitalRead(joystick.sw_pin);

  potentiometer.value = analogRead(potentiometer.pin) / 4095.0; // Normalize to 0-1

  Serial.print("X: ");
  Serial.print(x_value);
  Serial.print(" | Y: ");
  Serial.print(y_value);
  Serial.print(" | SW: ");
  Serial.println(sw_value);

  Serial.print("Potentiometer: ");
  Serial.println(potentiometer.value, 4); // Print with 4 decimal places

  // Measure distance using URM
  urm.distance = read_urm();
  //urm.distance = 0; // Placeholder since URM is not used
  //urm.distance = 123.45; // Placeholder value for testing
  Serial.print("Distance: ");
  Serial.print(urm.distance);

  state.jx_value = x_value;
  state.jy_value = y_value;
  state.jsw_value = sw_value;
  state.potentiometer_value = potentiometer.value;
  state.urm_distance = urm.distance;

  post_data(&state);
  delay(2000);
}
