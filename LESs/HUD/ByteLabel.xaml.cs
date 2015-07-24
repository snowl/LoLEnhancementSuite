using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LESs
{
    /// <summary>
    /// Interaction logic for ByteLabel.xaml
    /// </summary>
    public partial class ByteLabel : UserControl
    {
        public byte OriginalByte = 0;
        public byte Byte
        {
            get { return byte.Parse(ByteTextBox.Text); }
            set { ByteTextBox.Text = value.ToString(); }
        }

        public ByteLabel()
        {
            InitializeComponent();
        }

        private void ByteTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ByteTextBox.Text == "")
                    ByteTextBox.Text = OriginalByte.ToString();
            }
        }

        private void ByteTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private static bool IsTextAllowed(string text)
        {
            Regex regex = new Regex("[^0-9]+");
            return !regex.IsMatch(text);
        }

        private void ByteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (ByteTextBox.Text == "")
                ByteTextBox.Text = OriginalByte.ToString();
        }

        private void ByteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ByteTextBox.Text == "")
                return;

            int parse = int.Parse(ByteTextBox.Text);
            string PreClamp = Clamp<int>(parse, 0, 255).ToString();
            if (PreClamp != ByteTextBox.Text)
            {
                ByteTextBox.Text = PreClamp;
                ByteTextBox.CaretIndex = ByteTextBox.Text.Length;
            }
        }

        public static T Clamp<T>(T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }
    }
}
