using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace Spir.Indus.ServiceWin.WFDetectDeviceUSB
{

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

        }
        protected override void OnLoad(EventArgs e)
        {
            this.ProgramVisible(false);

            base.OnLoad(e);
        }

        private void ProgramVisible(bool visible)
        {
            if (visible == true)
            {
                // affichage du programme
                Visible = true; // Hide form window.
                ShowInTaskbar = true; // Remove from taskbar.
                Opacity = 100;

                if (this.CanFocus)
                {
                    this.Focus();
                }
            }
            else if (visible == false)
            {
                if (this.statusDetection == StatusDetection.FINISHED && this.statusScan == StatusScan.FINISHED)
                {                    
                    dgLogs.Rows.Clear();
                    dgLogs.Refresh();
                }
                Visible = false;  
                ShowInTaskbar = false; // Remove from taskbar.
                Opacity = 0;
            }
        }
        
        private void ProgramRefresh()
        {
            dgLogs.Rows.Clear();
            dgLogs.Refresh();
        }

        enum StatusDetection { WAITING, DETECTED, SCANNING, FINISHED };
        enum StatusScan { WAITING, STARTED, SCANNING, FINISHED };
        StatusDetection statusDetection = StatusDetection.WAITING;
        StatusScan statusScan = StatusScan.WAITING;
        BroadcastHeader lBroadcastHeader;
        Volume lVolume;
        DeviceEvent lEvent;

        string letter = "";
        string pathSEP = @"C:\Program Files (x86)\Symantec\Symantec Endpoint Protection\doscan.exe";
        string pathLogsSEP = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Symantec\Symantec Endpoint Protection\Logs\";
        string fileLogDatenow = "";
        string filename = "";
        string filenamecp = "";

        #region API constants and structures

        /// <summary>
        /// Constant defined for the WM_DEVICECHANGE message in WinUser.h
        /// </summary>
        const int WM_DEVICECHANGE = 0x0219;

        /// <summary>
        /// Constants and structs defined in DBT.h
        /// </summary>
        public enum DeviceEvent : int
        {
            Arrival = 0x8000,           //DBT_DEVICEARRIVAL
            QueryRemove = 0x8001,       //DBT_DEVICEQUERYREMOVE
            QueryRemoveFailed = 0x8002, //DBT_DEVICEQUERYREMOVEFAILED
            RemovePending = 0x8003,     //DBT_DEVICEREMOVEPENDING
            RemoveComplete = 0x8004,    //DBT_DEVICEREMOVECOMPLETE
            Specific = 0x8005,          //DBT_DEVICEREMOVECOMPLETE
            Custom = 0x8006             //DBT_CUSTOMEVENT
        }

        public enum DeviceType : int
        {
            OEM = 0x00000000,           //DBT_DEVTYP_OEM
            DeviceNode = 0x00000001,    //DBT_DEVTYP_DEVNODE
            Volume = 0x00000002,        //DBT_DEVTYP_VOLUME
            Port = 0x00000003,          //DBT_DEVTYP_PORT
            Net = 0x00000004            //DBT_DEVTYP_NET
        }

        public enum VolumeFlags : int
        {
            Media = 0x0001,             //DBTF_MEDIA
            Net = 0x0002                //DBTF_NET
        }

        public struct BroadcastHeader   //_DEV_BROADCAST_HDR 
        {
            public int Size;            //dbch_size
            public DeviceType Type;     //dbch_devicetype
            private int Reserved;       //dbch_reserved
        }

        public struct Volume            //_DEV_BROADCAST_VOLUME 
        {   
            public int Size;            //dbcv_size
            public DeviceType Type;     //dbcv_devicetype
            private int Reserved;       //dbcv_reserved
            public int Mask;            //dbcv_unitmask
            public int Flags;           //dbcv_flags
        }
        #endregion
        

        protected override void WndProc(ref Message m)
        {
            ////Console.WriteLine("PathSEP: " + pathSEP);
            ////Console.WriteLine("PathLogsSEP: " + pathLogsSEP);

            //if (m.Msg == WM_DEVICECHANGE)
            //{


            if (this.statusDetection == StatusDetection.FINISHED || this.statusScan == StatusScan.FINISHED)
            {
                this.statusDetection = StatusDetection.WAITING;
                this.statusScan = StatusScan.WAITING;
            }

            this.lEvent = (DeviceEvent)m.WParam.ToInt32();

            try
            {
                if (this.lEvent == DeviceEvent.Arrival)
                {
                    // ** Détection d'un périphérique de stockage
                    this.lBroadcastHeader = (BroadcastHeader)Marshal.PtrToStructure(m.LParam, typeof(BroadcastHeader));
                    // On récupère la lettre du lecteur monté
                    this.lVolume = (Volume)Marshal.PtrToStructure(m.LParam, typeof(Volume));
                    this.letter = ToDriveName(this.lVolume.Mask);

                    if (this.lBroadcastHeader.Type == DeviceType.Volume && new DriveInfo(this.letter).DriveType == DriveType.Removable)
                    {
                        //if (this.statusScan == StatusScan.WAITING)
                        //{
                                this.ProgramVisible(true);
                                this.addRow("Périphérique détecté", string.Format("Périphérique détecté sur le lecteur {0}", letter));
                                //Thread.Sleep(1000);
                                this.statusDetection = StatusDetection.DETECTED;
                        //}
                    }
                }

                else if (this.statusDetection == StatusDetection.DETECTED)
                {
                    // ** Lancement du scan usb
                    try
                    {
                        using (Process myProcess = new Process())
                        {
                            myProcess.StartInfo.UseShellExecute = false;
                            myProcess.StartInfo.FileName = string.Format("\"{0}\"", pathSEP);
                            myProcess.StartInfo.Arguments = string.Format("/ScanDir \"{0}\" /C", letter);
                            myProcess.StartInfo.CreateNoWindow = true;
                            myProcess.Start();
                            // myProcess.WaitForExit();
                            //Thread.Sleep(2000);
                        }
                    }
                    catch (Exception e)
                    {
                        this.addRow("Echec", string.Format("Le scan n'a pu être initié.", e.Message));
                    }
                    this.statusScan = StatusScan.STARTED;
                    this.statusDetection = StatusDetection.SCANNING;
                }

                else if (this.statusDetection == StatusDetection.SCANNING)
                {
                    switch (this.statusScan)
                    {
                        case StatusScan.WAITING:

                            break;
                        case StatusScan.STARTED:
                            //Thread.Sleep(1000);
                            this.addRow("Scan en cours", string.Format("Scan en cours sur le lecteur {0}", letter));
                            Thread tDetect = new Thread(() => MessageBox.Show(new Form() { TopMost = true }, string.Format("\nUn appareil a été branché sur le lecteur {0}\nUn scan antivirus a été lancé, merci pour votre vigilance.", letter), "Symantec Endpoint Protection"));
                            tDetect.SetApartmentState(ApartmentState.STA);
                            tDetect.Start();
                            this.statusScan = StatusScan.SCANNING;
                            Thread.Sleep(2000);
                            break;
                        case StatusScan.SCANNING:

                            fileLogDatenow = DateTime.Today.Month.ToString("00") + "" + DateTime.Today.Day.ToString("00") + "" + DateTime.Today.Year;
                            filename = pathLogsSEP + fileLogDatenow + ".Log";
                            filenamecp = filename + "cp";

                            //Thread.Sleep(2000);
                            if (this.checkScanState(filename, filenamecp))
                            {
                                this.statusScan = StatusScan.FINISHED;
                            }

                            //Thread.Sleep(1000);
                            break;
                        case StatusScan.FINISHED:
                            if (this.CanFocus)
                            {
                                this.Focus();
                            }
                            Thread.Sleep(1000);
                            this.statusDetection = StatusDetection.FINISHED;
                            break;
                    }


                }
            }
            catch (Exception e)
            {

            }
            base.WndProc(ref m);
        }

        //private void RestartProgram()
        //{
        //    this.statusDetection = StatusDetection.WAITING;
        //    this.statusScan = StatusScan.WAITING;
        //}

        // Convert to the Drive name (”D:”, “F:”, etc)
        private string ToDriveName(int mask)
        {

            int offset = 0;
            while ((offset < 26) && ((mask & 0x00000001) == 0))
            {
                mask = mask >> 1;
                offset++;
            }

            if (offset < 26)
                return String.Format("{0}:", Convert.ToChar(Convert.ToInt32('A') + offset));

            return "?";
        }

        public void addRow(string status, string message)
        {
            this.dgLogs.Rows.Add(DateTime.Now.ToString("dd/mm HH:mm:ss"), status, message);
        }

        public bool checkScanState(string filename, string filenamecp)
        {
            // TODO : Récupérer le nom du dernier fichier dans pathLogsSEP
            string fileLogDatenow = DateTime.Today.Month.ToString("00") + "" + DateTime.Today.Day.ToString("00") + "" + DateTime.Today.Year;
            File.Copy(filename, filenamecp, true);
            if (File.Exists(filename))
            {
                // TODO : Récupérer le fichier log du jour
                var lines = File.ReadAllLines(filenamecp);

                // TODO : Récupérer le dernier scan complete
                if (!lines[lines.Length - 1].Contains("Scan Complete") && !lines[lines.Length - 1].Contains("Analyse Installation standard"))
                {
                    // TODO : Lire la dernière ligne Scan Complete
                    // Tant que le scan n'est pas terminé
                    File.Copy(filename, filenamecp, true);
                    lines = File.ReadAllLines(filenamecp);
                    return false;
                }
                else
                {
                    //this.ProgramVisible(true);
                    String[] msg_array = lines[lines.Length - 1].Split(',');
                    string msg = "Scan du périphérique termié. " +msg_array[4] + " " + msg_array[5];
                    this.addRow("Succès", string.Format("Scan du périphérique terminé : {0}", msg));
                    this.addRow("Rapport", string.Format("{0}", msg_array[13].Substring(17).Replace('"', ' ')));

                    Thread tEnd = new Thread(() => MessageBox.Show(new Form() { TopMost = true }, string.Format("\nLe scan de votre périphérique est terminé.\n"), "Symantec Endpoint Protection"));
                    tEnd.SetApartmentState(ApartmentState.STA);
                    tEnd.Start();
                    return true;
                }
            }
            else
            {
                // lbMessage.Text += "\nErreur lors du scan " + fileLogDatenow + " (fichier log non trouvé)";
                this.addRow("Echec", string.Format("Erreur lors du scan {0} (fichier log non trouvé)", fileLogDatenow));
                Thread tError = new Thread(() => MessageBox.Show(string.Format("Erreur lors du scan {0} (fichier log non trouvé)", fileLogDatenow), "Symantec Endpoint Protection"));
                tError.SetApartmentState(ApartmentState.STA);
                tError.Start();
                throw new Exception(string.Format("Erreur lors du scan {0} (fichier log non trouvé)", fileLogDatenow));
            }
        }
        
        private void desactive_all_usb_port()
        {
            RegistryKey key;
            key = Registry.LocalMachine.OpenSubKey
                     ("SYSTEM\\CurrentControlSet\\Services\\UsbStor");
            // On empèche le montage d'un autre périphérique durant le scan
            //key.SetValue("Start", 4, RegistryValueKind.DWord);  //disables usb drives
            key.Close();
        }
        private void active_all_usb_port()
        {
            RegistryKey key;
            key = Registry.LocalMachine.OpenSubKey
                     ("SYSTEM\\CurrentControlSet\\Services\\UsbStor");
            // On autorise le montage d'un autre périphérique à la fin du scan
            //key.SetValue("Start", 3, RegistryValueKind.DWord);  //disables usb drives
            key.Close();
        }
        public static bool IsFileReady(string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.ProgramVisible(false);
            }
        }

    }
    
    /// <summary>
    /// Delegate used to implement the class events
    /// </summary>
    public delegate void DeviceVolumeAction(int aMask);

    /// <summary>
    /// Custom exception
    /// </summary>
    public class DeviceVolumeMonitorException : ApplicationException
    {
        public DeviceVolumeMonitorException(string aMessage) : base(aMessage) { }
    }


    //private bool WaitForFile(FileInfo file)
    //{
    //    FileStream stream = null;
    //    bool FileReady = false;
    //    while (!FileReady)
    //    {
    //        try
    //        {
    //            using (stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    //            {
    //                FileReady = true;
    //            }
    //        }
    //        catch (IOException)
    //        {
    //            //File isn't ready yet, so we need to keep on waiting until it is.
    //        }
    //        //We'll want to wait a bit between polls, if the file isn't ready.
    //        if (!FileReady) Thread.Sleep(1000);
    //    }
    //    return FileReady;
    //}
}
