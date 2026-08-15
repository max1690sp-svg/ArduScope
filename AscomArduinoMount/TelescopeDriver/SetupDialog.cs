using System;
using System.Runtime.InteropServices;
using ASCOM;

namespace AscomArduinoMount.Telescope
{
    /// <summary>
    /// Класс настройки драйвера (Setup Dialog)
    /// </summary>
    [Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SetupDialogForm : ISetupDialogV2
    {
        private Telescope _telescope;

        public SetupDialogForm()
        {
            // Конструктор
        }

        public void Configure(IntPtr parentHandle)
        {
            // Здесь должно открываться диалоговое окно настройки
            // Для простоты реализации пока пусто
            // В полной версии здесь будет форма с настройками:
            // - Выбор COM порта
            // - Настройка скорости (Baud rate)
            // - Параметры монтировки (передаточные числа и т.д.)
        }

        public void Dispose()
        {
            // Очистка ресурсов
        }
    }
}
