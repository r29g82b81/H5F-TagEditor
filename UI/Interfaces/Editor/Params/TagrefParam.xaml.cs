using Infinite_module_test;
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
using static Infinite_module_test.tag_structs;
using static TagEditor.UI.Windows.TagExplorer;

namespace TagEditor.UI.Interfaces.Params{
    public partial class TagrefParam : UserControl {
        public string key;
        public TagrefParam(TagInstance _callback, int _param_type, int _line_index, string name, TagExplorer _tags_explorer, string _key) {
            callback = _callback;
            line_index = _line_index;
            param_type = _param_type;
            key = _key;
            tags_explorer = _tags_explorer;
            InitializeComponent();
            loadValue();
            Namebox.Text = name;
            is_setting_up = false;
        }
        public void reload(string _key) {
            key = _key;
            is_setting_up = true;
            loadValue();
            is_setting_up = false;
        }
        TagExplorer tags_explorer;
        string TagIDname = "";
        string TagName = "";
        string TagGroup = "";
        string TagAssetID = "";

        bool is_setting_up = true;
        private void loadValue() {
            byte[] val = callback.read_param_value(key);

            uint tagID = BitConverter.ToUInt32(val[0xC..0x10]);
            TagIDname = tagID.ToString("X8");
            TagName = tag_structs.get_shorttagname(tagID);
            TagIDBox.Content = TagName;
            ulong assetID = BitConverter.ToUInt64(val[0x10..0x18]);
            TagAssetID = assetID.ToString("X16");
            AssetIDBox.Content = TagAssetID;

            int value_test = BitConverter.ToInt32(val[0x18..0x1C]);
            if (value_test == -1) TagGroup = "NULL";
            else TagGroup = Reverse(System.Text.Encoding.Default.GetString(val[0x18..0x1C]));
            GroupBox.Content = TagGroup;

            if (og_value == null) og_value = TagGroup + "," + TagIDname + "," + TagAssetID;
        }
        public static string Reverse(string s) {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        private void call_open_tag(object sender, RoutedEventArgs e)
        {
            tags_explorer.try_open_tag(TagName, TagIDname);
        }




        string[]? selectable_groups = null;
        private void GroupBox_Click(object sender, RoutedEventArgs e) {
            if (groups_popup.IsOpen == true){
                groups_popup.IsOpen = false;
                e.Handled = true;
                return; // we should close the dropdown if its already open
            }
            e.Handled = true;
            groups_popup.IsOpen = true;


            // iterate through all tag groups from all active modules
            Dictionary<string, bool> groups = new();

            // we now need to populate the list thing
            foreach (var module in tags_explorer.main.Active_ModuleViewer.active_modules)
                foreach (var group in module.file_groups)
                    groups[group.Key] = true;

            selectable_groups = new string[groups.Count];
            for (int i = 0; i < groups.Count; i++) selectable_groups[i] = groups.ElementAt(i).Key;


            groups_list.SelectedIndex = -1;
            groups_list.ItemsSource = selectable_groups;

            groups_list.Focus();
            Keyboard.Focus(groups_list);

        }

        private void groups_list_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (groups_list.SelectedIndex == -1) return; // this fires when removing selection
            TagGroup = selectable_groups[groups_list.SelectedIndex];
            selectable_groups = null; // clear
            groups_list.ItemsSource = null;
            groups_popup.IsOpen = false;
            SaveValue();
        }

        private void SaveValue() {
            if (is_setting_up) return;


            string new_value = TagGroup + "," + TagIDname + "," + TagAssetID;
            SetValue(this, TagGroup, TagIDname, TagAssetID, callback, key);
            callback.set_diff(this, key, Namebox.Text, param_type, og_value, new_value, line_index);
        }
    

        private static void SetValue(TagrefParam? target, string group, string tagid, string assetid, TagInstance callback, string key){

            byte[] buffer = new byte[0x18];


            // pack the updated values into mem
            uint tagID = Convert.ToUInt32(tagid, 16); 
            ulong assetID = Convert.ToUInt64(assetid, 16); 
            BitConverter.GetBytes(tagID).CopyTo(buffer, 0xC);
            BitConverter.GetBytes(assetID).CopyTo(buffer, 0x10);

            if (group == "NULL") BitConverter.GetBytes(-1).CopyTo(buffer, 0x18);
            else Encoding.UTF8.GetBytes(Reverse(group)).CopyTo(buffer, 0x18);

            callback.write_param_value(key, buffer);

            // update UI element if it exists
            if (target != null){
                target.is_setting_up = true;
                // update the visual values?
                target.loadValue();
                target.is_setting_up = false;
            }
        }
        // diff'ing stuff
        TagInstance callback;
        int line_index;
        int param_type;
        string? og_value; // note that this will not always be the actual OG value
        public static void revert_value(string old_value, TagrefParam? target, TagInstance callback, string key){

            string[] old_values = old_value.Split(",");
            string target_group = old_values[0];
            string target_tagid = old_values[1];
            string target_assetid = old_values[2];

            if (target != null){
                target.og_value = old_value;
                SetValue(target, target_group, target_tagid, target_assetid, callback, key);
            }else SetValue(null, target_group, target_tagid, target_assetid, callback, key);
        }

        // tags list junk

        List<module_structs.module.indexed_module_file>? selectable_tags = null;
        string[]? selectable_tag_names = null; 
        private void TagIDBox_Click(object sender, RoutedEventArgs e){
            e.Handled = true;
            if (tag_popup.IsOpen == true){
                tag_popup.IsOpen = false;
                return; // we should close the dropdown if its already open
            }
            tag_popup.IsOpen = true;


            // iterate through all tag groups from all active modules
            selectable_tags = new();

            // we now need to populate the list thing
            foreach (var module in tags_explorer.main.Active_ModuleViewer.active_modules)
                if (module.file_groups.ContainsKey(TagGroup))
                    foreach (var tag in module.file_groups[TagGroup])
                        if (tag.is_resource == false /*&& tag.manifest_index == -1*/)
                            selectable_tags.Add(tag);

            selectable_tag_names = new string[selectable_tags.Count];
            for (int i = 0; i < selectable_tags.Count; i++) selectable_tag_names[i] = selectable_tags[i].alias;


            tags_list.SelectedIndex = -1;
            tags_list.ItemsSource = selectable_tag_names;

            tags_list.Focus();
            Keyboard.Focus(tags_list);
        }

        private void tags_list_SelectionChanged(object sender, SelectionChangedEventArgs e){
            if (tags_list.SelectedIndex == -1) return; // this fires when removing selection
            var selected_tag = selectable_tags[tags_list.SelectedIndex];
            TagIDname = selected_tag.name;
            TagAssetID = selected_tag.file.header.AssetId.ToString("X16");



            selectable_tag_names = null; // clear
            selectable_tags = null; // clear
            tags_list.ItemsSource = null;
            tag_popup.IsOpen = false;
            SaveValue();
        }
    }
}
