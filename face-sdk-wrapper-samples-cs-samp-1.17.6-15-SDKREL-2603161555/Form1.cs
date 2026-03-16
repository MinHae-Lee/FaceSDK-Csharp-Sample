using FaceSDKSample.sample;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using static FaceSDKSample.SampleProp.Prop;
using Timer = System.Windows.Forms.Timer;


namespace FaceSDKSample
{
    public partial class Form1 : Form
    {
        SampleProp.Settings settings_ = new SampleProp.Settings();
        SampleProp.DemoClientServerProp demo_prop_ = new SampleProp.DemoClientServerProp();
        SampleProp.SamplePropFaceInfo faceinfo_prop_ = new SampleProp.SamplePropFaceInfo();

        List<CamCtx.CamResResolver.Res> resolved_cap_resolutions_ = new List<CamCtx.CamResResolver.Res>();

        Timer compare_face_label_blink_timer_ = new Timer();

        void CompareFaceLabelBlinkTimer_Tick(object sender, EventArgs e)
        {
            lbl_select_face_cmp_img.Visible = !lbl_select_face_cmp_img.Visible;
        }

        void SetSelectFaceCompareImgLabelBlinkState(bool blink)
        {
            if(blink)
            {
                compare_face_label_blink_timer_.Start();
                lbl_select_face_cmp_img.Visible = true;
            }
            else
            {
                compare_face_label_blink_timer_.Stop();
                lbl_select_face_cmp_img.Visible = false;
            }
        }

        public string last_face_compare_img_path = "";

        private void button4_Click(object sender, EventArgs e)
        {
            if (last_face_compare_img_path == "")
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                last_face_compare_img_path = Path.Combine(exeDir, "imgs\\compare");
            }

            string folderPath = last_face_compare_img_path;

            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            Process.Start("explorer.exe", folderPath);
        }

        public void ReLoadFaceCompareImg(string img_path = "")
        {
            imglst_face_compare.Images.Clear();
            imglst_face_compare.ImageSize = new Size(64, 64);
            imglst_face_compare.ColorDepth = ColorDepth.Depth32Bit;

            lv_face_compare_img.Items.Clear();

            if (img_path == "")
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                img_path = Path.Combine(exeDir, "imgs\\compare");
            }

            if (Directory.Exists(img_path))
            {
                string[] files = Directory.GetFiles(img_path, "*.*", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp")
                    {
                        try
                        {
                            Image img = Image.FromFile(file);
                            const int ExifOrientationId = 0x0112;

                            if (img.PropertyIdList.Contains(ExifOrientationId))
                            {
                                var prop = img.GetPropertyItem(ExifOrientationId);
                                int val = BitConverter.ToUInt16(prop.Value, 0);

                                switch (val)
                                {
                                    case 3:
                                        img.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                        break;
                                    case 6:
                                        img.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                        break;
                                    case 8:
                                        img.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                        break;
                                }

                                img.RemovePropertyItem(ExifOrientationId);
                            }

                            imglst_face_compare.Images.Add(img);

                            string name = Path.GetFileName(file);
                            lv_face_compare_img.Items.Add(new ListViewItem(name, imglst_face_compare.Images.Count - 1));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed To load Image: {file} ({ex.Message})");
                        }
                    }
                }

                last_face_compare_img_path = img_path;
            } 
            else
            {
                last_face_compare_img_path = "";
            }

            settings_.COMPARE_IMG_PATH = last_face_compare_img_path;

