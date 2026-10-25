using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Курсач
{
    public partial class SupportRequestWindow : Window
    {
        public List<string> Categories { get; } = new List<string>
        {
            "Технические проблемы",
            "Ошибка в контенте",
            "Вопрос по аккаунту",
            "Предложения"
        };

        public string RequestText => txtRequest.Text;
        public string SelectedCategory => cmbCategory.SelectedItem as string;

        public SupportRequestWindow()
        {
            InitializeComponent();
            cmbCategory.ItemsSource = Categories;
            cmbCategory.SelectedIndex = 0;
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtRequest.Text))
            {
                MessageBox.Show("Введите текст запроса!");
                return;
            }
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