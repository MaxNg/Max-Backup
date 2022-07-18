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
using System.IO.Compression;
using System.Windows.Forms;
using System.IO;
using Path = System.IO.Path;
using System.ComponentModel;


public static class DateTimeExtensions
{
    public static DateTime RoundToMinutes(this DateTime target, int minutes = 1) => RoundToTicks(target, minutes * TimeSpan.TicksPerMinute);
    public static DateTime RoundToTicks(this DateTime target, long ticks) => new DateTime((target.Ticks + ticks / 2) / ticks * ticks, target.Kind);
}


namespace Backup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly BackgroundWorker bgWorker = new BackgroundWorker();
        //private ZipArchive archive;
        //DateTime dt = DateTime.Now;
        private readonly StreamWriter resultFile = new StreamWriter("log.txt", false);

        public MainWindow()
        {
            InitializeComponent();
            bgWorker.WorkerReportsProgress = true;
            bgWorker.WorkerSupportsCancellation = true;
            bgWorker.ProgressChanged += BgWorker_ProgressChanged;
            bgWorker.RunWorkerCompleted += BgWorker_RunWorkerCompleted;
            bgWorker.DoWork += BgWorker_DoWork;
        }
        private void BgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DateTime dt = DateTime.Now;
            System.Windows.Forms.MessageBox.Show("Work is completed.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //archive.Dispose();
            //If updating archive, update filename with today date
            // remove date from zip filename
            if (!(bool)RdBtnBackup.IsChecked)
            {
                string zipName = TxtArchiveFile.Text.Substring(0, TxtArchiveFile.Text.IndexOf("-"));
                //rename zip file
                string zipFileName = zipName + "-" + dt.ToString("d") + ".zip";
                FileInfo fileZip = new FileInfo(TxtArchiveFile.Text);
                fileZip.MoveTo(zipFileName);
            }
            resultFile.Close();
            ProgressBar.Value = 0;
        }

        private void BgWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            DateTime dt = DateTime.Now;
            int numFiles = 0;
            int percentComplete = 0;
            string path = "";
            bool fullBackUp = true;
            string zipFileName = "";
            string zipPath = "";
            this.Dispatcher.Invoke(() =>
            {
                path = TxtFolder.Text;
                zipPath = TxtArchiveFolder.Text;
                zipFileName = TxtArchiveFile.Text;
                if (!(bool)RdBtnBackup.IsChecked)
                {
                    fullBackUp = false;
                }
            });
            ZipArchive archive;

            if (fullBackUp)
            {
                zipFileName = zipPath + "\\" + Path.GetFileName(path) + "-" + dt.ToString("d") + ".zip";
                archive = ZipFile.Open(zipFileName, ZipArchiveMode.Create);
            }
            else
            {
                archive = ZipFile.Open(zipFileName, ZipArchiveMode.Update);
            }

            int fileCount = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Count();

            //Is checked, do full backup else update archive
            if (fullBackUp)
            {
                BackgroundWorker worker = sender as BackgroundWorker;

                var folders = new Stack<string>();
                CompressionLevel compressionLevel = CompressionLevel.Fastest;

                folders.Push(path);
                do
                {
                    var currentFolder = folders.Pop();
                    foreach (var item in Directory.GetFiles(currentFolder))
                    {
                        archive.CreateEntryFromFile(item, item.Substring(path.Length + 1), compressionLevel);
                        resultFile.Write(item + "\n");
                        numFiles++;
                        percentComplete = (int)((float)numFiles / (float)fileCount * 100);
                        bgWorker.ReportProgress(percentComplete);
                    }

                    foreach (var item in Directory.GetDirectories(currentFolder))
                    {
                        folders.Push(item);
                    }
                }
                while (folders.Count > 0);

            }
            else
            {
                var folders = new Stack<string>();

                //string folderPath = @"D:\Guitar Practice\GuitarPro";

                folders.Push(path);
                do
                {
                    var currentFolder = folders.Pop();
                    foreach (string item in Directory.GetFiles(currentFolder))
                    {
                        numFiles++;

                        // Compare files and zip new and modified files
                        ZipArchiveEntry entry = archive.GetEntry(item.Substring(path.Length + 1));

                        //ZipArchiveEntry entry = archive.GetEntry(item.Substring(4));
                        if (entry == null)
                        {
                            //add to zip
                            //AddToZip(archive, path, item);
                            archive.CreateEntryFromFile(item, item.Substring(path.Length + 1));
                            resultFile.Write(item + " - new" + "\n");
                        }
                        else if (DateTime.Compare(DateTimeExtensions.RoundToMinutes(entry.LastWriteTime.DateTime), DateTimeExtensions.RoundToMinutes(File.GetLastWriteTime(item))) < 0)
                        {
                            //Update zip
                            //Delete entry and add the new file
                            entry.Delete();
                            //AddToZip(archive, path, item);
                            archive.CreateEntryFromFile(item, item.Substring(path.Length + 1));
                            resultFile.Write(item + " - modified" + "\n");
                        };
                        // zip.CreateEntryFromFile(item, item.Substring(path.Length + 1), compressionLevel);
                        percentComplete = (int)((float)numFiles / (float)fileCount * 100);
                        bgWorker.ReportProgress(percentComplete);
                    }

                    foreach (var item in Directory.GetDirectories(currentFolder))
                    {
                        folders.Push(item);
                    }
                }
                while (folders.Count > 0);
            }
            archive.Dispose();
            this.Dispatcher.Invoke(() =>
            {
                TxTBoxTotalFiles.Text = numFiles.ToString();
            });
        }

        private void BgWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressBar.Value = e.ProgressPercentage;
        }

        private void BtnArchiveFile_Click(object sender, RoutedEventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Select your zip.";
                ofd.Filter = "zip files (*.zip)|*.zip";
                DialogResult result = ofd.ShowDialog();
                TxtArchiveFile.Text = ofd.FileName;
            };

        }

        internal bool CheckAllInput()
        {
            if (string.IsNullOrEmpty(TxtFolder.Text))
            {
                System.Windows.Forms.MessageBox.Show("Please select your folder to archive", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtFolder.Focus();
                return false;
            }

            if ((bool)RdBtnBackup.IsChecked)
            {
                if (string.IsNullOrEmpty(TxtArchiveFolder.Text))
                {
                    System.Windows.Forms.MessageBox.Show("Please select the folder where the zip file will be saved.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtArchiveFolder.Focus();
                    return false;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(TxtArchiveFile.Text))
                {
                    System.Windows.Forms.MessageBox.Show("Please select your archive file.", "Message", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtArchiveFile.Focus();
                    return false;
                }
            }

            return true;
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!CheckAllInput())
            {
                return;
            }

            ProgressBar.Minimum = 0;
            ProgressBar.Maximum = 100;

            //Make ProgessBar Visible
            ProgressBar.Visibility = Visibility.Visible;

            if (!bgWorker.IsBusy)
                bgWorker.RunWorkerAsync();

        }

        private void BtnArchive_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog afbd = new FolderBrowserDialog())
            {
                DialogResult res = afbd.ShowDialog();
                TxtArchiveFolder.Text = afbd.SelectedPath;
            };
        }

        private void BtnFolder_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = fbd.ShowDialog();
                TxtFolder.Text = fbd.SelectedPath;
            };
        }

        private void RdBtnUpdate_Checked(object sender, RoutedEventArgs e)
        {

            LblArchiveFile.Visibility = Visibility.Visible;
            TxtArchiveFile.Visibility = Visibility.Visible;
            BtnArchiveFile.Visibility = Visibility.Visible;

            LblArchiveFolder.Visibility = Visibility.Collapsed;
            TxtArchiveFolder.Visibility = Visibility.Collapsed;
            BtnArchive.Visibility = Visibility.Collapsed;
        }

        private void RdBtnBackup_Checked(object sender, RoutedEventArgs e)
        {
            LblArchiveFile.Visibility = Visibility.Collapsed;
            TxtArchiveFile.Visibility = Visibility.Collapsed;
            BtnArchiveFile.Visibility = Visibility.Collapsed;

            LblArchiveFolder.Visibility = Visibility.Visible;
            TxtArchiveFolder.Visibility = Visibility.Visible;
            BtnArchive.Visibility = Visibility.Visible;
        }

    }
}
