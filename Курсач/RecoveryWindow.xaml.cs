using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Курсач
{
    /// <summary>
    /// Логика взаимодействия для RecoveryWindow.xaml
    /// </summary>
    public partial class RecoveryWindow : Window
    {
        private readonly DatabaseManager db = new DatabaseManager();

        public RecoveryWindow()
        {
            InitializeComponent();
        }

        private void Recover_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLogin.Text))
            {
                MessageBox.Show("Введите логин/email/телефон!");
                return;
            }

            if (db.RequestPasswordReset(txtLogin.Text.Trim()))
            {
                MessageBox.Show("Временный пароль отправлен!");
                Close();
            }
            else
            {
                MessageBox.Show("Пользователь не найден!");
            }
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
    }
}

