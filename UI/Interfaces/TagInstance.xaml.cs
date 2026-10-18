using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata;
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
using System.Xml;
using TagEditor.UI.Interfaces.Params;
using TagEditor.UI.Interfaces.Editor;
using TagFramework.Src.TagFiles;
using TagFramework.Src.ModuleFiles;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using System.Collections.ObjectModel;
using TagEditor.UI.Interfaces.Editor.Params;
using static TagEditor.MainWindow;
using static TagEditor.UI.Windows.TagInstance;
using Microsoft.Win32;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;

namespace TagEditor.UI.Windows{
    public partial class TagInstance : UserControl{
        public TagInstance(MainWindow _main, Module? _parent, Tag _loaded_tag, TabItem _container, string tabitem_name, byte[] _raw_tagbytes)
        {
            InitializeComponent();
            main = _main;
            parent = _parent;
            loaded_tag = _loaded_tag;
            container = _container;
            container.Content = this;

            container.Header = tabitem_name;
            tabname = tabitem_name;

            indexes_panel.ItemsSource = line_indexes;
            types_panel.ItemsSource = line_groups;
            raw_tagbytes = _raw_tagbytes;
        }
        MainWindow main;
        public TabItem container;
        string tabname;
        Tag loaded_tag;
        byte[] raw_tagbytes;
        bool tag_is_modified = false;
        bool tag_has_been_committed = false; // use this so we dont remove the edited status when we revert changes after committing
        public void commit_changes(){
            if (module_file_header == null || parent == null) {
                main.DisplayNote("Tag does not need to be committed, try exporting instead", null, error_level.NOTE);
                return;}
            if (tag_is_modified == false){
                main.DisplayNote("Tag has not been modified and does not need to be committed", null, error_level.NOTE);
                return;}

            // mark as not edited because we're basically saving this tag
            tag_is_modified = false;
            container.Header = tabname;


            var v = loaded_tag.compile();
            ModuleCompiler packer = new(parent);
            packer.pack_tag(v.tag_bytes, v.resource_bytes, module_file_header.header.GlobalTagId, module_file_header.header.AssetId, (uint?)module_file_header.header.ClassId, true);

            tag_has_been_committed = true;
            module_file_header.has_been_edited = true;
            main.TagViewer_UpdateModulesStats();
        }

        public Module.unpacked_module_file? module_file_header;
        Module? parent;
        expand_link root_expand;
        public void LoadTag_UI(){
            if (loaded_tag.root.blocks.Count != 1)
            {

            }
            current_line_index = 0; // related to below line
            root_expand = recurs_map_expands(loaded_tag.root.GUID);


            XmlNode? root_struct_node = loaded_tag.reference_root.SelectSingleNode('_' + loaded_tag.root.GUID);
            string? param_name = root_struct_node.Attributes?["Name"]?.Value;

            StructParam param = new(param_name, loaded_tag.root.blocks[0], 0, loaded_tag.root.GUID, root_expand, "__root");
            params_panel.Children.Add(param);
            line_indexes.Add("0");
            line_groups.Add("root");
            setup_struct_element(root_expand, param, 0);
        }
        int current_line_index;
        expand_link recurs_map_expands(string struct_guid){
            XmlNode? guid_test = loaded_tag.reference_root.SelectSingleNode('_' + struct_guid);
            if (guid_test == null) Debug.Assert(false, "failed to locate guid xml node?");
            expand_link output = new(this);
            output.caculated_line_index = current_line_index;
            output.child_line_count = guid_test.ChildNodes.Count; // we could realistically just use this value, and not have to store it
            output.total_contained_lines = output.child_line_count;

            for (int i = 0; i < guid_test.ChildNodes.Count; i++){
                current_line_index++; 
                XmlNode node = guid_test.ChildNodes[i];
                int type = Convert.ToInt32(node.Name.Substring(1), 16);
                switch (type){
                    case 0x38:
                    case 0x39:
                    case 0x40:
                    case 0x43:
                        string next_guid = node.Attributes?["GUID"]?.Value;
                        expand_link new_child = recurs_map_expands(next_guid);
                        output.total_contained_lines += new_child.total_contained_lines;
                        output.child_links.Add(i, new_child);
                        break;
            }}
            return output;
        }
        public class expand_link{
            public expand_link(TagInstance parent){
                send_info_to = parent;
            }
            public string unique_id;
            // only needs to be assigned for items that are automatically opened
            public int literal_line_index; // needs to be updated when any expand_link is either opened or closed

            public int caculated_line_index;
            public int child_line_count;
            public int total_contained_lines;
            // offset, child expander
            public Dictionary<int, expand_link> child_links = new();
            public TagInstance send_info_to; // needs to be given on construction
            // these will be assigned during tab opening and whatever (AKA RUNTIME)
            public BlockExpand expand_button; // needs to be assigned when the containing element is opened
            public UserControl struct_UI_element;

