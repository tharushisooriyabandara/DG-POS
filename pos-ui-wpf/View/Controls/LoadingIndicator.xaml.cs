using System.Windows;
using System.Windows.Controls;

namespace POS_UI.View.Controls
{
    public partial class LoadingIndicator : UserControl
    {
        /*public static readonly DependencyProperty LoadingTextProperty =
            DependencyProperty.Register("LoadingText", typeof(string), typeof(LoadingIndicator), new PropertyMetadata("Loading..."));

        public static readonly DependencyProperty TextSizeProperty =
            DependencyProperty.Register("TextSize", typeof(double), typeof(LoadingIndicator), new PropertyMetadata(16.0));

        public string LoadingText
        {
            get => (string)GetValue(LoadingTextProperty);
            set => SetValue(LoadingTextProperty, value);
        }
         public double TextSize
        {
            get => (double)GetValue(TextSizeProperty);
            set => SetValue(TextSizeProperty, value);
        }*/

         public static readonly DependencyProperty ProgressRingSizeProperty =
            DependencyProperty.Register("ProgressRingSize", typeof(double), typeof(LoadingIndicator), new PropertyMetadata(50.0));

        public double ProgressRingSize
        {
            get => (double)GetValue(ProgressRingSizeProperty);
            set => SetValue(ProgressRingSizeProperty, value);
        }

        public LoadingIndicator()
        {
            InitializeComponent();
        }
    }
} 