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
using TagEditor.UI.Windows;

namespace TagEditor.UI.Interfaces.Params{
    /// <summary>
    /// Interaction logic for IntegerParam.xaml
    /// </summary>
    public partial class IntegerParam : UserControl{
        public string key;
        public IntegerParam(TagInstance _callback, int _param_type, int _line_index, string name, IntType _int_type, string _key){
            key = _key;
            callback = _callback;
            line_index = _line_index;
            param_type = _param_type;
            int_type = _int_type;
            InitializeComponent();
            loadValue();
            Namebox.Text = name;
            is_setting_up = false;
        }
        public void reload(string _key){
            key = _key;
            is_setting_up = true;
            loadValue();
            is_setting_up = false;
        }
        bool is_setting_up = true;
        // integer stuff //
        IntType int_type;
        public enum IntType{
            signed_char    = 0,
            signed_short   = 1,
            signed_int     = 2,
            signed_long    = 3,
            unsigned_char  = 4,
            unsigned_short = 5,
            unsigned_int   = 6,
            unsigned_long  = 7
        } // check 0b0100 for signed, then subtract it

        private void loadValue(){
            switch ((sbyte)int_type){ // if we get errors about "too large or too small" then we aren't actually converting the values right
                case 0: // byte
                    sbyte value0 = (sbyte)callback.read_param_value(key)[0];
                    Valuebox.Text = value0.ToString();
                    break;
                case 1: // short
                    short value1 = BitConverter.ToInt16(callback.read_param_value(key));
                    Valuebox.Text = value1.ToString();
                    break;
                case 2: // int
                    int value2 = BitConverter.ToInt32(callback.read_param_value(key));
                    Valuebox.Text = value2.ToString();
                    break;
                case 3: // long
                    long value3 = BitConverter.ToInt64(callback.read_param_value(key));
                    Valuebox.Text = value3.ToString();
                    break;
                case 4: // ubyte
                    byte value4 = callback.read_param_value(key)[0];
                    Valuebox.Text = value4.ToString();
                    break;
                case 5: // ushort
                    ushort value5 = BitConverter.ToUInt16(callback.read_param_value(key));
                    Valuebox.Text = value5.ToString();
                    break;
                case 6: // uint
                    uint value6 = BitConverter.ToUInt32(callback.read_param_value(key));
                    Valuebox.Text = value6.ToString();
                    break;
                case 7: // ulong
                    long value7 = BitConverter.ToInt64(callback.read_param_value(key));
                    Valuebox.Text = value7.ToString();
                    break;
            }
            if (og_value == null) og_value = Valuebox.Text;
        }

        private void Button_SaveValue(object sender, TextChangedEventArgs e){
            if (is_setting_up) return;
            // we going to call the exception inside the set value function this time
            try{SetValue(this, Valuebox, error_marker, Valuebox.Text, int_type, callback, key);
                callback.set_diff(this, key, Namebox.Text, param_type, og_value, Valuebox.Text, line_index);
            }catch { 
                error_marker.Visibility = Visibility.Visible;
        }}
        private static void SetValue(IntegerParam? target, TextBox? source, Separator? error, string value, IntType int_type, TagInstance callback, string key){
            byte[] results;
            switch ((byte)int_type){
                case 0: // byte
                    sbyte value0 = Convert.ToSByte(value);
                    results = new byte[1] { (byte)value0 }; // Convert.ToByte(value0);// what is this??
                    break;
                case 1: // short
                    short value6 = Convert.ToInt16(value);
                    results = BitConverter.GetBytes(value6);
                    break;
                case 2: // int
                    int value1 = Convert.ToInt32(value);
                    results = BitConverter.GetBytes(value1);
                    break;
                case 3: // long
                    long value2 = Convert.ToInt64(value);
                    results = BitConverter.GetBytes(value2);
                    break;
                case 4: // ubyte
                    byte value7 = Convert.ToByte(value);
                    results = new byte[1] { value7 };
                    break;
                case 5: // ushort
                    ushort value3 = Convert.ToUInt16(value);
                    results = BitConverter.GetBytes(value3);
                    break;
                case 6: // uint
                    uint value4 = Convert.ToUInt32(value);
                    results = BitConverter.GetBytes(value4);
                    break;
                case 7: // ulong
                    ulong value5 = Convert.ToUInt64(value);
                    results = BitConverter.GetBytes(value5);
                    break;
                default: throw new Exception("invalid int type?");
            }
            // assuming we didn't cause an exception, then we can proceed
            if (target != null){
                target.is_setting_up = true;
                source.Text = value;
                if (error.Visibility != Visibility.Collapsed) error.Visibility = Visibility.Collapsed;
                target.is_setting_up = false;
            }
            callback.write_param_value(key, results);
        }
        // diff'ing stuff
        TagInstance callback;
        int line_index;
        int param_type;
        string? og_value; // note that this will not always be the actual OG value
        public static void revert_value(string old_value, IntType int_type, IntegerParam? target, TagInstance callback, string key){
            if (target != null){
                target.og_value = old_value;
                SetValue(target, target.Valuebox, target.error_marker, old_value, int_type, callback, key);
            }else SetValue(null, null, null, old_value, int_type, callback, key);
        }
    }
}
