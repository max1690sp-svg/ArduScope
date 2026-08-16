/*
 * ASCOM Arduino Equatorial Mount Firmware
 * 
 * Прошивка для управления экваториальной монтировкой телескопа
 * через последовательный порт (USB)
 * 
 * Поддерживаемые команды:
 * - MOVE_RA <speed>    : Движение по оси RA (-1.0 до 1.0)
 * - MOVE_DEC <speed>   : Движение по оси DEC (-1.0 до 1.0)
 * - STOP               : Остановить все движения
 * - GUIDE <dir> <ms>   : Гидирование (N, S, E, W) в течение указанного времени
 * - STATUS             : Получить текущий статус
 * - HOME               : Переход в домашнюю позицию
 */

// Пины для управления шаговыми двигателями (пример для драйверов A4988/DRV8825)
#define RA_STEP_PIN     2
#define RA_DIR_PIN      3
#define RA_ENABLE_PIN   4

#define DEC_STEP_PIN    5
#define DEC_DIR_PIN     6
#define DEC_ENABLE_PIN  7

// Параметры двигателей
#define STEPS_PER_REVOLUTION  200      // Шагов на оборот двигателя
#define MICROSTEPS            16       // Микроступеней (зависит от драйвера)
#define RA_GEAR_RATIO         144.0    // Передаточное отношение оси RA
#define DEC_GEAR_RATIO        144.0    // Передаточное отношение оси DEC

// Скорость звездного трекинга (в шагах в секунду)
#define SIDEREAL_RATE         (360.0 / 23.9344696 / 3600.0)  // Градусов в секунду

// Глобальные переменные
double raSpeed = 0.0;
double decSpeed = 0.0;
bool isGuiding = false;
unsigned long guideStartTime = 0;
unsigned long guideDuration = 0;
char guideDirection = 0;

// Позиции (в шагах от домашней позиции)
long raPosition = 0;
long decPosition = 0;

// Тайминг для шагов
unsigned long lastRaStepTime = 0;
unsigned long lastDecStepTime = 0;
int raStepInterval = 0;
int decStepInterval = 0;

void setup() {
  // Инициализация последовательного порта
  Serial.begin(9600);
  while (!Serial) {
    ; // Ждем подключения USB
  }

  // Настройка пинов
  pinMode(RA_STEP_PIN, OUTPUT);
  pinMode(RA_DIR_PIN, OUTPUT);
  pinMode(RA_ENABLE_PIN, OUTPUT);
  
  pinMode(DEC_STEP_PIN, OUTPUT);
  pinMode(DEC_DIR_PIN, OUTPUT);
  pinMode(DEC_ENABLE_PIN, OUTPUT);

  // Включаем драйверы (LOW = включен, HIGH = выключен)
  digitalWrite(RA_ENABLE_PIN, LOW);
  digitalWrite(DEC_ENABLE_PIN, LOW);

  // Останавливаем двигатели изначально
  raSpeed = 0.0;
  decSpeed = 0.0;
  
  Serial.println("OK");
  Serial.println("Arduino Equatorial Mount Ready");
}

void loop() {
  // Обработка команд от последовательного порта
  if (Serial.available() > 0) {
    handleCommand();
  }

  // Проверка гидирования
  if (isGuiding && millis() - guideStartTime >= guideDuration) {
    isGuiding = false;
    raSpeed = 0.0;
    decSpeed = 0.0;
  }

  // Выполнение движения по оси RA
  if (raSpeed != 0.0) {
    performRaStep();
  }

  // Выполнение движения по оси DEC
  if (decSpeed != 0.0) {
    performDecStep();
  }
}

