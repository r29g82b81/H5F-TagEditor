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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace TagEditor.UI.Interfaces.Params{
    /// <summary>
    /// Interaction logic for TriplefloatParam.xaml
    /// </summary>
    public partial class TriplefloatParam : UserControl{
        public string key;
        public TriplefloatParam(TagInstance _callback, int _param_type, int _line_index, string name, string _key){
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
        // FLOAT STUFF //
        // TODO //
        private void loadValue(){
            var v = callback.read_param_value(key);
            Valuebox1.Text = BitConverter.ToSingle(v[0..4]).ToString();
            Valuebox2.Text = BitConverter.ToSingle(v[4..8]).ToString();
            Valuebox3.Text = BitConverter.ToSingle(v[8..12]).ToString();
            if (og_value == null) og_value = Valuebox1.Text + ", " + Valuebox2.Text + ", " + Valuebox3.Text;
        }

        private void Button1_SaveValue(object sender, TextChangedEventArgs e) => Button_SaveValue();
        private void Button2_SaveValue(object sender, TextChangedEventArgs e) => Button_SaveValue();
        private void Button3_SaveValue(object sender, TextChangedEventArgs e) => Button_SaveValue();


        private void Button_SaveValue(){
            if (is_setting_up) return;
            float value1;
            float value2;
            float value3;
            try{value1 = Convert.ToSingle(Valuebox1.Text);
            }catch { error_marker1.Visibility = Visibility.Visible; return; }
            try{value2 = Convert.ToSingle(Valuebox2.Text);
            }catch { error_marker2.Visibility = Visibility.Visible; return; }
            try{value3 = Convert.ToSingle(Valuebox3.Text);
            }catch { error_marker3.Visibility = Visibility.Visible; return; }
            // we can only set values & submit the diff if both values passed
            SetValue(this, [Valuebox1, Valuebox2, Valuebox3], [error_marker1, error_marker2, error_marker3], [value1, value2, value3], callback, key);
            callback.set_diff(this, key, Namebox.Text, param_type, og_value, value1.ToString() + ", " + value2.ToString() + ", " + value3.ToString(), line_index);
        }
        private static void SetValue(TriplefloatParam? target, TextBox[]? source, Separator[]? error, float[] value, TagInstance callback, string key){
            // update UI element if it exists
            if (target != null){
                target.is_setting_up = true;
                source[0].Text = value[0].ToString();
                source[1].Text = value[1].ToString();
                source[2].Text = value[2].ToString();
                if (error[0].Visibility != Visibility.Collapsed) error[0].Visibility = Visibility.Collapsed;
                if (error[1].Visibility != Visibility.Collapsed) error[1].Visibility = Visibility.Collapsed;
                if (error[2].Visibility != Visibility.Collapsed) error[2].Visibility = Visibility.Collapsed;
                target.is_setting_up = false;
            }
            byte[] result = new byte[value.Length * 4];
            Buffer.BlockCopy(value, 0, result, 0, result.Length);
            callback.write_param_value(key, result);
        }
        // diff'ing stuff
        TagInstance callback;
        int line_index;
        int param_type;
        string? og_value; // note that this will not always be the actual OG value
        public static void revert_value(string old_value, TriplefloatParam? target, TagInstance callback, string key){
            string[] values = old_value.Split(", ");
            if (target != null) {
                target.og_value = old_value;
                SetValue(target, [target.Valuebox1, target.Valuebox2, target.Valuebox3], [target.error_marker1, target.error_marker2, target.error_marker3], [Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2])], callback, key);
            }else {
                SetValue(null, null, null, [Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2])], callback, key);
            }
        }
    }
}