            lv_face_compare_img.View = View.LargeIcon;
            lv_face_compare_img.LargeImageList = imglst_face_compare;
            lv_face_compare_img.MultiSelect = false;
        }

        public Form1()
        {
            InitializeComponent();
            Text = "Alchera FaceSDK Sample - " + Sample.NAME + "-" + Sample.REV + "_" + Sample.ID;

            settings_.EXEC_PATH = AppDomain.CurrentDomain.BaseDirectory;

            if (settings_.EXEC_PATH.EndsWith("\\"))
                settings_.EXEC_PATH = settings_.EXEC_PATH.TrimEnd('\\');

            ppg_settings.SelectedObject = settings_;
            ppg_settings.PropertyValueChanged += PPG_Settings_PropertyValueChanged;

            ppg_liveness.SelectedObject = demo_prop_;
            ppg_liveness.PropertyValueChanged += PPG_Liveness_PropertyValueChanged;

            ppg_face_info.SelectedObject = faceinfo_prop_;
            ppg_face_info.PropertyValueChanged += PPG_UI_PropertyValueChanged;

            SampleProp.SamplePropFaceInfo.SetPropertyGridLabelWidth(ppg_face_info, 0);

            ReLoadFaceCompareImg();

            compare_face_label_blink_timer_.Interval = 500;
            compare_face_label_blink_timer_.Tick += CompareFaceLabelBlinkTimer_Tick;
            compare_face_label_blink_timer_.Stop();


        }

        private void PPG_Settings_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            Sample.inst.PostDemoPropEvent(ppg_settings.SelectedObject as SampleProp.Prop,
                "Changed", s, e);

            string propName = e.ChangedItem.PropertyDescriptor.Name;
            object oldValue = e.OldValue;
            object newValue = e.ChangedItem.Value;

            SampleProp.Settings prop_settings
                = ppg_settings.SelectedObject as SampleProp.Settings;

            switch (propName)
            {
                case "CAP_INDEX_RGB":
                case "CAP_WIDTH":
                case "CAP_HEIGHT":
                    {
                        lbl_cap_info.Text = $"Desired: {prop_settings.CAP_WIDTH}x{prop_settings.CAP_HEIGHT}, idx-rgb={prop_settings.CAP_INDEX_RGB}";
                    }
                    break;

                default:
                    break;
            }

      
        }

        

        private void PPG_UI_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            Sample.inst.PostDemoPropEvent(ppg_face_info.SelectedObject as SampleProp.Prop,
                "Changed", s, e);
        }


        private void PPG_Liveness_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            Sample.inst.PostDemoPropEvent(ppg_liveness.SelectedObject as SampleProp.Prop, 
                "Changed", s, e);
        }

        private void btn_load_facesdk_Click(object sender, EventArgs e)
        {
            btn_load_facesdk.Enabled = false;

            try
            {
                Sample.inst.CreateFaceSDKCtx();

                MessageBox.Show("[INFO] Successfully Loaded FaceSDK!, elapsed=" + Sample.fsdk.elapsed_ms_init_ + " MS", 
                    "FaceSDK", MessageBoxButtons.OK);

                btn_select_camera_res.Enabled = true;
                btn_open_camera.Enabled = true;
            }
            catch (Sample.SampleException ex_samp)
            {
                btn_load_facesdk.Enabled = true;

                String err_msg = "[ERR] " + ex_samp.Message;
                err_msg += "\n\nCheck below at model path=" + Sample.inst.GetFaceSDKModelPath();
                err_msg += "\n\n1) license.cer file existence";
                err_msg += "\n2) LicenseActivation(run LicenseGen.exe in model path)";
                
                MessageBox.Show(err_msg, "[ERR] Failed to initialize FaceSDK", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            btn_load_facesdk.Enabled = true;
            btn_open_camera.Enabled = false;
            btn_demo_passive_liveness_best4_feature_faceserver_start.Enabled = false;
            btn_demo_passive_liveness_best4_feature_faceserver_stop.Enabled = false;

            SampleProp.Settings prop_settings = ppg_settings.SelectedObject as SampleProp.Settings;

            lbl_cap_info.Text = $"Desired: {prop_settings.CAP_WIDTH}x{prop_settings.CAP_HEIGHT}, idx-rgb={prop_settings.CAP_INDEX_RGB}";
        }

        private void btn_open_camera_Click(object sender, EventArgs e)
        {
            btn_open_camera.Enabled = false;

            SampleProp.Settings prop_settings = ppg_settings.SelectedObject as SampleProp.Settings;

            int open_cap_index = prop_settings.CAP_INDEX_RGB;
            int open_cap_width = prop_settings.CAP_WIDTH;
            int open_cap_height = prop_settings.CAP_HEIGHT;
            bool open_cap_flip_horizontal = prop_settings.CAP_FLIP_HOR;

            //
            // select resolution
            //

            if (resolved_cap_resolutions_.Count > 0)
            {
                // filter by prefer capture size
                var filter_cap_res = CamCtx.CamResResolver.filter_resolution(
                    resolved_cap_resolutions_,
                    prop_settings.CAP_PREFER_WIDTH, prop_settings.CAP_PREFER_HEIGHT);

                string[] cap_resolutions
                    = CamCtx.CamResResolver.ToStringAry(filter_cap_res);

                if(cap_resolutions.Length == 0)
                {
                    string err_msg = "[ERR] No Suitable Camera Resolution For Cap Prefer Width/Height!";
                    err_msg += $"\n > current prefer cap width={prop_settings.CAP_PREFER_WIDTH}, height={prop_settings.CAP_PREFER_HEIGHT}";
                    err_msg += $"\n\n Please Adjust cap-prefer-width, cap-prefer-height !";

                    MessageBox.Show(err_msg, "[ERR] Camera Resolution", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btn_open_camera.Enabled = true;
                    return;
                }

                string selected_cap_res = ListSelectMsgBox.Show("Select Resolution",
                    "Select Capture Resolution (Filtered By Prefer W=" + prop_settings.CAP_PREFER_WIDTH + ", H=" + prop_settings.CAP_PREFER_HEIGHT + ") ",
                    cap_resolutions);

                if (selected_cap_res == null)
                {
                    btn_open_camera.Enabled = true;
                    return; // canceled selection
                }

                if (!CamCtx.CamResResolver.ParseAsWidthxHeight(selected_cap_res, out open_cap_width, out open_cap_height))
                {
                    String err_msg = "[ERR] invalid cap resoultion string, err=" + selected_cap_res;
                    MessageBox.Show(err_msg, "[ERR] CAMERA OPEN", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    btn_open_camera.Enabled = true;
                    return;
                }
            }

            //
            // open camera
            //

            try
            {                


                Sample.inst.OpenCamera(open_cap_index, open_cap_width, open_cap_height, open_cap_flip_horizontal);

                Sample.inst.GetCaptureInfo(out open_cap_index, out open_cap_width, out open_cap_height);

                MessageBox.Show("[INFO] Successfully Opened Capture Device, capture_idx=" + open_cap_index
                    + "\n > Cap Width=" + open_cap_width + " , Height=" + open_cap_height
                    + "\n > Open Elapsed=" + Sample.cam.elapsed_sec_cam_open_ + " Second",
                    "Cam Capure Index=" + open_cap_index + " is Opened", MessageBoxButtons.OK);

                prop_settings.CAP_WIDTH = open_cap_width;
                prop_settings.CAP_HEIGHT = open_cap_height;
                ppg_settings.Refresh();

                lbl_cap_info.Text = $"Opened: {open_cap_width}x{open_cap_height}, idx-rgb={open_cap_index}";

                SetSelectFaceCompareImgLabelBlinkState(true);
                lv_face_compare_img.Enabled = true;
            }
            catch (Sample.SampleException ex_samp)
            {
                btn_open_camera.Enabled = true;

                String err_msg = "[ERR] " + ex_samp.Message;
                err_msg += "\n camera index=" + open_cap_index;
                err_msg += "\n cap width=" + open_cap_width + ", height=" + open_cap_height;
                err_msg += "\n\nCheck Camera Connection or Camera Index and prefer cap width, height..";

                MessageBox.Show(err_msg, "[ERR] CAMERA OPEN", MessageBoxButtons.OK, MessageBoxIcon.Error);

                btn_open_camera.Enabled = true;
            }

        }

        private void btn_demo_passive_liveness_best4_feature_faceserver_start_Click(object sender, EventArgs e)
        {
            btn_demo_passive_liveness_best4_feature_faceserver_start.Enabled = false;

            Sample.inst.StartDemo(DemoClientServer._NAME,
                () =>
                {
                    if (btn_demo_passive_liveness_best4_feature_faceserver_start.InvokeRequired)
                    {
                        // do not use this.Invoke
                        this.BeginInvoke((MethodInvoker)delegate () {
                            btn_demo_passive_liveness_best4_feature_faceserver_stop.Enabled = false;
                        });
                    }
                    else
                    {
                        btn_demo_passive_liveness_best4_feature_faceserver_stop.Enabled = false;
                    }

                },
                ppg_settings.SelectedObject as SampleProp.Settings,
                ppg_liveness.SelectedObject as SampleProp.DemoClientServerProp,
                ppg_face_info.SelectedObject as SampleProp.SamplePropFaceInfo
                );


            btn_demo_passive_liveness_best4_feature_faceserver_stop.Enabled = true;
        }

        private void StopDemo()
        {
            if (btn_demo_passive_liveness_best4_feature_faceserver_stop.Enabled == false)
                return;

            btn_demo_passive_liveness_best4_feature_faceserver_stop.Enabled = false;

            Sample.inst.StopDemo(DemoClientServer._NAME,
                (_elasped_ms, _status) =>
                {
                    if (exit_msg_box_ == null)
                    {
                        ShowExitMessageBox();
                        while (!exit_msg_box_.IsHandleCreated)
                        {
                            Thread.Sleep(100);
                        }
                    }

                    if (exit_msg_box_ != null)
                    {
                        bool invoke_req = exit_msg_box_.InvokeRequired;

                        switch (_status)
                        {
                            case 0: // program is exited 
                                {
                                    if (invoke_req)
                                    {
                                        exit_msg_box_.BeginInvoke(new Action(() => {
                                            exit_msg_box_.Close();
                                            exit_msg_box_ = null;
                                        }));
                                    }
                                    else
                                    {
                                        exit_msg_box_.Invoke(new Action(() =>
                                        {
                                            exit_msg_box_.Close();
                                            exit_msg_box_ = null;
                                        }));
                                    }                                    
                                }
                                break;

                            case 1:  // exiting..
                                {
                                    if (invoke_req)
                                    {
                                        exit_msg_box_.BeginInvoke(new Action(() => {
                                            if(exit_msg_box_!= null)
                                                exit_msg_box_.Controls["status"].Text = "Elapse Time(Sec)=" + (int)(_elasped_ms / 1000);
                                        }));
                                    }
                                    else
                                    {
                                        exit_msg_box_.Invoke(new Action(() =>
                                        {
                                            if(exit_msg_box_ != null)
                                                exit_msg_box_.Controls["status"].Text = "Elapse Time(Sec)=" + (int)(_elasped_ms / 1000);
                                        }));
                                    }
                                }
                                break;

                            case -1:
                                {
                                    // timeoutted, failed
                                    if (invoke_req)
                                    {
                                        exit_msg_box_.BeginInvoke(new Action(() => {
                                            if (exit_msg_box_ != null)
                                                exit_msg_box_.Controls["status"].Text = "Timeout occured on Exiting.., Elapse Time(Sec)=" + (int)(_elasped_ms / 1000);
                                        }));
                                    }
                                    else
                                    {
                                        exit_msg_box_.Invoke(new Action(() => {
                                            if (exit_msg_box_ != null)
                                                exit_msg_box_.Controls["status"].Text = "Timeout occured on Exiting..,  Elapse Time(Sec)=" + (int)(_elasped_ms / 1000);
                                        }));
                                    }
                                }
                                break;

                            default:
                                break;
                        }
                    }
                },
                30);

            btn_demo_passive_liveness_best4_feature_faceserver_start.Enabled = true;
        }

        private async void btn_demo_passive_liveness_best4_feature_faceserver_stop_Click(object sender, EventArgs e)
        {
            StopDemo();
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void lv_face_compare_img_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lv_face_compare_img.SelectedItems.Count > 0)
            {
                SetSelectFaceCompareImgLabelBlinkState(false);
                lbl_select_face_cmp_img.Visible = true;
                lbl_select_face_cmp_img.Text = "[Compare] " + lv_face_compare_img.SelectedItems[0].Text;                
                btn_demo_passive_liveness_best4_feature_faceserver_start.Enabled = true;

                demo_prop_.COMPARE_SRC_IMG_FILENAME = lv_face_compare_img.SelectedItems[0].Text;
                ppg_liveness.Refresh();
            } 
            else
            {
                SetSelectFaceCompareImgLabelBlinkState(true);
                btn_demo_passive_liveness_best4_feature_faceserver_start.Enabled = false;
            }
        }

        

        private void btn_reload_compare_img_Click(object sender, EventArgs e)
        {
            ReLoadFaceCompareImg(last_face_compare_img_path);
        }

        private void btn_check_camera_res_Click(object sender, EventArgs e)
        {
            btn_select_camera_res.Enabled = false;

            string msg = "";
            SampleProp.Settings prop_settings 
                = ppg_settings.SelectedObject as SampleProp.Settings;

            Stopwatch sw_resolve_resolution = new Stopwatch();
            sw_resolve_resolution.Start();
            var cam_res_lst = CamCtx.CamResResolver.GetAllCapResolutions(prop_settings.CAP_INDEX_RGB);
            sw_resolve_resolution.Stop();
            
            if (cam_res_lst == null || cam_res_lst.Count == 0)
            {
                msg += "[ERR] failed to retrive camera resolution list !!";

                resolved_cap_resolutions_ = new List<CamCtx.CamResResolver.Res>();

                btn_open_camera.Enabled = false;
                btn_select_camera_res.Enabled = true;
            }
            else
            {
                msg += "[INFO] Detected Camera Resolutions)";

                foreach (var res in cam_res_lst)
                {
                    msg += "\n" + $"{res.w} x {res.h}";
                }

                resolved_cap_resolutions_ = cam_res_lst;
                btn_open_camera.Enabled = true;
                btn_select_camera_res.Enabled = false;
            }

            msg += $"\n\n > elapsed time = {sw_resolve_resolution.ElapsedMilliseconds} MS";
            msg += $"\n > cap_index_rgb={prop_settings.CAP_INDEX_RGB} ";
            //msg += $"\n > cap_prefer_w={prop_settings.CAP_PREFER_WIDTH} ";
            //msg += $"\n > cap_prefer_h={prop_settings.CAP_PREFER_HEIGHT} ";

            MessageBox.Show(msg,"FaceSDK - Camera Resolution", MessageBoxButtons.OK);
        }


        //
        // resolution select dialog
        //
        public static class ListSelectMsgBox
        {
            public static string Show(string title, string message, string[] items)
            {
                if(items == null || items.Length == 0) { 
                    return null; 
                }

                Form form = new Form();
                Label label = new Label();
                ListBox listBox = new ListBox();
                Button buttonOk = new Button();
                Button buttonCancel = new Button();

                form.Text = title;
                label.Text = message;

                listBox.Items.AddRange(items);
                listBox.SelectedIndex = 0;
                listBox.SelectionMode = SelectionMode.One;

                buttonOk.Text = "OK";
                buttonCancel.Text = "Cancel";
                buttonOk.DialogResult = DialogResult.OK;
                buttonCancel.DialogResult = DialogResult.Cancel;

                label.SetBounds(9, 10, 372, 13);
                listBox.SetBounds(12, 36, 372, 120);
                buttonOk.SetBounds(228, 165, 75, 23);
                buttonCancel.SetBounds(309, 165, 75, 23);

                label.AutoSize = true;
                form.ClientSize = new System.Drawing.Size(396, 200);
                form.Controls.AddRange(new Control[] { label, listBox, buttonOk, buttonCancel });
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.AcceptButton = buttonOk;
                form.CancelButton = buttonCancel;

                listBox.DoubleClick += (s, e) =>
                {
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };

                var dialogResult = form.ShowDialog();
                if (dialogResult == DialogResult.OK && listBox.SelectedItem != null)
                    return listBox.SelectedItem.ToString();

                return null;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopDemo();
        }

        private Form exit_msg_box_;
        private void ShowExitMessageBox()
        {
            exit_msg_box_ = new Form
            {
                Text = "Exiting..",
                Width = 300,
                Height = 120,
                StartPosition = FormStartPosition.CenterParent
            };

            Label lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = "Exiting demo program.. please wait.."
            };
            exit_msg_box_.Controls.Add(lbl);

            Label lbl_status = new Label
            {
                Name = "status",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = "Elasped Time="
            };
            exit_msg_box_.Controls.Add(lbl_status);

            new Thread(() => Application.Run(exit_msg_box_)).Start();
        }

        //
        // Record
        //

        private void btn_rec_start_Click(object sender, EventArgs e)
        {
            btn_rec_start.Enabled = false;

            string rec_path = GetRecPath();

            if (rec_path == "")
            {
                MessageBox.Show(
                    "Can't create rec path=" + rec_path,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                btn_rec_start.Enabled = true;
                return;
            }


            if (!Sample.inst.RecordDemo(DemoClientServer._NAME,
                (_siz_vid, _file_path, _status) =>
                {
                    if (lbl_rec_st == null)
                    {
                        return;
                    }

                    if (lbl_rec_st.InvokeRequired)
                    {
                        // do not use this.Invoke
                        this.BeginInvoke((MethodInvoker)delegate () {
                            lbl_rec_st.Text = $"siz: {_siz_vid} MB, {_file_path}";
                        });
                    }
                    else
                    {
                        lbl_rec_st.Text = $"siz: {_siz_vid} MB, {_file_path}";
                    }

                    if (_status == -1)
                    {
                        if (btn_rec_stop.InvokeRequired)
                        {
                            this.BeginInvoke((MethodInvoker)delegate () {
                                btn_rec_stop.PerformClick();
                            });
                        }
                        else
                        {
                            btn_rec_stop.PerformClick();
                        }
                    }

                },
                "start",
                ppg_settings.SelectedObject as SampleProp.Settings
                ))
            {
                btn_rec_start.Enabled = true;
                btn_rec_stop.Enabled = false;
                return;
            }

            btn_rec_start.Enabled = false;
            btn_rec_stop.Enabled = true;
        }

        private void _stop_record()
        {
            Sample.inst.RecordDemo(DemoClientServer._NAME,
                /*(_siz_vid, _file_path, _status) =>
                {
                    if (lbl_rec_st == null)
                    {
                        return;
                    }

                    bool lbl_rec_st_invoke_req = lbl_rec_st.InvokeRequired;

                    //switch (_status)
                    //{
                    //} 

                }*/
                null,
                "stop",
                ppg_settings.SelectedObject as SampleProp.Settings
                );
        }

        private void btn_rec_stop_Click(object sender, EventArgs e)
        {
            btn_rec_stop.Enabled = false;
            _stop_record();
            btn_rec_start.Enabled = true;
        }

        private void btn_rec_open_folder_Click(object sender, EventArgs e)
        {
            string rec_path = GetRecPath();

            if (rec_path == "")
            {
                MessageBox.Show(
                    "Can't create rec path=" + rec_path,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            Process.Start("explorer.exe", rec_path);
        }

        private string GetRecPath(bool create_on_not_exist = true)
        {
            string rec_path = settings_.EXEC_PATH + "\\" + settings_.REC_PATH;

            if (!System.IO.Directory.Exists(rec_path))
            {
                if (create_on_not_exist)
                    System.IO.Directory.CreateDirectory(rec_path);
            }

            if (!System.IO.Directory.Exists(rec_path))
            {
                return "";
            }

            return rec_path;
        }

        private void btn_get_img_score_Click(object sender, EventArgs e)
        {
            if(Sample.fsdk == null)
            {
                MessageBox.Show("Press Load FaceSDK First", "ERROR", MessageBoxButtons.OK);
                return;
            }

            SampleProp.Settings prop_settings = settings_;
            SampleProp.DemoClientServerProp prop_demo = demo_prop_;

            string img_path = prop_settings.COMPARE_IMG_PATH + "\\"
                       + prop_demo.COMPARE_SRC_IMG_FILENAME;

            float liv_score;
            float fas_score;

            string err_desc;

            bool is_ok;

            is_ok = Sample.ImgScore_GetScore(out err_desc, out fas_score,
                out liv_score, img_path);

            string filename = prop_demo.COMPARE_SRC_IMG_FILENAME;
            string title = "Img Scores";
            string msg = "file: " + filename;

            if (is_ok)
            {
                title = "[OK] " + title;
                msg = "[OK] " + msg
                    + "\n"
                    + "\nFAS Antispoofing Score=" + fas_score
                    + "\nBGR Liveness Score=" + liv_score;
            }
            else
            {
                title = "[ERR] " + title;
                msg = "[ERR] " + msg + "\nFailed To Get Score \n" + err_desc;
            }

            MessageBox.Show(msg, title, MessageBoxButtons.OK);
        }

        private void btn_feature_explorer_Click(object sender, EventArgs e)
        {
            SampleProp.Settings prop_settings = settings_;
            string startup_path = prop_settings.COMPARE_IMG_PATH;

            FeatureExplorer.show_dlg(this, startup_path);
        }
    }
}
