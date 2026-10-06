using System;
using System.Windows;
using System.Windows.Controls;
using TagEditor.UI.Windows;

namespace TagEditor.UI.Interfaces.Params{
    /// <summary>
    /// Interaction logic for AngleParam.xaml
    /// </summary>
    public partial class AngleParam : UserControl{
        public string key;
        public AngleParam(TagInstance _callback, int _param_type, int _line_index, string name, string _key){
            key = _key;
            callback = _callback;
            line_index = _line_index;
            param_type = _param_type;
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
        // ANGLE STUFF // 
        // there is no angle stuff //
        private void loadValue(){
            double radians = BitConverter.ToSingle(callback.read_param_value(key));
            radians *= 180 / Math.PI; // convert to degrees
            Valuebox.Text = ((float)radians).ToString();
            // store og value
            if (og_value == null) og_value = Valuebox.Text;
        }

        private void Button_SaveValue(object sender, TextChangedEventArgs e){
            if (is_setting_up) return;
            try{SetValue(this, Valuebox, error_marker, Convert.ToDouble(Valuebox.Text), callback, key);
                callback.set_diff(this, key, Namebox.Text, param_type, og_value, Valuebox.Text, line_index);
            }catch { 
                error_marker.Visibility = Visibility.Visible;
        }}
        private static void SetValue(AngleParam? target, TextBox? source, Separator? error, double value, TagInstance callback, string key){
            // update UI element if it exists
            if (target != null){
                target.is_setting_up = true;
                source.Text = ((float)value).ToString();
                if (error.Visibility != Visibility.Collapsed) error.Visibility = Visibility.Collapsed;
                target.is_setting_up = false;
            }
            value *= Math.PI / 180;
            byte[] bytes = BitConverter.GetBytes((float)value);
            callback.write_param_value(key, bytes);
        }
        // diff'ing stuff
        TagInstance callback;
        int line_index;
        int param_type;
        string? og_value; // note that this will not always be the actual OG value
        public static void revert_value(string old_value, AngleParam? target, TagInstance callback, string key){
            if (target != null){
                target.og_value = old_value;
                SetValue(target, target.Valuebox, target.error_marker, Convert.ToDouble(old_value), callback, key);
            }else SetValue(null, null, null, Convert.ToDouble(old_value), callback, key);
        }
    }
}