            public bool is_opened = false;
            public bool is_on_screen = false;
            public void wipe_child(){
                // we may beable to use this section to unhook diffs??

                foreach (var v in child_links)
                { // cleanup expand buttons of the child elements that we're about to destroy
                    if (v.Value.is_opened) v.Value.expand(false);
                    v.Value.literal_line_index = 0;
                    send_info_to.expanders_panel.Children.Remove(v.Value.expand_button);
                    v.Value.expand_button = null;
                    v.Value.struct_UI_element = null;
                    v.Value.is_on_screen = false;
                }
            }
            public void unhook_children(int previous_selected_index = -1){
                // call recursion on contained opened structs
                foreach (var v in child_links){ // iterate through all links & unhook
                    if (v.Value.is_opened) 
                        v.Value.unhook_children();
                }

                StackPanel? sp = null;
                string key = "";
                if (struct_UI_element is StructParam){
                    StructParam? test = struct_UI_element as StructParam;
                    sp = test.params_panel;
                    key = test.key + "\\\\";
                }else if (struct_UI_element is ArrayParam){
                    ArrayParam? test = struct_UI_element as ArrayParam;
                    sp = test.params_panel;
                    // if previous key was specified, then we use that
                    if (previous_selected_index == -1) key = test.key + "\\" + test.selected_index + "\\";
                    else key = test.key + "\\" + previous_selected_index + "\\";
                }else if (struct_UI_element is TagblockParam){
                    TagblockParam? test = struct_UI_element as TagblockParam;
                    sp = test.params_panel;
                    // if previous key was specified, then we use that
                    if (previous_selected_index == -1) key = test.key + "\\" + test.selected_index + "\\";
                    else key = test.key + "\\" + previous_selected_index + "\\";
                }
                else if (struct_UI_element is ResourceParam){
                    ResourceParam? test = struct_UI_element as ResourceParam;
                    sp = test.params_panel;
                    key = test.key + "\\\\";
                }
                if (sp == null) throw new Exception("this shouldn't happen! theres only 4 struct types");

                UIElement? bobject = null;
                for (int i = 0; i < sp.Children.Count; i++){
                    UIElement param_ui = sp.Children[i];
                    string param_key = key + "." + i;
                    send_info_to.try_unhook_diff(param_key);
                }
            }
            public void expand(bool reload, int old_selected_index = -1){ // this function is directly called by the expand button control, opposed to the struct UI element, thats why this even exists, so those two can be entirely unrelated
                if (!is_on_screen) Debug.Assert(false, "bad. this element is not visible and is attempting to expand");

                // if (is_opened && reload) expand(false); // to close it so we can reopne it
                // we need to figure out what our struct type is
                // struct
                // array 
                // tagblock
                // resource block

                // CLOSE OPENED TAG
                if (is_opened && !reload){ // if open & not reloading
                    unhook_children();
                    wipe_child();
                    if (struct_UI_element is StructParam){
                        StructParam test = struct_UI_element as StructParam;
                        test.params_panel.Children.Clear();
                        test.expand_indicator.Visibility = Visibility.Visible;

                    }else if (struct_UI_element is ArrayParam){
                        ArrayParam test = struct_UI_element as ArrayParam;
                        test.params_panel.Children.Clear();
                        test.expand_indicator.Visibility = Visibility.Visible;

                    }else if (struct_UI_element is TagblockParam){
                        TagblockParam test = struct_UI_element as TagblockParam;
                        test.params_panel.Children.Clear();
                        test.expand_indicator.Visibility = Visibility.Visible;

                    }else if(struct_UI_element is ResourceParam){
                        ResourceParam test = struct_UI_element as ResourceParam;
                        test.params_panel.Children.Clear();
                        test.expand_indicator.Visibility = Visibility.Visible;
                    }

                    expand_button.visual.Text = "+";
                    is_opened = false;
                    send_info_to.lines_altered(literal_line_index+1, -child_line_count);
                    send_info_to.diff_lines_altered(literal_line_index, -child_line_count);
                    // RELOAD OPEN TAG
                } else if (is_opened){ // if is open & we're reloading // reload the tag if reload is checked and reload is marked
                    if (struct_UI_element == null) Debug.Assert(false, "bad. no binded control");
                    if (struct_UI_element is TagblockParam && (struct_UI_element as TagblockParam).tag_data.blocks.Count == 0){
                        // if this tag is : 1. open & 2. no longer openable. // then we have to close this
                        unhook_children(); // i think?
                        expand(false); // close tag
                        return;
                    }
                    // we need an index to unload???
                    unhook_children(old_selected_index);
                    //expand_button.visual.Text = "-";
                    //is_opened = true;

                    if (struct_UI_element is StructParam){
                        StructParam test = struct_UI_element as StructParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data, test.guid, test.params_panel, test.struct_offset, this, test.key + "\\\\");

                    }else if (struct_UI_element is ArrayParam){
                        ArrayParam test = struct_UI_element as ArrayParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data, test.guid, test.params_panel, test.struct_offset + (test.struct_size * test.selected_index), this, test.key + "\\" + test.selected_index + '\\');

                    }else if (struct_UI_element is TagblockParam){
                        TagblockParam test = struct_UI_element as TagblockParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data.blocks[test.selected_index], test.tag_data.GUID, test.params_panel, 0, this, test.key + "\\" + test.selected_index + '\\');

                    }else if(struct_UI_element is ResourceParam){
                        ResourceParam test = struct_UI_element as ResourceParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data.blocks[0], test.tag_data.GUID, test.params_panel, 0, this, test.key + "\\\\");
                    }
                // OPEN CLOSED TAG
                } else{ // is closed // open the tag as normal, generate new UI
                    if (struct_UI_element == null) Debug.Assert(false, "bad. no binded control");
                    if (struct_UI_element is TagblockParam && (struct_UI_element as TagblockParam).tag_data.blocks.Count == 0){
                        send_info_to.main.DisplayNote((struct_UI_element as TagblockParam).Namebox.Text + " has no content", null, error_level.NOTE);
                        return;} // failed because tagblock not openable
                    if (struct_UI_element is ResourceParam && (struct_UI_element as ResourceParam).tag_data == null){
                        send_info_to.main.DisplayNote((struct_UI_element as ResourceParam).Namebox.Text + " has no loaded tag data", null, error_level.NOTE);
                        return;} // failed because no resource data
                    

                    expand_button.visual.Text = "-";
                    is_opened = true;

                    send_info_to.lines_altered(literal_line_index + 1, child_line_count);
                    if (struct_UI_element is StructParam){
                        StructParam test = struct_UI_element as StructParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data, test.guid, test.params_panel, test.struct_offset, this, test.key + "\\\\");

                    }else if (struct_UI_element is ArrayParam){
                        ArrayParam test = struct_UI_element as ArrayParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data, test.guid, test.params_panel, test.struct_offset + (test.struct_size * test.selected_index), this, test.key + "\\" + test.selected_index + '\\');

                    }else if (struct_UI_element is TagblockParam){
                        TagblockParam test = struct_UI_element as TagblockParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data.blocks[test.selected_index], test.tag_data.GUID, test.params_panel, 0, this, test.key + "\\" + test.selected_index + '\\');

                    }else if(struct_UI_element is ResourceParam){
                        ResourceParam test = struct_UI_element as ResourceParam;
                        test.expand_indicator.Visibility = Visibility.Collapsed;
                        send_info_to.Expand_struct(test.tag_data.blocks[0], test.tag_data.GUID, test.params_panel, 0, this, test.key + "\\\\");
                    }
                    send_info_to.diff_lines_altered(literal_line_index, child_line_count);
                }
                send_info_to.attempt_to_update_lists();
        }}
        // note this can be + or -
        void lines_altered(int at_line, int num_of_lines_changed){
            if (num_of_lines_changed < 0){ // aka we're removing lines
                for (int i = 0; i < -num_of_lines_changed; i++){
                    line_indexes.RemoveAt(at_line);
                    line_groups.RemoveAt(at_line);
            }}
            if (at_line <= current_highlighted_line){ // reposition the selected line to reflect the position after the change
                if ((num_of_lines_changed < 0) && (at_line-num_of_lines_changed > current_highlighted_line))
                    current_highlighted_line = at_line-1;
                else current_highlighted_line += num_of_lines_changed;
                selec_border.RenderTransform = new TranslateTransform(0, current_highlighted_line * 18);
            }
            recurs_lines_altered(root_expand, at_line, num_of_lines_changed);
        }
        // this one should explicitly be called after the string lines are altered or whatever
        void diff_lines_altered(int at_line, int num_of_lines_changed){
            // and here we process the diffs
            // if lines were removed, then we are only going to merge/raise clumps
            if (num_of_lines_changed < 0){
                // if clump was caught between at line & atline+ lines removed, then we have to collapse it 
                // duplicate dictionary so we can make changes while unaffecting our iteration
                Dictionary<int, diffs_clump> duplicate_dictionary = new(diffs_ui_dict);
                foreach (var diff_clump in duplicate_dictionary){
                    if (diff_clump.Value.line_index > at_line){
                        // if it falls within the range of collapsed lines, then we need to merge it to our at line index
                        if (diff_clump.Value.line_index <= at_line - num_of_lines_changed){ // negate negative amount of lines
                            // check to see if a clump is already at the target location 
                            int new_line_number = Convert.ToInt32(line_indexes[at_line]);
                            diffs_ui_dict.TryGetValue(new_line_number, out diffs_clump? existing_clump);
                            if (existing_clump != null){
                                // then just copy this guys info onto that guys
                                foreach(var v in diff_clump.Value.groups){
                                    if (existing_clump.groups.ContainsKey(v.Key)){
                                        // if somehow, we have two clumps that have values that have the same target line number,
                                        // then we just pop them into the same diff group // we should really throw an exception instead, this indicates a very critical failure
                                        throw new Exception("massive failure!!, two diff groups contained target line numbers that were equal! (groups cannot contain matching line numbers)");
                                        foreach (var diff in v.Value.diffs) existing_clump.groups[v.Key].diffs.Add(diff.Key, diff.Value);
                                    // else we just pop each diff group into the existing clump
                                    } else{
                                        v.Value.parent_clump = existing_clump;
                                        existing_clump.groups.Add(v.Key, v.Value);
                                    }
                                }
                                existing_clump.ui.reload(); // update UI
                                remove_diffs_clump(diff_clump.Value);
                            } else { // we have to update this guy to move them
                                diffs_ui_dict.Remove(diff_clump.Value.current_line_number);
                                diff_clump.Value.line_index = at_line; // remember, its negative
                                diff_clump.Value.current_line_number = new_line_number;
                                diffs_ui_dict.Add(new_line_number, diff_clump.Value);
                                reposition_diff(diff_clump.Value);
                                diff_clump.Value.ui.reload(); // update UI
                        // else we just update the line index, as the line number remains unaffected
                        }} else {
                            diff_clump.Value.line_index += num_of_lines_changed; // remember, its negative
                            reposition_diff(diff_clump.Value);
                            diff_clump.Value.ui.reload(); // update UI
                        }
            // else if lines were added, then we potentially have to split up our clumps
            }}} else{
                // we need to update the line index for any other blocks that might follow after at line
                foreach (var diff_clump in diffs_ui_dict){
                    if (diff_clump.Value.line_index > at_line){
                        diff_clump.Value.line_index += num_of_lines_changed;
                        reposition_diff(diff_clump.Value);
                        diff_clump.Value.ui.reload(); // update UI
                }}
                // we only ever need to split a single clump, as its only possible for a single clump to exist on a tagblock, or whatever we opened
                int new_line_number = Convert.ToInt32(line_indexes[at_line]);
                diffs_ui_dict.TryGetValue(new_line_number, out diffs_clump? expanded_clump);
                if (expanded_clump != null){
                    bool clump_should_still_exist = false;
                    // then we have to split this clump up probably
                    List<int> removed_groups = new();
                    foreach(var diff_group in expanded_clump.groups){
                        // test if this clump has a better possible line now in the newly created lines
                        int next_best_line_number = expanded_clump.current_line_number;
                        int next_best_line_index = expanded_clump.line_index;
                        for (int i = 1; i <= num_of_lines_changed; i++){
                            int current_line_number = Convert.ToInt32(line_indexes[at_line + i]);
                            if (current_line_number <= diff_group.Value.target_line_number){
                                next_best_line_number = current_line_number;
                                next_best_line_index = at_line + i;
                        }}
                        if (next_best_line_number != expanded_clump.current_line_number){
                            removed_groups.Add(diff_group.Key);
                            // then we found a better spot to put this guy in, create new clump
                            diffs_ui_dict.TryGetValue(next_best_line_number, out diffs_clump? existing_clump);
                            if (existing_clump != null){
                                // then just copy this group into the existing clump
                                if (existing_clump.groups.ContainsKey(diff_group.Key))
                                    throw new Exception("massive failure!!, two diff groups contained target line numbers that were equal! (groups cannot contain matching line numbers)");
                                // else we just pop each diff group into the existing clump
                                diff_group.Value.parent_clump = existing_clump;
                                existing_clump.groups.Add(diff_group.Key, diff_group.Value);
                                existing_clump.ui.reload(); // update UI
                            // otherwise, create a new clump to host this target group
                            } else {
                                diffs_clump new_clump = create_diff_clump(next_best_line_number, next_best_line_index);
                                diffs_ui_dict[next_best_line_number] = new_clump;
                                diff_group.Value.parent_clump = new_clump;
                                new_clump.groups[diff_group.Value.target_line_number] = diff_group.Value;
                        }} 
                        else clump_should_still_exist = true;
                    }
                    if (!clump_should_still_exist){
                        // then all its child components were moved and we should delete this clump
                        remove_diffs_clump(expanded_clump);
                    } else { // we need to remove all the items that are now in other clumps
                        foreach (int key in removed_groups)
                            expanded_clump.groups.Remove(key);
                        expanded_clump.ui.reload();
                    }
                }
            }
        }
        void recurs_lines_altered(expand_link link, int at_line, int num_of_lines_changed){
            if ((!link.is_on_screen) || (!link.is_opened)) return; // only is_opened is needed????
            foreach (var baby_link in link.child_links){
                if (baby_link.Value.is_on_screen == false) continue; // this type has not be inplemented yet, skip for now // REPLACE WITH ERROR ONCE IMPLEMENTED //

                if (baby_link.Value.literal_line_index >= at_line){ // so if this is past the point where they'd be affected
                    baby_link.Value.literal_line_index += num_of_lines_changed;
                    // recalculate visual position
                    baby_link.Value.expand_button.RenderTransform = new TranslateTransform(0, baby_link.Value.literal_line_index * 18);
                }
                recurs_lines_altered(baby_link.Value, at_line, num_of_lines_changed);
        }}
        void attempt_to_update_lists(){
            indexes_panel.ItemsSource = line_indexes;
            types_panel.ItemsSource = line_groups;
        }

        void setup_struct_element(expand_link data_link, UserControl target, int line_index){
            data_link.struct_UI_element = target; // inherit the target for expanding
            data_link.literal_line_index = line_index;
            data_link.is_on_screen = true;

            data_link.expand_button = new(data_link){
                RenderTransform = new TranslateTransform(0, line_index * 18)
            };
            // we need a panel 
            expanders_panel.Children.Add(data_link.expand_button);

            //if (struct_UI_element is StructParam)
            //else if (struct_UI_element is StructParam)
            //else if (struct_UI_element is StructParam)
            //else if (struct_UI_element is StructParam)
            //StructParam test = target as StructParam;
            //data_link.expand(); // not a good idea to expand as we're setting up, because it would prove to be quite difficult to know which line the next struct would be at, seeing as this doesn't return that information
            //Expand_struct(test.tag_data, test.guid, test.params_panel, test.struct_offset, data_link);
        }

        ObservableCollection<string> line_indexes = new();
        ObservableCollection<string> line_groups = new();
        public void Expand_struct(Tag.thing _struct, string struct_guid, StackPanel container, int struct_offset, expand_link expandus_linkus, string key){
            // we need to generate a key to identify each component
            // ideally, we'd just index each element, and then index tagblocks?


            // container.Children.Clear(); // knock out any previous elements here, although it would be more efficient to reuse them
            XmlNode? guid_test = loaded_tag.reference_root.SelectSingleNode('_' + struct_guid);
            if (guid_test == null) Debug.Assert(false, "failed to locate guid xml node?");

            int current_line = expandus_linkus.literal_line_index;
            int theoretical_line = expandus_linkus.caculated_line_index;

            for (int i = 0; i < guid_test.ChildNodes.Count; i++){

                XmlNode node = guid_test.ChildNodes[i];
                string? param_name = node.Attributes?["Name"]?.Value;
                if (param_name == null)  Debug.Assert(false, "param has no name???");

                int offset = Convert.ToInt32(node.Attributes?["Offset"]?.Value, 16);
                offset += struct_offset; // this is for structs & array structs, so we can offset to the correct position, even though we're reading a different group of params


                // note that we need to calucate the block offset, not the filew offset as teh file offset doesn't exist as soon as we load the tag
                // then we give them the bock offset, and nam, and then tell them to initalize their value IE read from that offset
                // we also need to assign the block that its offsetted from, so it can read & write
                // integer types nee the int length, and signed boolean
                current_line++;
                theoretical_line++;
                int actual_line = theoretical_line + 0;
                int type = Convert.ToInt32(node.Name.Substring(1), 16);
                string param_key = key + param_name; // just slap on the index as our unique sub identifier
                // and then we can check if this key already exists within our diff set, and we can just update the value to have this new param ui or something

                // determine whether the item already exists or not
                UIElement? bobject = null;
                if (container.Children.Count > i)
                    bobject = container.Children[i];

                // only update lines information if this line does not already exist
                if (bobject == null){
                    if (line_groups.Count < current_line) line_groups.Add(group_names[type]);
                    else line_groups.Insert(current_line, group_names[type]);
                    // shouldn't be separate, but whatever for now
                    // figure out what index this guy should actually be at
                    if (line_indexes.Count < current_line) line_indexes.Add(theoretical_line.ToString());
                    else line_indexes.Insert(current_line, theoretical_line.ToString());
                    
                    // /////////////////////// //
                    // CREATE NEW CHILD PARAM //
                    // ///////////////////// //
                    switch (type){
                        case 0x0:{ // _field_string
                                StringParam pad_garb_val = new(this, type, actual_line, param_name, 32, param_key);
                                container.Children.Add(pad_garb_val);
                                try_hook_diff(pad_garb_val, param_key); 
                            }continue;  
                        case 0x1:{ // _field_long_string
                                StringParam pad_garb_val = new(this, type, actual_line, param_name, 256, param_key);
                                container.Children.Add(pad_garb_val);
                                try_hook_diff(pad_garb_val, param_key); 
                            }continue; 
                        case 0x2:{ // _field_string_id
                                HashParam pad_garb_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(pad_garb_val);
                                try_hook_diff(pad_garb_val, param_key); 
                            }continue;
                        case 0x3: //
                            break;  
                        case 0x4:{ // _field_char_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.signed_char, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;  
                        case 0x5:{ // _field_short_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.signed_short, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;  
                        case 0x6:{ // _field_long_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.signed_int, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;  
                        case 0x7:{ // _field_int64_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.signed_long, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;  
                        case 0x8:{ // _field_angle
                                AngleParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case  0x9: // _field_tag
                            break;  
                        case  0xA:{ // _field_char_enum
                                string[] enum_names = new string[node.ChildNodes.Count];
                                for (int i2 = 0; i2 < enum_names.Length; i2++)
                                    enum_names[i2] = node.ChildNodes[i2].Attributes?["n"]?.Value;
                                EnumParam new_val = new(this, type, actual_line, param_name, EnumParam.EnumType._byte, enum_names, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case  0xB:{ // _field_short_enum
                                string[] enum_names = new string[node.ChildNodes.Count];
                                for (int i2 = 0; i2 < enum_names.Length; i2++)
                                    enum_names[i2] = node.ChildNodes[i2].Attributes?["n"]?.Value;
                                EnumParam new_val = new(this, type, actual_line, param_name, EnumParam.EnumType._short, enum_names, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case  0xC:{ // _field_long_enum
                                string[] enum_names = new string[node.ChildNodes.Count];
                                for (int i2 = 0; i2 < enum_names.Length; i2++)
                                    enum_names[i2] = node.ChildNodes[i2].Attributes?["n"]?.Value;
                                EnumParam new_val = new(this, type, actual_line, param_name, EnumParam.EnumType._int, enum_names, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case  0xD:{ // _field_long_flags
                                string[] flag_names = new string[32];
                                for (int i2 = 0; i2 < flag_names.Length; i2++){
                                    if (node.ChildNodes.Count > i2) flag_names[i2] = node.ChildNodes[i2].Attributes?["n"]?.Value;
                                    else flag_names[i2] = "Flag" + i2;
                                }
                                FlagsParam new_val = new(this, type, actual_line, param_name, FlagsParam.FlagType._int, flag_names, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case  0xE:{ // _field_word_flags
                                string[] flag_names = new string[16];
                                for (int i2 = 0; i2 < flag_names.Length; i2++){
                                    if (node.ChildNodes.Count > i2) flag_names[i2] = node.ChildNodes[i2].Attributes?["n"]?.Value;
                                    else flag_names[i2] = "Flag" + i2;
                                }
                                FlagsParam new_val = new(this, type, actual_line, param_name, FlagsParam.FlagType._short, flag_names, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case  0xF:{ // _field_byte_flags
                                string[] flag_names = new string[8];
                                for (int i2 = 0; i2 < flag_names.Length; i2++){
                                    if (node.ChildNodes.Count > i2) flag_names[i2] = node.ChildNodes[i2].Attributes?["n"]?.Value;
                                    else flag_names[i2] = "Flag" + i2;
                                }
                                FlagsParam new_val = new(this, type, actual_line, param_name, FlagsParam.FlagType._byte, flag_names, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x10:{ // _field_point_2d
                                DoubleshortParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x11:{ // _field_rectangle_2d
                                DoubleshortParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x12: // _field_rgb_color
                            break;  
                        case 0x13: // _field_argb_color 
                            break;   
                        case 0x14:{ // _field_real
                                FloatParam new_val = new(this, type, actual_line, param_name, false, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;  
                        case 0x15:{ // _field_real_fraction
                                FloatParam new_val = new(this, type, actual_line, param_name, true, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;  
                        case 0x16:{ // _field_real_point_2d
                                DoublefloatParam new_val = new(this, type, actual_line, param_name, false, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x17:{ // _field_real_point_3d
                                TriplefloatParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x18:{ // _field_real_vector_2d
                                DoublefloatParam new_val = new(this, type, actual_line, param_name, false, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x19:{ // _field_real_vector_3d
                                TriplefloatParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x1A:  // _field_real_quaternion
                            break; 
                        case 0x1B:{ // _field_real_euler_angles_2d
                                DoublefloatParam new_val = new(this, type, actual_line, param_name, false, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x1C:{ // _field_real_euler_angles_3d
                                TriplefloatParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x1D: // _field_real_plane_2d
                            break;  
                        case 0x1E: // _field_real_plane_3d
                            break;  
                        case 0x1F: // _field_real_rgb_color
                            break;  
                        case 0x20: // _field_real_argb_color
                            break;  
                        case 0x21: // _field_real_hsv_color
                            break;  
                        case 0x22: // _field_real_ahsv_color
                            break;  
                        case 0x23:{ // _field_short_bounds
                                DoubleshortParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x24:{ // _field_angle_bounds
                                DoubleangleParam new_val = new(this, type, actual_line, param_name, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x25:{ // _field_real_bounds
                                DoublefloatParam new_val = new(this, type, actual_line, param_name, false, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x26:{ // _field_real_fraction_bounds
                                DoublefloatParam new_val = new(this, type, actual_line, param_name, true, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x27: //
                            break;
                        case 0x28: //
                            break;
                        case 0x29: // _field_long_block_flags
                            break;
                        case 0x2A: // _field_word_block_flags
                            break;
                        case 0x2B: // _field_byte_block_flags
                            break;
                        case 0x2C: // _field_char_block_index
                            break;
                        case 0x2D: // _field_custom_char_block_index
                            break;
                        case 0x2E: // _field_short_block_index
                            break;
                        case 0x2F: // _field_custom_short_block_index
                            break;
                        case 0x30: // _field_long_block_index
                            break;
                        case 0x31: // _field_custom_long_block_index
                            break;
                        case 0x32: // 
                            break;
                        case 0x33: // 
                            break;
                        case 0x34: case 0x35:{ // _field_skip // _field_pad
                                short size = Convert.ToInt16(node.Attributes?["Length"]?.Value);
                                GarbageParameter pad_garb_val = new(this, type, actual_line, param_name, size, param_key);
                                container.Children.Add(pad_garb_val);
                                try_hook_diff(pad_garb_val, param_key); 
                            }continue;
                        case 0x36:{ // _field_explanation
                                CommentParam new_val = new("// "+param_name);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x37:{ // _field_custom
                                // min max values for those few istances in the levl
                                string? min = node.Attributes?["Min"]?.Value;
                                string? max = node.Attributes?["Max"]?.Value;
                                if (min != null) param_name += " min: " + min;
                                if (max != null) param_name += " max: " + max;
                                CommentParam new_val = new("// "+param_name);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x38:{ // _field_struct 
                                string next_guid = node.Attributes?["GUID"]?.Value;
                                expand_link struct_link = expandus_linkus.child_links[i];
                                StructParam param = new(param_name, _struct, offset, next_guid, struct_link, param_key);
                                container.Children.Add(param);
                                setup_struct_element(struct_link, param, current_line);
                                theoretical_line += struct_link.total_contained_lines;
                                try_hook_diff(param, param_key); 
                            }continue;
                        case 0x39:{ // _field_array
                                string next_guid = node.Attributes?["GUID"]?.Value;
                                int array_length = Convert.ToInt32(node.Attributes?["Count"]?.Value);
                                int array_struct_size = Convert.ToInt32(loaded_tag.reference_root.SelectSingleNode('_' + next_guid).Attributes?["Size"]?.Value, 16);
                                expand_link struct_link = expandus_linkus.child_links[i];
                                ArrayParam param = new(param_name, _struct, offset, next_guid, struct_link, array_length, array_struct_size, param_key);
                                container.Children.Add(param);
                                setup_struct_element(struct_link, param, current_line);
                                theoretical_line += struct_link.total_contained_lines;
                                try_hook_diff(param, param_key); 
                            }continue;
                        case 0x3A: // 
                            break;
                        case 0x3B: // end of struct
                            break;
                        case 0x3C:{ // _field_byte_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.unsigned_char, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x3D:{ // _field_word_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.unsigned_short, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x3E:{ // _field_dword_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.unsigned_int, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x3F:{ // _field_qword_integer
                                IntegerParam new_val = new(this, type, actual_line, param_name, IntegerParam.IntType.unsigned_long, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x40:{ // _field_block_v2
                                if (!_struct.tag_block_refs.ContainsKey((ulong)offset)) 
                                    break;

                                expand_link struct_link = expandus_linkus.child_links[i];
                                TagblockParam param = new(param_name, _struct.tag_block_refs[(ulong)offset], struct_link, param_key);
                                container.Children.Add(param);
                                setup_struct_element(struct_link, param, current_line);
                                theoretical_line += struct_link.total_contained_lines;
                                try_hook_diff(param, param_key); 
                            }continue;
                        case 0x41:{ // _field_reference_v2
                                TagrefParam new_val = new(this, type, actual_line, param_name, main.Active_TagExplorer, param_key);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x42:{ // _field_data_v2
                                DataParam new_val = new(param_name, _struct.tag_resource_refs[(ulong)offset], (short)TagStructs.param_group_sizes[type], param_key, this);
                                container.Children.Add(new_val);
                                try_hook_diff(new_val, param_key); 
                            }continue;
                        case 0x43:{ // tag_resource
                                expand_link struct_link = expandus_linkus.child_links[i];
                                ResourceParam param = new(param_name, _struct.resource_file_refs[(ulong)offset], struct_link, param_key);
                                container.Children.Add(param);
                                setup_struct_element(struct_link, param, current_line);
                                theoretical_line += struct_link.total_contained_lines;
                                try_hook_diff(param, param_key); 
                            }continue;
                        case 0x44: // UNKNOWN
                            break;
                        case 0x45: // UNKNOWN
                            break;
                    } // if we're still here, then the type was unimplemented, create new garbage block
                    GarbageParameter garb_val = new(this, type, actual_line, param_name, (short)TagStructs.param_group_sizes[type], param_key);
                    container.Children.Add(garb_val);
                    try_hook_diff(garb_val, param_key);
                } else{

                    // /////////////////// //
                    // RELOAD CHILD PARAM //
                    // ///////////////// //
                    switch (type){
                        case 0x0:{ // _field_string
                                StringParam? pad_garb_val = bobject as StringParam;
                                Debug.Assert(pad_garb_val != null, "cast failed");
                                pad_garb_val.reload(param_key);
                                try_hook_diff(pad_garb_val, param_key);
                            }continue;
                        case 0x1:{ // _field_long_string
                                StringParam? pad_garb_val = bobject as StringParam;
                                Debug.Assert(pad_garb_val != null, "cast failed");
                                pad_garb_val.reload(param_key);
                                try_hook_diff(pad_garb_val, param_key);
                            }continue;
                        case 0x2:{ // _field_string_id
                                HashParam? pad_garb_val = bobject as HashParam;
                                Debug.Assert(pad_garb_val != null, "cast failed");
                                pad_garb_val.reload(param_key);
                                try_hook_diff(pad_garb_val, param_key);
                            }continue;
                        case 0x3:  //
                            break;
                        case 0x3C: // _field_byte_integer // REORDERED //
                        case 0x3D: // _field_word_integer // REORDERED //
                        case 0x3E: // _field_dword_integer // REORDERED //
                        case 0x3F: // _field_qword_integer // REORDERED //
                        case 0x4:  // _field_char_integer
                        case 0x5:  // _field_short_integer
                        case 0x6:  // _field_long_integer
                        case 0x7:{ // _field_int64_integer
                                IntegerParam? new_val = bobject as IntegerParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x8:{ // _field_angle
                                AngleParam? new_val = bobject as AngleParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case  0x9:  // _field_tag
                            break;  
                        case  0xA:  // _field_char_enum
                        case  0xB:  // _field_short_enum
                        case  0xC:{ // _field_long_enum
                                EnumParam? new_val = bobject as EnumParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case  0xD:  // _field_long_flags
                        case  0xE:  // _field_word_flags
                        case  0xF:{ // _field_byte_flags
                                FlagsParam? new_val = bobject as FlagsParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;

                        case 0x10:{ // _field_point_2d
                                DoubleshortParam? new_val = bobject as DoubleshortParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x11:{ // _field_rectangle_2d // we may decide to change this to its own struct some time in the future
                                DoubleshortParam? new_val = bobject as DoubleshortParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x12:  // _field_rgb_color
                            break;  
                        case 0x13:  // _field_argb_color 
                            break;   
                        case 0x14:  // _field_real
                        case 0x15:{ // _field_real_fraction
                                FloatParam? new_val = bobject as FloatParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x25: // _field_real_bounds
                        case 0x26: // _field_real_fraction_bounds
                        case 0x16:  // _field_real_point_2d
                        case 0x18:{ // _field_real_vector_2d // REORDERED //
                                DoublefloatParam? new_val = bobject as DoublefloatParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x17:  // _field_real_point_3d // REORDERED //
                        case 0x19:{ // _field_real_vector_3d
                                TriplefloatParam? new_val = bobject as TriplefloatParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x1A:  // _field_real_quaternion
                            break; 
                        case 0x1B:{ // _field_real_euler_angles_2d
                                DoublefloatParam? new_val = bobject as DoublefloatParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x1C:{ // _field_real_euler_angles_3d
                                TriplefloatParam? new_val = bobject as TriplefloatParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x1D: // _field_real_plane_2d
                            break;  
                        case 0x1E: // _field_real_plane_3d
                            break;  
                        case 0x1F: // _field_real_rgb_color
                            break;  
                        case 0x20: // _field_real_argb_color
                            break;  
                        case 0x21: // _field_real_hsv_color
                            break;  
                        case 0x22: // _field_real_ahsv_color
                            break;  
                        case 0x23:{ // _field_short_bounds
                                DoubleshortParam? new_val = bobject as DoubleshortParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x24:{ // _field_angle_bounds
                                DoubleangleParam? new_val = bobject as DoubleangleParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x27: //
                            break;
                        case 0x28: //
                            break;
                        case 0x29: // _field_long_block_flags
                            break;
                        case 0x2A: // _field_word_block_flags
                            break;
                        case 0x2B: // _field_byte_block_flags
                            break;
                        case 0x2C: // _field_char_block_index
                            break;
                        case 0x2D: // _field_custom_char_block_index
                            break;
                        case 0x2E: // _field_short_block_index
                            break;
                        case 0x2F: // _field_custom_short_block_index
                            break;
                        case 0x30: // _field_long_block_index
                            break;
                        case 0x31: // _field_custom_long_block_index
                            break;
                        case 0x32: // 
                            break;
                        case 0x33: // 
                            break;
                        case 0x34: case 0x35:{ // _field_skip // _field_pad
                                GarbageParameter? new_val = bobject as GarbageParameter;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x36:{ // _field_explanation
                                // do literally nothing, this block is fine as is right now
                            }continue;
                        case 0x37:{ // _field_custom
                                // also do literally nothing, the custom values are static
                            }continue;


                        case 0x38:{ // _field_struct 
                                StructParam? new_val = bobject as StructParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(_struct, offset, param_key);
                                theoretical_line += new_val.parent.total_contained_lines;
                                // then process children //
                                if (new_val.parent.is_opened)
                                    new_val.parent.expand(true);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x39:{ // _field_array
                                ArrayParam? new_val = bobject as ArrayParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(_struct, offset, param_key);
                                theoretical_line += new_val.parent.total_contained_lines;
                                // then process children //
                                if (new_val.parent.is_opened)
                                    new_val.parent.expand(true);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x3A: // 
                            break;
                        case 0x3B: // end of struct // do we not have criteria that blocks this one?
                            break;
                        case 0x40:{ // _field_block_v2
                                Debug.Assert(_struct.tag_block_refs.ContainsKey((ulong)offset), "idk why this is here, but changing it into something neater");

                                TagblockParam? new_val = bobject as TagblockParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_name, _struct.tag_block_refs[(ulong)offset], param_key);
                                theoretical_line += new_val.parent.total_contained_lines;
                                // then process children //
                                if (new_val.parent.is_opened)
                                    new_val.parent.expand(true);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x41:{ // _field_reference_v2
                                TagrefParam? new_val = bobject as TagrefParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_key);
                                try_hook_diff(new_val, param_key);
                           } continue;
                        case 0x42:{ // _field_data_v2
                                DataParam? new_val = bobject as DataParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(param_name, _struct.tag_resource_refs[(ulong)offset], (short)TagStructs.param_group_sizes[type], param_key);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x43:{ // tag_resource // fixed but might still be broken?
                                // make sure the resource file ref exists or generate error, even though it'd literally throw the error in the next 3 lines
                                Debug.Assert(_struct.resource_file_refs.ContainsKey((ulong)offset), "idk why this is here, but changing it into something neater");

                                ResourceParam? new_val = bobject as ResourceParam;
                                Debug.Assert(new_val != null, "cast failed");
                                new_val.reload(_struct.resource_file_refs[(ulong)offset], param_key);
                                theoretical_line += new_val.parent.total_contained_lines;
                                // then process children //
                                if (new_val.parent.is_opened)
                                    new_val.parent.expand(true);
                                try_hook_diff(new_val, param_key);
                            }continue;
                        case 0x44: // UNKNOWN
                            break;
                        case 0x45: // UNKNOWN
                            break;
                    } // if we're still here, then the type was unimplemented, create new garbage block
                    GarbageParameter? garb_val = bobject as GarbageParameter;
                    Debug.Assert(garb_val != null, "cast failed");
                    garb_val.reload(param_key);
                    try_hook_diff(garb_val, param_key);
                }


            }
        }

        // matches line number to groups of values
        Dictionary<int, diffs_clump> diffs_ui_dict = new();
        // matches param diff to their unique key
        Dictionary<string, param_diff> diffs_dict = new();
        public class diffs_clump {
            public DiffExpand ui;
            public int current_line_number; // inferred by the key?
            public int line_index;
            public Dictionary<int, diffs_group> groups; // sorted by target line
        }
        public class diffs_group{
            public int target_line_number;
            // sorted by diff key
            public Dictionary<string, param_diff> diffs = new();
            public diffs_clump parent_clump; // so we can clear this if it becomes empty
        }
        public class param_diff{
            public string key;
            public string original_value;
            public string updated_value;
            public string param_name;
            public int type;
            public UIElement? param_ui;
            // so we can manage this mess easier
            public diffs_group line_group;
        }

        diffs_clump create_diff_clump(int line_number, int line_index){
            if (diffs_ui_dict.ContainsKey(line_number)) throw new Exception("this clump already exists, cannot create new one with same key");

            diffs_clump new_clump = new();
            new_clump.current_line_number = line_number;
            new_clump.line_index = line_index;
            new_clump.groups = new();
            new_clump.ui = new(new_clump, this);
            diffs_ui_dict[line_number] = new_clump;
            // add diff to UI then position
            reposition_diff(new_clump);
            diffs_panel.Children.Add(new_clump.ui);
            return new_clump;
        }
        void reposition_diff(diffs_clump diff){
            // instead of passing positions off, we'll just read them from the diffs group
            diff.ui.RenderTransform = new TranslateTransform(0, diff.line_index * 18);
        }
        public byte[] read_param_value(string api_path)
        {
            if (api_path.StartsWith("__root\\\\")) api_path = api_path.Substring(8);
            return loaded_tag.get_basic(api_path);
        }
        public void write_param_value(string api_path, byte[] new_value)
        {
            if (api_path.StartsWith("__root\\\\")) api_path = api_path.Substring(8);
            loaded_tag.set_basic(api_path, new_value);
            //loaded_tag.poke_basic(api_path, new_value);
        }
        public void set_diff(UIElement element, string key, string param_name, int param_type, string original, string updated, int line_number){
            if (module_file_header != null){
                if (tag_is_modified == false){
                    container.Header = tabname + '*';
                    tag_is_modified = true;
            }}

            diffs_dict.TryGetValue(key, out param_diff? diff_object);
            if (diff_object != null){
                // ok easy, just update the value
                diff_object.updated_value = updated;
            } else{ // if we didn't find a diff already, then we simple create a new diff struct based off the information that we have
                diff_object = new();
                diff_object.key = key;
                diff_object.original_value = original;
                diff_object.updated_value = updated;
                diff_object.param_name = param_name;
                diff_object.type = param_type;
                diff_object.param_ui = element;
                // now we need to figure out where to put this in the UI
                // calculate best spot to enter this at
                int best_line_index = 0; // fallback to root
                int best_line_number = 0;
                for(int i = 0; i < line_indexes.Count; i++){
                    int current_line_number = Convert.ToInt32(line_indexes[i]);
                    if (current_line_number <= line_number){
                        best_line_index = i;
                        best_line_number = current_line_number;
                }}
                // now we have to find the clump & update the clump info
                diffs_ui_dict.TryGetValue(best_line_number, out diffs_clump? clump);
                if (clump == null){
                    clump = create_diff_clump(best_line_number, best_line_index);
                    diffs_ui_dict[best_line_number] = clump;
                }
                // get/create diff group
                clump.groups.TryGetValue(line_number, out diffs_group? diff_group);
                if (diff_group == null){
                    diff_group = new();
                    diff_group.parent_clump = clump;
                    diff_group.target_line_number = line_number;
                    diff_group.diffs = new();
                    clump.groups[line_number] = diff_group;
                }
                // error check to see if this diff already exists?
                if (diff_group.diffs.ContainsKey(key))
                    throw new Exception("param diff was already contained within diff line group");
                diff_group.diffs[key] = diff_object;
                // then assign that info to our quick diff list
                diff_object.line_group = diff_group;
                diffs_dict[key] = diff_object;
            }

        }
        // we need to iterate through all child params before deleting them
        // so we unhook their references, allowing them to be garbage collected
        // call this on every single key that gets destroyed when children of closed struct are cleared
        public void try_unhook_diff(string key){
            diffs_dict.TryGetValue(key, out param_diff? diff_object);
            if (diff_object != null) diff_object.param_ui = null;
        }
        // call this whenever creating a new ui param
        public void try_hook_diff(UIElement paramui, string key){
            diffs_dict.TryGetValue(key, out param_diff? diff_object);
            if (diff_object != null){
                if (diff_object.param_ui != null)
                    throw new Exception("how are we attempting to rehook to a key that is already hooked");
                diff_object.param_ui = paramui;
        }}
        public void revert_diff(param_diff diff){
            // remove this diff, as it is no longer needed
            remove_diff(diff);
            // then apply the revert
            switch (diff.type){
                case 0x0: // _field_string
                    StringParam.revert_value(diff.original_value, 32, diff.param_ui as StringParam, this, diff.key);
                    return;  
                case 0x1: // _field_long_string
                    StringParam.revert_value(diff.original_value, 256, diff.param_ui as StringParam, this, diff.key);
                    return; 
                case 0x2: // _field_string_id
                    HashParam.revert_value(diff.original_value, diff.param_ui as HashParam, this, diff.key);
                    return;
                case 0x3: //
                break;  
                case 0x4: // _field_char_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.signed_char,  diff.param_ui as IntegerParam, this, diff.key);
                    return;  
                case 0x5: // _field_short_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.signed_short, diff.param_ui as IntegerParam, this, diff.key);
                    return;  
                case 0x6: // _field_long_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.signed_int, diff.param_ui as IntegerParam, this, diff.key);
                    return;  
                case 0x7: // _field_int64_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.signed_long, diff.param_ui as IntegerParam, this, diff.key);
                    return;  
                case 0x8: // _field_angle
                    AngleParam.revert_value(diff.original_value, diff.param_ui as AngleParam, this, diff.key);
                    return;
                case  0x9: // _field_tag
                break;  
                case  0xA: // _field_char_enum
                    EnumParam.revert_value(diff.original_value, EnumParam.EnumType._byte, diff.param_ui as EnumParam, this, diff.key);
                    return;
                case  0xB: // _field_short_enum
                    EnumParam.revert_value(diff.original_value, EnumParam.EnumType._short, diff.param_ui as EnumParam, this, diff.key);
                    return;
                case  0xC: // _field_long_enum
                    EnumParam.revert_value(diff.original_value, EnumParam.EnumType._int, diff.param_ui as EnumParam, this, diff.key);
                    return;
                case  0xD: // _field_long_flags
                    FlagsParam.revert_value(diff.original_value, FlagsParam.FlagType._int, diff.param_ui as FlagsParam, this, diff.key);
                    return;
                case  0xE: // _field_word_flags
                    FlagsParam.revert_value(diff.original_value, FlagsParam.FlagType._short, diff.param_ui as FlagsParam, this, diff.key);
                    return;
                case  0xF: // _field_byte_flags
                    FlagsParam.revert_value(diff.original_value, FlagsParam.FlagType._byte, diff.param_ui as FlagsParam, this, diff.key);
                    return;
                case 0x10: // _field_point_2d
                    DoubleshortParam.revert_value(diff.original_value, diff.param_ui as DoubleshortParam, this, diff.key);
                    return;
                case 0x11: // _field_rectangle_2d
                    DoubleshortParam.revert_value(diff.original_value, diff.param_ui as DoubleshortParam, this, diff.key);
                    return;
                case 0x12: // _field_rgb_color
                break;  
                case 0x13: // _field_argb_color 
                break;   
                case 0x14: // _field_real
                    FloatParam.revert_value(diff.original_value, diff.param_ui as FloatParam, this, diff.key);
                    return;  
                case 0x15: // _field_real_fraction
                    FloatParam.revert_value(diff.original_value, diff.param_ui as FloatParam, this, diff.key);
                    return;  
                case 0x16: // _field_real_point_2d
                    DoublefloatParam.revert_value(diff.original_value, diff.param_ui as DoublefloatParam, this, diff.key);
                    return;
                case 0x17: // _field_real_point_3d
                    TriplefloatParam.revert_value(diff.original_value, diff.param_ui as TriplefloatParam, this, diff.key);
                    return;
                case 0x18: // _field_real_vector_2d
                    DoublefloatParam.revert_value(diff.original_value, diff.param_ui as DoublefloatParam, this, diff.key);
                    return;
                case 0x19: // _field_real_vector_3d
                    TriplefloatParam.revert_value(diff.original_value, diff.param_ui as TriplefloatParam, this, diff.key);
                    return;
                case 0x1A:  // _field_real_quaternion
                break; 
                case 0x1B: // _field_real_euler_angles_2d
                    DoublefloatParam.revert_value(diff.original_value, diff.param_ui as DoublefloatParam, this, diff.key);
                    return;
                case 0x1C: // _field_real_euler_angles_3d
                    TriplefloatParam.revert_value(diff.original_value, diff.param_ui as TriplefloatParam, this, diff.key);
                    return;
                case 0x1D: // _field_real_plane_2d
                break;  
                case 0x1E: // _field_real_plane_3d
                break;  
                case 0x1F: // _field_real_rgb_color
                break;  
                case 0x20: // _field_real_argb_color
                break;  
                case 0x21: // _field_real_hsv_color
                break;  
                case 0x22: // _field_real_ahsv_color
                break;  
                case 0x23: // _field_short_bounds
                    DoubleshortParam.revert_value(diff.original_value, diff.param_ui as DoubleshortParam, this, diff.key);
                    return;
                case 0x24: // _field_angle_bounds
                    DoubleangleParam.revert_value(diff.original_value, diff.param_ui as DoubleangleParam, this, diff.key);
                    return;
                case 0x25: // _field_real_bounds
                    DoublefloatParam.revert_value(diff.original_value, diff.param_ui as DoublefloatParam, this, diff.key);
                    return;
                case 0x26: // _field_real_fraction_bounds
                    DoublefloatParam.revert_value(diff.original_value, diff.param_ui as DoublefloatParam, this, diff.key);
                    return;
                case 0x27: //
                break;
                case 0x28: //
                break;
                case 0x29: // _field_long_block_flags
                break;
                case 0x2A: // _field_word_block_flags
                break;
                case 0x2B: // _field_byte_block_flags
                break;
                case 0x2C: // _field_char_block_index
                break;
                case 0x2D: // _field_custom_char_block_index
                break;
                case 0x2E: // _field_short_block_index
                break;
                case 0x2F: // _field_custom_short_block_index
                break;
                case 0x30: // _field_long_block_index
                break;
                case 0x31: // _field_custom_long_block_index
                break;
                case 0x32: // 
                break;
                case 0x33: // 
                break;
                case 0x34: case 0x35: // _field_skip // _field_pad
                    GarbageParameter.revert_value(diff.original_value, diff.param_ui as GarbageParameter, this, diff.key);
                    return;
                case 0x36: // _field_explanation
                    throw new Exception("param type Comment cannot be reverted, nor have a diff");
                case 0x37: // _field_custom
                    throw new Exception("param type Comment cannot be reverted, nor have a diff");
                case 0x38: // _field_struct 
                    throw new Exception("param type Struct cannot be reverted, nor have a diff");
                case 0x39: // _field_array
                    throw new Exception("param type Array cannot be reverted, nor have a diff");
                case 0x3A: // 
                break;
                case 0x3B: // end of struct
                    throw new Exception("param type End of struct cannot be reverted, nor have a diff");
                case 0x3C: // _field_byte_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.unsigned_char, diff.param_ui as IntegerParam, this, diff.key);
                    return;
                case 0x3D: // _field_word_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.unsigned_short, diff.param_ui as IntegerParam, this, diff.key);
                    return;
                case 0x3E: // _field_dword_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.unsigned_int, diff.param_ui as IntegerParam, this, diff.key);
                    return;
                case 0x3F: // _field_qword_integer
                    IntegerParam.revert_value(diff.original_value, IntegerParam.IntType.unsigned_long, diff.param_ui as IntegerParam, this, diff.key);
                    return;
                case 0x40: // _field_block_v2
                    throw new Exception("Unimplemented revert type!!"); //TagblockParam
                case 0x41: // _field_reference_v2
                    TagrefParam.revert_value(diff.original_value, diff.param_ui as TagrefParam, this, diff.key);
                    return;
                case 0x42: // _field_data_v2
                    throw new Exception("Unimplemented revert type!!"); //DataParam
                case 0x43: // tag_resource
                    throw new Exception("Unimplemented revert type!!"); //ResourceParam
                case 0x44: // UNKNOWN
                break;
                case 0x45: // UNKNOWN
                break;
            } // if we're still here, then the type was unimplemented, create new garbage block
            GarbageParameter.revert_value(diff.original_value, diff.param_ui as GarbageParameter, this, diff.key);
        }
        public void remove_diff(param_diff diff){
            // this should only be called when we either revert a diff, or we saved changes?
            // essentially just untie all references to the diff, and any containers that then become empty without the diff
            diffs_dict.Remove(diff.key);
            diff.line_group.diffs.Remove(diff.key);
            diffs_clump clump = diff.line_group.parent_clump;
            clump.ui.reload(); // to update the visual elements if its open?
            if (diff.line_group.diffs.Count == 0){
                clump.groups.Remove(diff.line_group.target_line_number);
                // now if the clump is empty remove that too
                if (clump.groups.Count == 0)
                    remove_diffs_clump(clump);
            }
            // if we just removed the last diff, then mark this file as NOT edited
            if (diffs_dict.Count == 0 && module_file_header != null && tag_has_been_committed == false){
                tag_is_modified = false;
                container.Header = tabname;
            }
        }
        public void remove_diffs_clump(diffs_clump clump){
            diffs_ui_dict.Remove(clump.current_line_number);
            diffs_panel.Children.Remove(clump.ui);
            clump.ui.clear_references();
        }


        public static string[] group_names = new string[]
        {
            "string32",
            "string256",
            "stringID",
            "UNKNOWN",
            "sbyte",
            "short",
            "int",
            "long",
            "angle",
            "group",
            "enum8",
            "enum16",
            "enum32",
            "flags32",
            "flags16",
            "flags8",
            "point2D", 
            "rectangle2D",
            "rgb",
            "argb",
            "float",
            "fraction",
            "float2D",
            "float3D",
            "vector2D",
            "vector3D",
            "quarternion",
            "euler2D",
            "euler3D",
            "plane2D",
            "plane3D",
            "rgb_float",
            "argb_float",
            "hsv",
            "ahsv",
            "short_r",
            "angle_r",
            "float_r",
            "fraction_r",
            "UNKNOWN",
            "UNKNOWN",
            "b_flags32",
            "b_flags16",
            "b_flags8",
            "block8",
            "cblock8",
            "block16",
            "cblock16",
            "block32",
            "cblock32",
            "UNKNOWN",
            "UNKNOWN",
            "padding",
            "skip",
            "comment",
            "c_comment",
            "struct",
            "array",
            "UNKNOWN",
            "struct_end",
            "byte",
            "ushort",
            "uint",
            "ulong",
            "tagblock",
            "tagref",
            "data",
            "resource",
            "UNKNOWN",
            "UNKNOWN",
        };
        int current_highlighted_line = -1;
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // find where it was on screen, we only care about the Y axis
            // then calculate which line it was, recalculate so we know the position of the start of that line
            // finally create a border that simply highlights this box
            int clicked_line = (int)Math.Truncate(Mouse.GetPosition(editor_container).Y / 18);

            if (clicked_line >= line_indexes.Count) return; // clicked outside

            current_highlighted_line = clicked_line;
            if (selec_border.Visibility == Visibility.Collapsed) selec_border.Visibility = Visibility.Visible;


            selec_border.RenderTransform = new TranslateTransform(0, current_highlighted_line*18);

        }

        // export functionality
        public void unpack_to_files(){
            if (module_file_header == null){
                main.DisplayNote("file is standalone tag and does not require unpacking!", null, error_level.NOTE);
                return;
            }
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.ShowDialog();
            if (!string.IsNullOrWhiteSpace(saveFileDialog1.FileName)){
                try{// then write the bytes & then write the resources
                    System.IO.File.WriteAllBytes(saveFileDialog1.FileName, raw_tagbytes);
                    for (int i = 0; i < loaded_tag.resource_list.Count; i++)
                        System.IO.File.WriteAllBytes(saveFileDialog1.FileName + "_res_" + i, loaded_tag.resource_list[i].Key);
                } catch (Exception ex){main.DisplayNote("failed to unpack file", ex, error_level.WARNING);}
            }
        }
        public void export_loaded_to_files(){
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();
            saveFileDialog1.ShowDialog();
            if (!string.IsNullOrWhiteSpace(saveFileDialog1.FileName)){
                try{// setup compiler and get the bytes
                    var v = loaded_tag.compile();
                    System.IO.File.WriteAllBytes(saveFileDialog1.FileName, v.tag_bytes);
                    for (int i = 0; i < v.resource_bytes.Count; i++)
                        System.IO.File.WriteAllBytes(saveFileDialog1.FileName + "_res_" + i, v.resource_bytes[i]);
                } catch (Exception ex) { main.DisplayNote("failed to unpack file", ex, error_level.WARNING); }
            }
        }
    }
}
