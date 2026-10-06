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
    public partial class FloatParam : UserControl{
        public string key;
        public FloatParam(TagInstance _callback, int _param_type, int _line_index, string name, bool _is_fraction, string _key){
            key = _key;
            callback = _callback;
            line_index = _line_index;
            param_type = _param_type;
            InitializeComponent();
            loadValue();
            Namebox.Text = name;
            is_setting_up = false;
            is_fraction = _is_fraction;
        }
        public void reload(string _key){
            key = _key;
            is_setting_up = true;
            loadValue();
            is_setting_up = false;
        }
        bool is_setting_up = true;
        // FLOAT STUFF //
        bool is_fraction = false;
        private void loadValue(){
            Valuebox.Text = BitConverter.ToSingle(callback.read_param_value(key)).ToString();
            if (og_value == null) og_value = Valuebox.Text;
        }

        private void Button_SaveValue(object sender, TextChangedEventArgs e){
            if (is_setting_up) return;
            try{float value = Convert.ToSingle(Valuebox.Text);
                if (is_fraction && (!(value >= 0.0f && value <= 1.0f))) throw new Exception(); // factions are between 0 & 1
                SetValue(this, Valuebox, error_marker, value, callback, key);
                callback.set_diff(this, key, Namebox.Text, param_type, og_value, Valuebox.Text, line_index);
            }catch { 
                error_marker.Visibility = Visibility.Visible;
        }}
        private static void SetValue(FloatParam? target, TextBox? source, Separator? error, float value, TagInstance callback, string key){
            // update UI element if it exists
            if (target != null){
                target.is_setting_up = true;
                source.Text = ((float)value).ToString();
                if (error.Visibility != Visibility.Collapsed) error.Visibility = Visibility.Collapsed;
                target.is_setting_up = false;
            }
            byte[] bytes = BitConverter.GetBytes(value);
            callback.write_param_value(key, bytes);
        }
        // diff'ing stuff
        TagInstance callback;
        int line_index;
        int param_type;
        string? og_value; // note that this will not always be the actual OG value
        public static void revert_value(string old_value, FloatParam? target, TagInstance callback, string key){
            if (target != null){
                target.og_value = old_value;
                SetValue(target, target.Valuebox, target.error_marker, Convert.ToSingle(old_value), callback, key);
            }else SetValue(null, null, null, Convert.ToSingle(old_value), callback, key);
        }
    }
}
