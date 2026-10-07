using System.Windows;
using Курсач.Security;

namespace Курсач
{
    public partial class SecureInputWindow : Window
    {
        public SecureInputWindow()
        {
            InitializeComponent();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string conn = txtConn.Text.Trim();
            string api = txtApi.Password.Trim();

            if (string.IsNullOrEmpty(conn) || string.IsNullOrEmpty(api))
            {
                MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SecureDataManager.SaveData(conn, api);
            MessageBox.Show("Данные сохранены и зашифрованы.", "Успех");
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
