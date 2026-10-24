using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TestRemDigital.Views;

namespace TestRemDigital.Views
{
    public partial class SplashPage : Page
    {
        private DispatcherTimer timer;

        public SplashPage()
        {
            InitializeComponent();

            // Inisialisasi timer 5 detik
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(5);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            timer.Stop();
            this.NavigationService.Navigate(new MainMenuPage());
        }
    }
}