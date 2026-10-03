using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Windows.Controls;
using static Infinite_module_test.tag_structs;
using static TagEditor.MainWindow;


namespace TagEditor.UI.Windows{
    public partial class TagViewer : UserControl{
        public TagViewer(MainWindow _main){
            InitializeComponent();
            main = _main;
        }
        MainWindow main;
        List<string> tab_keys = new();
        Dictionary<string, TagInstance> Tabs = new();
        public class tagfiles{
            public byte[] bytes;
            public List<KeyValuePair<byte[], bool>> resource_list;
        }
        static public tagfiles? get_tagbytes_with_resources(string tag_path, MainWindow main){
            tagfiles result = new();

            if (!File.Exists(tag_path)){ // fail via file not valid
                main.DisplayNote(tag_path + " is not a valid file", null, error_level.WARNING);
                return null;
            }
            using (FileStream fs = new FileStream(tag_path, FileMode.Open)){
                try{
                    byte[] bytes = new byte[4];
                    fs.Read(bytes, 0, 4);
                    if (!bytes.SequenceEqual(new byte[] { 0x75, 0x63, 0x73, 0x68 })){ // fail via file not a tag file
                        main.DisplayNote(tag_path + " is not a tag file", null, error_level.WARNING);
                        return null;
                }}catch{ // fail via error opening file
                    main.DisplayNote(tag_path + " failed to open", null, error_level.WARNING);
                    return null;
            }}
            // check to see if the name is a resource file name
            if (tag_path.Contains("_res_")){
                main.DisplayNote(tag_path + " appears to be a tag struct resource file, not a real tag", null, error_level.WARNING);
                return null;
            }
            // error checking past, open tag for real

            // lets first get a list of all the resource file that this guy probably owns
            var folder = Path.GetDirectoryName(tag_path);
            string tag_file_name = Path.GetFileName(tag_path);
            result.resource_list = new();

            foreach (var item in Directory.GetFiles(folder)){
                string file_name = Path.GetFileName(item);
                if (file_name.Length > tag_file_name.Length + 4 && file_name.StartsWith(tag_file_name)){
                    // get index of file, just incase the function that retrives all the files doesn't do it alphabetically
                    // then either insert or add 


                    try{
                        string resource_number = file_name.Substring(tag_file_name.Length + 5);
                        int resource_index = Convert.ToInt32(resource_number);
                        byte[] resource_bytes = File.ReadAllBytes(item);  
                        // test whether the first 4 bytes are the 'hscu' magic (or whatever its supposed to be)
                        bool is_standalone_resource = resource_bytes[0..4].SequenceEqual(new byte[] { 0x75, 0x63, 0x73, 0x68 });
                        // pop it at the end if the currently index is too high (this is probably a terrible idea)
                        if (resource_index >= result.resource_list.Count) result.resource_list.Add(new KeyValuePair<byte[], bool>(resource_bytes, is_standalone_resource));
                        else result.resource_list.Insert(resource_index, new KeyValuePair<byte[], bool>(resource_bytes, is_standalone_resource));
                    }catch { main.DisplayNote("resource file: \"" + item + "\" is unable to be opened, disregarding", null, error_level.WARNING); }
                }
            }
            // anomaly check // make sure all entries are of either type, else this will become very difficult to manage
            if (result.resource_list.Count > 0){
                bool inital = result.resource_list[0].Value;
                foreach (var item in result.resource_list)
                    if (item.Value != inital)
                        main.DisplayNote(item + " does not have a matching chunked/non-chunked status", null, error_level.WARNING);
            }
            
            try{result.bytes = File.ReadAllBytes(tag_path);
            }catch{ 
                main.DisplayNote(tag_path + " returned an error (likely due to file read attempt)", null, error_level.WARNING); 
                return null; 
            }
            // successful
            return result;
        }
        public void OpenTag(string tag_path){
            if (Tabs.ContainsKey(tag_path)){
                TagInstance target_tab = Tabs[tag_path];
                TagsTabs.SelectedIndex = TagsTabs.Items.IndexOf(target_tab.container);
                return;
            }

            tagfiles? files = get_tagbytes_with_resources(tag_path, main);
            if (files == null) return; // error message will be covered by that function

            tag test = new tag(files.resource_list);
            try{if (!test.Load_tag_file(files.bytes)){
                    main.DisplayNote(tag_path + " was not able to be loaded as a tag", null, error_level.WARNING);
                    return;
            }} catch(Exception e){ 
                main.DisplayNote(tag_path + " returned an error (likely due to file read attempt)", null, error_level.WARNING); return;
            }

            // load new tag tab here
            TabItem new_tag = new();
            TagInstance tag_interface = new TagInstance(main, null, test, new_tag, Path.GetFileName(tag_path), files.bytes);
            TagsTabs.Items.Add(new_tag);
            tab_keys.Add(tag_path);
            Tabs.Add(tag_path, tag_interface);
            tag_interface.LoadTag_UI();
            TagsTabs.SelectedIndex = TagsTabs.Items.IndexOf(tag_interface.container);
        }
        public void OpenModuleTag(directory_item item){
            if (Tabs.ContainsKey(item.name)){
                TagInstance target_tab = Tabs[item.name];
                TagsTabs.SelectedIndex = TagsTabs.Items.IndexOf(target_tab.container);
                return;}
            // TODO: i think we missed a couple of things that we were supposed to check
            // check to make sure the code is working right
            if (item.is_folder) { 
                main.DisplayNote(item.name + " is a folder, not a tag! failure of epic portportions!!", null, error_level.WARNING);
                return;}
            if (!item.is_module) { 
                main.DisplayNote(item.name + " is a local-disk tag, not a module tag!!", null, error_level.WARNING);
                return;}
            // check to see if the name is a resource file name
            if (item.name.Contains("_res_")){
                main.DisplayNote(item.name + " appears to be a tag struct resource file, not a real tag", null, error_level.WARNING);
                return;}

            // error checking past, open tag for real

            // lets first get a list of all the resource file that this guy probably owns
            List<KeyValuePair<byte[], bool>> resource_list = new();
            try{ // idk why we have a try catch here
                if (item.module_file == null || item.source_module == null) throw new Exception("annoying get rid of null test");

                List<byte[]> resulting_resources = item.source_module.get_tag_resource_list((Infinite_module_test.module_structs.module.unpacked_module_file)item.module_file);
                foreach (byte[] resource in resulting_resources) {

                    bool is_standalone_resource = resource[0..4].SequenceEqual(new byte[] { 0x75, 0x63, 0x73, 0x68 }); // test for those 4 chars at the top of the file
                    resource_list.Add(new KeyValuePair<byte[], bool>(resource, is_standalone_resource));
            }}catch{
                main.DisplayNote(item.name + " failed to read resources", null, error_level.WARNING);
                return;
            }
            
            // get the resource from the module



            // anomaly check // make sure all entries are of either type, else this will become very difficult to manage
            if (resource_list.Count > 0){
                bool inital = resource_list[0].Value;
                foreach (var resource in resource_list){
                    if (resource.Value != inital){
                        main.DisplayNote(resource + " does not have a matching chunked/non-chunked status!!", null, error_level.WARNING);
            }}}
            
            // we forgot to actually load the tag bytes from module
            // + for some reason we are failing to read resources

            tag test = new tag(resource_list);
            byte[] tagbytes;
            try{
                if (item.module_file == null) throw new Exception("get rid of annoying green line test");
                tagbytes = item.source_module.get_module_file_bytes(item.module_file);
                if (!test.Load_tag_file(tagbytes)){
                    main.DisplayNote(item.name + " was not able to be loaded as a tag", null, error_level.WARNING);
                    return;
            }} catch{ 
                main.DisplayNote(item.name + " returned an error (likely due to file read attempt)", null, error_level.WARNING); return;}

            //DEBUG
            // write original tag to file & resources too
            //File.WriteAllBytes("C:\\Users\\Joe bingle\\Downloads\\tag testing\\og", tagbytes);
            //for (int i = 0; i < resource_list.Count; i++)
            //    File.WriteAllBytes("C:\\Users\\Joe bingle\\Downloads\\tag testing\\og_res_" + i, resource_list[i].Key);

            //tag.compiled_tag testoutput = test.compile();
            //File.WriteAllBytes("C:\\Users\\Joe bingle\\Downloads\\tag testing\\recompiled", testoutput.tag_bytes);
            //for (int i = 0; i < testoutput.resource_bytes.Count; i++)
            //    File.WriteAllBytes("C:\\Users\\Joe bingle\\Downloads\\tag testing\\recompiled_res_" + i, testoutput.resource_bytes[i]);
            


            // load new tag tab here
            TabItem new_tag = new();
            TagInstance tag_interface = new TagInstance(main, item.source_module, test, new_tag, item.alias, tagbytes);
            TagsTabs.Items.Add(new_tag);
            tab_keys.Add(item.name);
            Tabs.Add(item.name, tag_interface);
            tag_interface.LoadTag_UI();
            tag_interface.module_file_header = item.module_file;
            TagsTabs.SelectedIndex = TagsTabs.Items.IndexOf(tag_interface.container);
        }

        private TagInstance get_active_window(){
            // we have to update this when we remove the test tab
            if (TagsTabs.SelectedIndex <= 0) throw new Exception("active window not valid");
            return Tabs[tab_keys[TagsTabs.SelectedIndex - 1]];
        }
        public void UnpackTag() => get_active_window().unpack_to_files();
        public void ExportTag() => get_active_window().export_loaded_to_files();
        public void CommitTag() => get_active_window().commit_changes();
    }
}
