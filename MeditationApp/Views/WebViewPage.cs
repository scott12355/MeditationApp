using Microsoft.Maui.Controls;

namespace MeditationApp.Views
{
    public partial class WebViewPage : ContentPage
    {
        public WebViewPage(string url, string title)
        {
            InitializeComponent();
            Title = title;
            webView.Source = url;
        }
    }
}
