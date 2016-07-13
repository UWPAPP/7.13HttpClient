using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace _7._13HttpClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private CancellationTokenSource cts;
        private const int maxUploadFileSize = 100 * 1024 * 1024;
        public MainPage()
        {
            this.InitializeComponent();
            cts = new CancellationTokenSource();
        }

        //Get请求
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();
            var headers = httpClient.DefaultRequestHeaders;
            string header = "ie";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            header = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            Uri requestUri = new Uri("http://www.contoso.com");

            //Send the GET request asynchronously and retrieve the response as a string.
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
                httpResponse = await httpClient.GetAsync(requestUri);
                httpResponse.EnsureSuccessStatusCode();
                httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
        }

        //下载
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Uri source;
            if (!Uri.TryCreate("http://localhost/BackgroundTransferSample/download.aspx".Trim(), UriKind.Absolute, out source))
            {
                return;
            }

            string destination = "1111.txt".Trim();

            if (string.IsNullOrWhiteSpace(destination))
            {
                return;
            }

            StorageFile destinationFile;
            try
            {
                destinationFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    destination,
                    CreationCollisionOption.GenerateUniqueName);
            }
            catch (FileNotFoundException ex)
            {
                return;
            }

            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(source, destinationFile);

            download.Priority = BackgroundTransferPriority.Default;

            await HandleDownloadAsync(download, true);
        }

        private async Task HandleDownloadAsync(DownloadOperation download, bool start)
        {
            try
            {
                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(DownloadProgress);
                if (start)
                {
                    await download.StartAsync().AsTask(cts.Token, progressCallback);
                }
                else
                {
                    await download.AttachAsync().AsTask(cts.Token, progressCallback);
                }

                ResponseInformation response = download.GetResponseInformation();

                string statusCode = response != null ? response.StatusCode.ToString() : String.Empty;

                Debug.WriteLine("下载完成");
            }
            catch (TaskCanceledException)
            {
                
            }
            catch (Exception ex)
            {
                
            }
            finally
            {

            }
        }

        private void DownloadProgress(DownloadOperation download)
        {
            BackgroundDownloadProgress currentProgress = download.Progress;

            double percent = 100;
            if (currentProgress.TotalBytesToReceive > 0)
            {
                percent = currentProgress.BytesReceived * 100 / currentProgress.TotalBytesToReceive;
                Debug.WriteLine("进度{0}", percent);
            }


            if (currentProgress.HasRestarted)
            {
            }

            if (currentProgress.HasResponseChanged)
            {
                ResponseInformation response = download.GetResponseInformation();
                int headersCount = response != null ? response.Headers.Count : 0;
            }
        }


        //上传
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Uri uri;
            if (!Uri.TryCreate("http://localhost/BackgroundTransferSample/Upload.aspx".Trim(), UriKind.Absolute, out uri))
            {
                return;
            }

            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");

            StartSingleFileUpload(picker, uri);
        }

        private async void StartSingleFileUpload(FileOpenPicker picker, Uri uri)
        {
            StorageFile file = await picker.PickSingleFileAsync();
            UploadSingleFile(uri, file);
        }

        private async void UploadSingleFile(Uri uri, StorageFile file)
        {
            if (file == null)
            {
                return;
            }

            BasicProperties properties = await file.GetBasicPropertiesAsync();
            if (properties.Size > maxUploadFileSize)
            {
                return;
            }

            BackgroundUploader uploader = new BackgroundUploader();
            uploader.SetRequestHeader("Filename", file.Name);

            UploadOperation upload = uploader.CreateUpload(uri, file);

            // Attach progress and completion handlers.
            await HandleUploadAsync(upload, true);
        }
        private async Task HandleUploadAsync(UploadOperation upload, bool start)
        {
            try
            {
                Progress<UploadOperation> progressCallback = new Progress<UploadOperation>(UploadProgress);
                if (start)
                {
                    // Start the upload and attach a progress handler.
                    await upload.StartAsync().AsTask(cts.Token, progressCallback);
                }
                else
                {
                    // The upload was already running when the application started, re-attach the progress handler.
                    await upload.AttachAsync().AsTask(cts.Token, progressCallback);
                }

                ResponseInformation response = upload.GetResponseInformation();

                Debug.WriteLine("上传完成");
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
        }

        private void UploadProgress(UploadOperation upload)
        {
            // UploadOperation.Progress is updated in real-time while the operation is ongoing. Therefore,
            // we must make a local copy so that we can have a consistent view of that ever-changing state
            // throughout this method's lifetime.
            BackgroundUploadProgress currentProgress = upload.Progress;

            double percentSent = 100;
            if (currentProgress.TotalBytesToSend > 0)
            {
                percentSent = currentProgress.BytesSent * 100 / currentProgress.TotalBytesToSend;
                Debug.WriteLine("上传进度:{0}",percentSent);
            }

            if (currentProgress.HasRestarted)
            {
            }

            if (currentProgress.HasResponseChanged)
            {
            }
        }
    }
}
