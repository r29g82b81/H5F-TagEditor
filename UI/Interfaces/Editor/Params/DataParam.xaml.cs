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
using System.Xml.Linq;
using TagEditor.UI.Windows;

namespace TagEditor.UI.Interfaces.Editor.Params
{
    /// <summary>
    /// Interaction logic for DataParam.xaml
    /// </summary>
    public partial class DataParam : UserControl
    {
        public string key;
        TagInstance callback;
        public DataParam(string name, byte[] _datter, short _length, string _key, TagInstance _callback)
        {
            key = _key;
            callback = _callback;
            InitializeComponent();
            load(name, _datter, _length);
        }
        public void reload(string name, byte[] _datter, short _length, string _key){
            key = _key;
            load(name, _datter, _length);
        }
        byte[] data;
        public void load(string name, byte[] _datter, short _length)
        {

            Namebox.Text = name + " [" + _datter.Length + "]";
            data = _datter;
            byte[] junk = callback.read_param_value(key);
            Valuebox.Text = BitConverter.ToString(junk[0..(_length)]).Replace("-", string.Empty);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string dat_as_hex = BitConverter.ToString(data).Replace('-', ' ');
            Clipboard.SetText(dat_as_hex);
        }
    }
}