void handleCommand() {
  String command = Serial.readStringUntil('\n');
  command.trim();
  
  if (command.startsWith("MOVE_RA")) {
    double speed = command.substring(8).toDouble();
    raSpeed = constrain(speed, -1.0, 1.0);
    calculateRaStepInterval();
    Serial.println("OK");
  }
  else if (command.startsWith("MOVE_DEC")) {
    double speed = command.substring(9).toDouble();
    decSpeed = constrain(speed, -1.0, 1.0);
    calculateDecStepInterval();
    Serial.println("OK");
  }
  else if (command == "STOP") {
    raSpeed = 0.0;
    decSpeed = 0.0;
    isGuiding = false;
    Serial.println("OK");
  }
  else if (command.startsWith("GUIDE")) {
    // Формат: GUIDE N|S|E|W <duration_ms>
    char direction = command.charAt(6);
    int duration = command.substring(8).toInt();
    
    guideDirection = direction;
    guideDuration = duration;
    guideStartTime = millis();
    isGuiding = true;
    
    // Устанавливаем направление и скорость для гидирования
    setGuideDirection(direction);
    raSpeed = 0.5; // Скорость гидирования
    decSpeed = 0.0;
    calculateRaStepInterval();
    calculateDecStepInterval();
    
    Serial.println("OK");
  }
  else if (command == "STATUS") {
    // Возвращаем текущие координаты (в часах для RA, градусах для DEC)
    double raHours = (raPosition % (360 * 15 * STEPS_PER_REVOLUTION * MICROSTEPS)) / 
                     (15.0 * STEPS_PER_REVOLUTION * MICROSTEPS);
    if (raHours < 0) raHours += 24.0;
    
    double decDegrees = decPosition / (double)(STEPS_PER_REVOLUTION * MICROSTEPS);
    
    Serial.print("STATUS:RA=");
    Serial.print(raHours, 4);
    Serial.print(",DEC=");
    Serial.println(decDegrees, 4);
  }
  else if (command == "HOME") {
    goToHome();
    Serial.println("OK");
  }
  else {
    Serial.println("ERROR: Unknown command");
  }
}

void setGuideDirection(char direction) {
  switch (direction) {
    case 'N':
      digitalWrite(DEC_DIR_PIN, HIGH);
      digitalWrite(RA_DIR_PIN, HIGH);
      decSpeed = 0.5;
      raSpeed = 0.0;
      break;
    case 'S':
      digitalWrite(DEC_DIR_PIN, LOW);
      digitalWrite(RA_DIR_PIN, HIGH);
      decSpeed = 0.5;
      raSpeed = 0.0;
      break;
    case 'E':
      digitalWrite(RA_DIR_PIN, HIGH);
      digitalWrite(DEC_DIR_PIN, HIGH);
      raSpeed = 0.5;
      decSpeed = 0.0;
      break;
    case 'W':
      digitalWrite(RA_DIR_PIN, LOW);
      digitalWrite(DEC_DIR_PIN, HIGH);
      raSpeed = 0.5;
      decSpeed = 0.0;
      break;
  }
}

void calculateRaStepInterval() {
  if (raSpeed == 0.0) {
    raStepInterval = 0;
    return;
  }
  
  // Вычисляем интервал между шагами в микросекундах
  double stepsPerSecond = fabs(raSpeed) * STEPS_PER_REVOLUTION * MICROSTEPS * RA_GEAR_RATIO;
  if (stepsPerSecond > 0) {
    raStepInterval = (int)(1000000.0 / stepsPerSecond);
  }
}

void calculateDecStepInterval() {
  if (decSpeed == 0.0) {
    decStepInterval = 0;
    return;
  }
  
  double stepsPerSecond = fabs(decSpeed) * STEPS_PER_REVOLUTION * MICROSTEPS * DEC_GEAR_RATIO;
  if (stepsPerSecond > 0) {
    decStepInterval = (int)(1000000.0 / stepsPerSecond);
  }
}

void performRaStep() {
  unsigned long currentTime = micros();
  
  if (raStepInterval > 0 && currentTime - lastRaStepTime >= (unsigned long)raStepInterval) {
    // Делаем шаг
    digitalWrite(RA_STEP_PIN, HIGH);
    delayMicroseconds(10);
    digitalWrite(RA_STEP_PIN, LOW);
    
    // Обновляем позицию
    if (raSpeed > 0) {
      raPosition++;
    } else {
      raPosition--;
    }
    
    lastRaStepTime = currentTime;
  }
}

void performDecStep() {
  unsigned long currentTime = micros();
  
  if (decStepInterval > 0 && currentTime - lastDecStepTime >= (unsigned long)decStepInterval) {
    // Делаем шаг
    digitalWrite(DEC_STEP_PIN, HIGH);
    delayMicroseconds(10);
    digitalWrite(DEC_STEP_PIN, LOW);
    
    // Обновляем позицию
    if (decSpeed > 0) {
      decPosition++;
    } else {
      decPosition--;
    }
    
    lastDecStepTime = currentTime;
  }
}

void goToHome() {
  // Простая реализация - сброс позиции в ноль
  // В полной версии здесь должен быть поиск концевого выключателя
  raPosition = 0;
  decPosition = 0;
  
  // Небольшая задержка
  delay(100);
}
