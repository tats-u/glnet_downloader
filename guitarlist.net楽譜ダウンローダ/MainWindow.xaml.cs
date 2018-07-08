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

using WebClient = System.Net.WebClient;
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using Directory = System.IO.Directory;
using System.IO.Compression;

namespace guitarlist.net楽譜ダウンローダ
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string guitarlistdotnet = "http://guitarlist.net/bandscore/";
        private bool downloadingnow = false; //ダウンロード中か 
        public MainWindow()
        {
            InitializeComponent();
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            //if (downloadingnow) DownloadCancel();
            //else
            Download();
        }

        async private void Download()
        {
            if (downloadingnow == false)
            {
                DownloadButton.IsEnabled = false; downloadingnow = true;
                await DownloadExec();
                DownloadButton.IsEnabled = true; downloadingnow = false;
            }
        }

        //private void DownloadCancel()
        //{

        //    PrgrsBr.Value = 0.0;
        //    Progress.Content = "キャンセルされました";
        //}


        async private Task DownloadExec()
        {
            string URL = URLTB.Text;
            string destpath = Destination.Text;

            bool createnewdir = WhetherCreateDirectory.IsChecked ?? false;
            bool createddir = false;

            ///URLのチェック///
            if (URL.Length == 0) goto noURL;
            if (URL.IndexOf(guitarlistdotnet) != 0) goto invalidURL;
            string[] tokens = URL.Substring(guitarlistdotnet.Length).Split('/');
            if (tokens.Length != 3) goto invalidURL;
            string[] tokens2 = tokens[2].Split('.');
            if (tokens2.Length != 2) goto invalidURL;
            if (tokens2[1] != "php" || (tokens2[0] != tokens[1] && tokens2[0] != (tokens[1] + "2"))) goto invalidURL;

            ///URLのエラー///
            goto validURL;
        noURL:
            MessageBox.Show("URLを入力してください。", "URL入力なし", MessageBoxButton.OK, MessageBoxImage.Error);
            Progress.Content = "エラー";
            URLTB.Focus();
            return;
        invalidURL:
            MessageBox.Show("URLが不正です。", "不正なURL", MessageBoxButton.OK, MessageBoxImage.Error);
            Progress.Content = "エラー";
            URLTB.Focus();
            return;
        validURL:

            ///保存先のチェック///
            if (destpath.Length == 0)
            {
                MessageBox.Show("保存先を入力してください。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Progress.Content = "エラー";
                Destination.Focus();
                return;
            }
            if (createnewdir)
            {
                if (!Directory.Exists(destpath))
                {
                    MessageBox.Show("不正なディレクトリ名です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    Progress.Content = "エラー";
                    Destination.Focus();
                    return;
                }
                if (destpath[destpath.Length - 1] != '\\')
                {
                    destpath += '\\';
                }
                destpath += tokens[1];
                destpath += '\\';
                if(!Directory.Exists(destpath))
                {
                    try
                    {
                        Directory.CreateDirectory(destpath);
                        createddir = true;
                    } catch(Exception)
                    {
                        MessageBox.Show("フォルダを作成できませんでした。", "フォルダ作成エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        Progress.Content = "エラー";
                        Destination.Focus();
                        return;
                    }
                }
            }
            else
            {
                if (!Directory.Exists(destpath))
                {
                    try
                    {
                        if (!Directory.GetParent(destpath).Exists)
                            throw new Exception();
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("不正なディレクトリ名です。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                        Progress.Content = "エラー";
                        Destination.Focus();
                        return;
                    }
                    if (MessageBox.Show("フォルダが存在しません。新規作成しますか？", "フォルダなし", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Directory.CreateDirectory(destpath);
                            createddir = true;
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("フォルダを作成できませんでした。", "フォルダ作成エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                            Progress.Content = "エラー";
                            Destination.Focus();
                            return;
                        }
                    }
                    else
                    {
                        Progress.Content = "エラー";
                        return;
                    }
                }
            }

            ///前処理///
            string targetdir = guitarlistdotnet + tokens[0] + '/' + tokens[1] + '/';
            string jsaddr = targetdir + tokens[1] + ".js";
            string imgdir = targetdir + "img/";
            string JSFile;
            string JSLine;
            string searchstr = "photo[pn++]=\"" + imgdir;
            if (destpath[destpath.Length - 1] != '\\')
            {
                destpath += '\\';
            }

            ///// JavaScriptファイルを解析して画像点数を割り出す /////
            Progress.Content = "画像枚数確認中・・・";
            WebClient wc = new WebClient();
            try
            {
                JSFile = await wc.DownloadStringTaskAsync(jsaddr); //javascriptファイルをダウンロード
            }
            catch (Exception)
            {
                MessageBox.Show("JavaScriptファイルがダウンロードできなかったため、画像枚数を確認できませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Progress.Content = "エラー";
                wc.Dispose();
                if (createddir) Directory.Delete(destpath);
                return;
            }

            ///// 画像枚数を数え上げる /////
            int imgnum = 0;
            var sr = new System.IO.StringReader(JSFile);
            while ((JSLine = await sr.ReadLineAsync()) != null)
            {
                if (JSLine.IndexOf(searchstr) == 0) imgnum++;
            }
            if (imgnum == 0)
            {
                MessageBox.Show("JavaScriptファイルの解析が上手く行かず、画像枚数を確認できませんでした。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Progress.Content = "エラー";
                wc.Dispose();
                if (createddir) Directory.Delete(destpath);
                return;
            }
            string fmt = "D" + (keta(imgnum).ToString());
            PrgrsBr.Maximum = imgnum;


            ///// 画像をダウンロード /////
            try {
                for (int i = 1; i <= imgnum; i++)
                {
                    Progress.Content = "画像ダウンロード中・・・(" + (i.ToString()) + "/" + (imgnum.ToString()) + "枚)";
                    string filename = (i.ToString()) + ".png";
                    string filenamezr = (i.ToString(fmt)) + ".png";
                    await wc.DownloadFileTaskAsync(imgdir + filename, destpath + filenamezr);
                    PrgrsBr.Value += 1.0;
                }
            }
            catch(Exception)
            {
                MessageBox.Show("画像ダウンロード中にエラーが発生しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Progress.Content = "エラー";
                PrgrsBr.Value = 0.0;
                wc.Dispose();
                if (createddir) Directory.Delete(destpath, true);
                return;
            }
            wc.Dispose();
            PrgrsBr.Value = 0.0;
            Progress.Content = "ダウンロード完了！";
            if (MessageBox.Show("ダンロードが完了しました。フォルダを開きますか？", "ダウンロード完了", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(destpath);
            }
        }


        private void DestButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Destination.Text = dialog.SelectedPath;
            }
        }
        private int keta(int num)
        {
            int ret = 1;
            while ((num /= 10) != 0)
            {
                ret++;
            }
            return ret;
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Download();
            }
            //else if (e.Key == Key.Escape && downloadingnow)
            //{
            //    DownloadCancel();
            //}
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (downloadingnow && MessageBox.Show("現在ダウンロード中ですが、本当に終了しますか？", "終了確認", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                e.Cancel = true;
        }
    }
}

