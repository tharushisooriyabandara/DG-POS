using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for NewPinDialog.xaml
    /// </summary>
    public partial class NewPinDialog : UserControl
    {
        public NewPinDialog()
        {
            InitializeComponent();
        }

        private void PinBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.Text?.Length == 1)
                {
                    // Auto-advance to the next PIN TextBox in a known sequence
                    var next = GetNextPinTextBox(textBox);
                    if (next != null)
                    {
                        try { next.Focus(); next.SelectAll(); } catch { }
                    }
                }
            }
        }

        private void PinBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (e.Key == Key.Back)
                {
                    // Always consume Backspace to prevent page-level navigation
                    e.Handled = true;
                    if (!string.IsNullOrEmpty(textBox.Text))
                    {
                        textBox.Clear();
                    }
                    return;
                }
                else if (e.Key == Key.Left)
                {
                    MoveFocusToPrevious(textBox);
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    MoveFocusToNext(textBox);
                    e.Handled = true;
                }
            }
        }

        private void MoveFocusToNext(TextBox current)
        {
            try
            {
                current.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
            catch
            {
                // Ignore focus errors; keep caret where it is
            }
        }

        private void MoveFocusToPrevious(TextBox current)
        {
            try
            {
                current.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
            }
            catch
            {
                // Ignore focus errors; keep caret where it is
            }
        }

        private TextBox GetAdjacentTextBox(TextBox current, FocusNavigationDirection direction)
        {
            // Attempt to find next/previous focusable control and return if it's a TextBox
            current.MoveFocus(new TraversalRequest(direction));
            var element = Keyboard.FocusedElement as TextBox;
            return element;
        }

        private TextBox GetNextPinTextBox(TextBox current)
        {
            var sequence = GetPinSequence().ToList();
            var idx = sequence.IndexOf(current);
            if (idx >= 0 && idx + 1 < sequence.Count)
            {
                return sequence[idx + 1];
            }
            return null;
        }

        private IEnumerable<TextBox> GetPinSequence()
        {
            var boxes = new TextBox[] { NewPin1, NewPin2, NewPin3, NewPin4, NewPin5, NewPin6, ConfirmPin1, ConfirmPin2, ConfirmPin3, ConfirmPin4, ConfirmPin5, ConfirmPin6 };
            foreach (var b in boxes)
            {
                if (b != null && b.IsEnabled && b.Visibility == Visibility.Visible)
                {
                    yield return b;
                }
            }
        }

    }
}
