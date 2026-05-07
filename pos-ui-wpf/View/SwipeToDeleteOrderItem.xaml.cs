using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace POS_UI.View
{
    public partial class SwipeToDeleteOrderItem : UserControl
    {
        public static readonly DependencyProperty RemoveCommandProperty = DependencyProperty.Register(
            nameof(RemoveCommand), typeof(ICommand), typeof(SwipeToDeleteOrderItem), new PropertyMetadata(null));

        public ICommand RemoveCommand
        {
            get => (ICommand)GetValue(RemoveCommandProperty);
            set => SetValue(RemoveCommandProperty, value);
        }

        public static readonly DependencyProperty EditCommandProperty = DependencyProperty.Register(
            nameof(EditCommand), typeof(ICommand), typeof(SwipeToDeleteOrderItem), new PropertyMetadata(null));

        public ICommand EditCommand
        {
            get => (ICommand)GetValue(EditCommandProperty);
            set => SetValue(EditCommandProperty, value);
        }

        private Point _startPoint;
        private bool _isPointerDown;
        private bool _isScrolling;
        private bool _isSwiping;
        private ScrollViewer _parentScrollViewer;
        private double _scrollStartOffset;
        private const double _directionThreshold = 12;
        private const double _dragThreshold = 160;
        private const double _maxDrag = 180;

        public SwipeToDeleteOrderItem()
        {
            InitializeComponent();
            ContentBorder.MouseLeftButtonDown += ContentBorder_MouseLeftButtonDown;
            ContentBorder.MouseMove += ContentBorder_MouseMove;
            ContentBorder.MouseLeftButtonUp += ContentBorder_MouseLeftButtonUp;
            ContentBorder.MouseLeave += ContentBorder_MouseLeave;
        }

        private ScrollViewer FindParentScrollViewer()
        {
            DependencyObject parent = VisualTreeHelper.GetParent(this);
            while (parent != null)
            {
                if (parent is ScrollViewer sv) return sv;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void ContentBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ContentTransform.BeginAnimation(TranslateTransform.XProperty, null);
            ContentTransform.X = 0;
            DeleteFeedback.Visibility = Visibility.Collapsed;

            _isPointerDown = true;
            _isScrolling = false;
            _isSwiping = false;

            if (_parentScrollViewer == null)
                _parentScrollViewer = FindParentScrollViewer();
            _scrollStartOffset = _parentScrollViewer?.VerticalOffset ?? 0;

            // Use the ScrollViewer as reference so positions stay stable while scrolling
            UIElement stableRef = (UIElement)_parentScrollViewer ?? this;
            _startPoint = e.GetPosition(stableRef);

            ContentBorder.CaptureMouse();
        }

        private void ContentBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPointerDown) return;

            UIElement stableRef = (UIElement)_parentScrollViewer ?? this;
            var pos = e.GetPosition(stableRef);
            var dx = pos.X - _startPoint.X;
            var dy = pos.Y - _startPoint.Y;

            if (!_isScrolling && !_isSwiping)
            {
                if (Math.Abs(dy) > _directionThreshold && Math.Abs(dy) > Math.Abs(dx))
                    _isScrolling = true;
                else if (Math.Abs(dx) > _directionThreshold && Math.Abs(dx) > Math.Abs(dy))
                    _isSwiping = true;
                else
                    return;
            }

            if (_isScrolling && _parentScrollViewer != null)
            {
                _parentScrollViewer.ScrollToVerticalOffset(_scrollStartOffset - dy);
            }
            else if (_isSwiping && dx < 0)
            {
                double drag = Math.Max(dx, -_maxDrag);
                ContentTransform.X = drag;
                DeleteFeedback.Visibility = Visibility.Visible;
                if (Math.Abs(drag) > _dragThreshold)
                {
                    DeleteOrderItem();
                    _isPointerDown = false;
                    _isSwiping = false;
                    ContentBorder.ReleaseMouseCapture();
                    DeleteFeedback.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ContentBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPointerDown) return;

            ContentBorder.ReleaseMouseCapture();
            _isPointerDown = false;

            if (_isSwiping)
            {
                AnimateContent(0);
                DeleteFeedback.Visibility = Visibility.Collapsed;
            }
            else if (!_isScrolling)
            {
                if (e.OriginalSource is DependencyObject depObj && FindParent<Button>(depObj) != null)
                {
                    _isScrolling = false;
                    _isSwiping = false;
                    return;
                }

                var orderItem = DataContext as POS_UI.Models.OrderItem;
                if (orderItem != null && !orderItem.IsReadOnly
                    && EditCommand != null && EditCommand.CanExecute(orderItem))
                {
                    EditCommand.Execute(orderItem);
                }
            }

            _isScrolling = false;
            _isSwiping = false;
        }

        private void ContentBorder_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPointerDown && !_isScrolling)
            {
                _isPointerDown = false;
                _isSwiping = false;
                ContentBorder.ReleaseMouseCapture();
                AnimateContent(0);
                DeleteFeedback.Visibility = Visibility.Collapsed;
            }
        }

        private void AnimateContent(double toX)
        {
            var anim = new DoubleAnimation
            {
                To = toX,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ContentTransform.BeginAnimation(TranslateTransform.XProperty, anim);
        }

        private void DeleteOrderItem()
        {
            if (RemoveCommand != null && RemoveCommand.CanExecute(DataContext))
            {
                RemoveCommand.Execute(DataContext);
            }
            AnimateContent(-ActualWidth);
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
