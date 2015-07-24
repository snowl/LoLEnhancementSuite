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
        //Stores the original byte if the user enters invalid data
        public byte OriginalByte = 0;
        
        /// <summary>
        /// The byte currently stored in the ByteLabel
        /// </summary>
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
            //Reset the label if there is no data entered
            if (e.Key == Key.Enter)
            {
                if (ByteTextBox.Text == "")
                    ByteTextBox.Text = OriginalByte.ToString();
            }
        }

        private void ByteTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            //Ensure that only allowed data (0-255) is entered into the textbox)
            e.Handled = !IsTextAllowed(e.Text);
        }

        private static bool IsTextAllowed(string text)
        {
            //Test if the data is a number
            Regex regex = new Regex("[^0-9]+");
            return !regex.IsMatch(text);
        }

        private void ByteTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            //Reset the label if there is no data entered
            if (ByteTextBox.Text == "")
                ByteTextBox.Text = OriginalByte.ToString();
        }

        private void ByteTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //Don't do anything if the label is empty
            if (ByteTextBox.Text == "")
                return;

            //Clamp the data to between 0 and 255 (a byte)
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
