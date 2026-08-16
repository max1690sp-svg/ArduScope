using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ASCOM;

namespace AscomArduinoMount.Telescope
{
    /// <summary>
    /// Класс настройки драйвера (Setup Dialog) с выбором COM-порта
    /// </summary>
    [Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SetupDialogForm : ISetupDialogV2
    {
        private Telescope _telescope;
        private static string _selectedPort = "";
        private static int _baudRate = 9600;

        public SetupDialogForm()
        {
            // Конструктор
        }

        public void Configure(IntPtr parentHandle)
        {
            using (var form = new PortSelectionForm(_selectedPort, _baudRate))
            {
                if (parentHandle != IntPtr.Zero)
                {
                    form.Owner = Form.FromHandle(parentHandle);
                }

                if (form.ShowDialog() == DialogResult.OK)
                {
                    _selectedPort = form.SelectedPort;
                    _baudRate = form.BaudRate;
                    
                    // Сохраняем настройки в телескоп
                    if (_telescope != null)
                    {
                        _telescope.SetPort(_selectedPort);
                        _telescope.SetBaudRate(_baudRate);
                    }
                }
            }
        }

        public void SetTelescope(Telescope telescope)
        {
            _telescope = telescope;
        }

        public static string GetSelectedPort() => _selectedPort;
        public static int GetBaudRate() => _baudRate;

        public void Dispose()
        {
            // Очистка ресурсов
        }
    }

    /// <summary>
    /// Форма выбора COM-порта
    /// </summary>
    internal class PortSelectionForm : Form
    {
        public string SelectedPort { get; private set; }
        public int BaudRate { get; private set; }

        private readonly ComboBox _cmbPorts;
        private readonly Button _btnRefresh;
        private readonly Button _btnOK;
        private readonly Button _btnCancel;
        private readonly Label _lblBaud;

        public PortSelectionForm(string currentPort, int currentBaudRate)
        {
            SelectedPort = currentPort;
            BaudRate = currentBaudRate;

            Text = "ASCOM Arduino Mount - Настройки";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new System.Drawing.Size(400, 220);
            FormAcceptButton = _btnOK;
            FormCancelButton = _btnCancel;

            // Label для выбора порта
            var lblPort = new Label
            {
                Text = "Выберите COM порт:",
                Location = new System.Drawing.Point(20, 20),
                AutoSize = true
            };
            Controls.Add(lblPort);

            // ComboBox для портов
            _cmbPorts = new ComboBox
            {
                Location = new System.Drawing.Point(20, 45),
                Width = 200,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            Controls.Add(_cmbPorts);

            // Кнопка обновления списка портов
            _btnRefresh = new Button
            {
                Text = "Обновить",
                Location = new System.Drawing.Point(230, 43),
                Width = 100,
                Height = 25
            };
            _btnRefresh.Click += BtnRefresh_Click;
            Controls.Add(_btnRefresh);

            // Label для скорости (информация)
            _lblBaud = new Label
            {
                Text = $"Скорость (Baud): {BaudRate}",
                Location = new System.Drawing.Point(20, 80),
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray
            };
            Controls.Add(_lblBaud);

            // Пояснение
            var lblInfo = new Label
            {
                Text = "Подключите Arduino Uno и выберите соответствующий COM-порт.",
                Location = new System.Drawing.Point(20, 105),
                AutoSize = true,
                Font = new System.Drawing.Font(Font.FontFamily, 8)
            };
            Controls.Add(lblInfo);

            // OK кнопка
            _btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(120, 150),
                Width = 100
            };
            Controls.Add(_btnOK);
            AcceptButton = _btnOK;

            // Cancel кнопка
            _btnCancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(230, 150),
                Width = 100
            };
            Controls.Add(_btnCancel);
            CancelButton = _btnCancel;

            // Загрузка доступных портов
            LoadAvailablePorts();

            // Предвыбор сохраненного порта
            if (!string.IsNullOrEmpty(currentPort) && _cmbPorts.Items.Contains(currentPort))
            {
                _cmbPorts.SelectedItem = currentPort;
            }
        }

        private void LoadAvailablePorts()
        {
            _cmbPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();

            if (ports.Length > 0)
            {
                _cmbPorts.Items.AddRange(ports);
                _cmbPorts.SelectedIndex = 0;
                SelectedPort = ports[0];
                _btnOK.Enabled = true;
            }
            else
            {
                _cmbPorts.Items.Add("Порты не найдены");
                _cmbPorts.SelectedIndex = 0;
                _cmbPorts.Enabled = false;
                _btnOK.Enabled = false;
            }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            string currentSelection = _cmbPorts.SelectedItem?.ToString();
            LoadAvailablePorts();

            // Попытка восстановить выбор если порт всё ещё доступен
            if (!string.IsNullOrEmpty(currentSelection) && _cmbPorts.Items.Contains(currentSelection))
            {
                _cmbPorts.SelectedItem = currentSelection;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                if (_cmbPorts.SelectedItem != null && _cmbPorts.SelectedItem.ToString() != "Порты не найдены")
                {
                    SelectedPort = _cmbPorts.SelectedItem.ToString();
                }
                else
                {
                    MessageBox.Show("Пожалуйста, выберите действительный COM-порт.", 
                        "Ошибка конфигурации", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }
            }
            base.OnClosing(e);
        }
    }
}
