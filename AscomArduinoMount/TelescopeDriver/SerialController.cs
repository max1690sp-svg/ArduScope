using System;
using System.IO.Ports;
using System.Threading;
using ASCOM.DriverAccess.Telescope;

namespace AscomArduinoMount.Telescope
{
    /// <summary>
    /// Контроллер для связи с Arduino через последовательный порт
    /// </summary>
    public class SerialController : IDisposable
    {
        private SerialPort _serialPort;
        private bool _isConnected;
        private readonly object _lockObj = new object();
        
        public string PortName { get; set; } = "COM3";
        public int BaudRate { get; set; } = 9600;
        public bool IsConnected => _isConnected;

        public event EventHandler<string> DataReceived;

        public void Connect()
        {
            if (_isConnected) return;

            lock (_lockObj)
            {
                _serialPort = new SerialPort(PortName, BaudRate, Parity.None, 8, StopBits.One);
                _serialPort.ReadTimeout = 1000;
                _serialPort.WriteTimeout = 1000;
                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();
                _isConnected = true;
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;

            lock (_lockObj)
            {
                try
                {
                    _serialPort.DataReceived -= OnDataReceived;
                    _serialPort.Close();
                    _serialPort.Dispose();
                }
                finally
                {
                    _isConnected = false;
                }
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                while (_serialPort.BytesToRead > 0)
                {
                    string line = _serialPort.ReadLine().Trim();
                    DataReceived?.Invoke(this, line);
                }
            }
            catch (Exception)
            {
                // Игнорируем ошибки чтения
            }
        }

        public string SendCommand(string command, int timeoutMs = 2000)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Порт не подключен");

            lock (_lockObj)
            {
                try
                {
                    _serialPort.WriteLine(command);
                    
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    while (stopwatch.ElapsedMilliseconds < timeoutMs)
                    {
                        if (_serialPort.BytesToRead > 0)
                        {
                            string response = _serialPort.ReadLine().Trim();
                            return response;
                        }
                        Thread.Sleep(10);
                    }
                    
                    throw new TimeoutException($"Таймаут ожидания ответа на команду: {command}");
                }
                catch (TimeoutException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Ошибка отправки команды: {ex.Message}", ex);
                }
            }
        }

        public void MoveRA(double speed)
        {
            // speed от -1.0 до 1.0
            speed = Math.Max(-1.0, Math.Min(1.0, speed));
            SendCommand($"MOVE_RA {speed:F3}");
        }

        public void MoveDEC(double speed)
        {
            // speed от -1.0 до 1.0
            speed = Math.Max(-1.0, Math.Min(1.0, speed));
            SendCommand($"MOVE_DEC {speed:F3}");
        }

        public void Stop()
        {
            SendCommand("STOP");
        }

        public void Guide(GuideDirections direction, int durationMs)
        {
            string dir = direction switch
            {
                GuideDirections.guideNorth => "N",
                GuideDirections.guideSouth => "S",
                GuideDirections.guideEast => "E",
                GuideDirections.guideWest => "W",
                _ => throw new ArgumentException("Неверное направление гидирования")
            };
            
            SendCommand($"GUIDE {dir} {durationMs}");
        }

        public void Home()
        {
            SendCommand("HOME");
        }

        public (double ra, double dec) GetStatus()
        {
            string response = SendCommand("STATUS");
            
            // Ожидаем формат: STATUS:RA=<value>,DEC=<value>
            if (response.StartsWith("STATUS:"))
            {
                response = response.Substring(7);
                var parts = response.Split(',');
                if (parts.Length == 2)
                {
                    double ra = double.Parse(parts[0].Split('=')[1]);
                    double dec = double.Parse(parts[1].Split('=')[1]);
                    return (ra, dec);
                }
            }
            
            return (0.0, 0.0);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
