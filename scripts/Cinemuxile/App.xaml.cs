using System.Windows;
using Курсач.Security;

namespace Курсач
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1️⃣ Пробуем загрузить защищённые данные
                var data = SecureDataManager.GetData();

                // 2️⃣ Если данных нет — открываем окно ввода
                if (data == null)
                {
                    var win = new SecureInputWindow();
                    bool? result = win.ShowDialog();

                    if (result != true)
                    {
                        MessageBox.Show(
                            "Приложение не может работать без настроек.\nРабота будет завершена.",
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );

                        // Завершение — но без дальнейшего кода
                        Current.Shutdown(0);
                        return;
                    }

                    // Загружаем данные повторно
                    data = SecureDataManager.GetData();
                    if (data == null)
                    {
                        MessageBox.Show(
                            "Ошибка загрузки конфиденциальных данных. Возможно, файл повреждён.",
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );

                        Current.Shutdown(0);
                        return;
                    }
                }

                // 3️⃣ Теперь данные гарантированно есть
                DatabaseManager.ConnectionString = data.ConnectionString;
                DatabaseManager.ApiKey = data.ApiKey;

            }
            catch (System.Exception ex)
            {
                // На случай любой непредвиденной ошибки
                MessageBox.Show(
                    "Не удалось запустить приложение:\n" + ex.Message,
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                // Безопасное завершение
                try { Current?.Shutdown(1); } catch { }
            }
        }
    }
}
