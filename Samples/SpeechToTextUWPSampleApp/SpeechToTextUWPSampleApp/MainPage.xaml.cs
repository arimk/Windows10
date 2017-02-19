﻿//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

namespace SpeechToTextUWPSampleApp
{
    /// <summary>
    /// Main page for the application.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Windows.Media.Playback.MediaPlayer mediaPlayer;
        SpeechToTextClient client;
        string[] LanguageArray = 
            {"ca-ES","de-DE","zh-TW", "zh-HK","ru-RU","es-ES", "ja-JP","ar-EG", "da-DK","en-AU" ,"en-CA","en-GB" ,"en-IN", "en-US" , "en-NZ","es-MX","fi-FI",
              "fr-FR","fr-CA" ,"it-IT","ko-KR" , "nb-NO","nl-NL","pt-BR" ,"pt-PT"  ,             
              "pl-PL"  ,"sv-SE", "zh-CN"  };
        public MainPage()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // TODO: If your application contains multiple pages, ensure that you are
            // handling the hardware Back button by registering for the
            // Windows.Phone.UI.Input.HardwareButtons.BackPressed event.
            // If you are using the NavigationHelper provided by some templates,
            // this event is handled for you.
            LogMessage("MainPage OnNavigatedTo");
            ApplicationView.GetForCurrentView().SetPreferredMinSize(new Size
            {
                Height = 436,
                Width = 320
            });
            // Hide Systray on phone
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                // Hide Status bar
                var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                await statusBar.HideAsync();
            }

            // Logs event to refresh the TextBox
            logs.TextChanged += Logs_TextChanged;

            // Bind player to element
            mediaPlayer = new Windows.Media.Playback.MediaPlayer();
            mediaPlayerElement.SetMediaPlayer(mediaPlayer);
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

            language.Items.Clear();
            foreach(var l in LanguageArray)
                language.Items.Add(l);
            language.SelectedItem = "en-US";

            // Get Subscription ID from the local settings
            ReadSettings();
            
            // Update control and play first video
            UpdateControls();
            convertAudioButton.Focus(FocusState.Programmatic);

            
            // Register Suspend/Resume
            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
            
            // Display OS, Device information
            LogMessage(Information.SystemInformation.GetString());
            
            // Create Cognitive Service SpeechToText Client
            client = new SpeechToTextClient();

            // Cognitive Service SpeechToText GetToken 
            if (!string.IsNullOrEmpty(subscriptionKey.Text))
            {
                string s = await client.GetToken(subscriptionKey.Text);
                if (!string.IsNullOrEmpty(s))
                    LogMessage("GetToken for subscription Key: " + subscriptionKey.Text);
                else
                    LogMessage("GetToken failed for subscription Key: " + subscriptionKey.Text);
            }

        }
        /// <summary>
        /// Method OnNavigatedFrom
        /// </summary>
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            LogMessage("MainPage OnNavigatedFrom");
            // Unregister Suspend/Resume
            Application.Current.Suspending -= Current_Suspending;
            Application.Current.Resuming -= Current_Resuming;
        }
        /// <summary>
        /// This method is called when the application is resuming
        /// </summary>
        void Current_Resuming(object sender, object e)
        {
            LogMessage("Resuming");
            //ReadSettings();

            // Resotre Playback Rate
            if (mediaPlayer.PlaybackSession.PlaybackRate != 1)
                mediaPlayer.PlaybackSession.PlaybackRate = 1;
        }
        /// <summary>
        /// This method is called when the application is suspending
        /// </summary>
        async void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            LogMessage("Suspending");
            var deferal = e.SuspendingOperation.GetDeferral();
            if (client.IsRecording())
            {
                LogMessage("Stop Recording...");
                await client.StopRecording();
            }
            SaveSettings();
            deferal.Complete();
        }

        #region Settings
        const string keySubscription = "subscriptionKey";
        /// <summary>
        /// Function to save all the persistent attributes
        /// </summary>
        public bool SaveSettings()
        {
            SaveSettingsValue(keySubscription,subscriptionKey.Text);
            return true;
        }
        /// <summary>
        /// Function to read all the persistent attributes
        /// </summary>
        public bool ReadSettings()
        {
            string s = ReadSettingsValue(keySubscription) as string;
            if (!string.IsNullOrEmpty(s))
                subscriptionKey.Text = s;
            return true;
        }
        /// <summary>
        /// Function to read a setting value and clear it after reading it
        /// </summary>
        public static object ReadSettingsValue(string key)
        {
            if (!Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
            {
                return null;
            }
            else
            {
                var value = Windows.Storage.ApplicationData.Current.LocalSettings.Values[key];
                Windows.Storage.ApplicationData.Current.LocalSettings.Values.Remove(key);
                return value;
            }
        }

        /// <summary>
        /// Save a key value pair in settings. Create if it doesn't exist
        /// </summary>
        public static void SaveSettingsValue(string key, object value)
        {
            if (!Windows.Storage.ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
            {
                Windows.Storage.ApplicationData.Current.LocalSettings.Values.Add(key, value);
            }
            else
            {
                Windows.Storage.ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }
        #endregion Settings

        #region Logs
        void PushMessage(string Message)
        {
            App app = Windows.UI.Xaml.Application.Current as App;
            if (app != null)
                app.MessageList.Enqueue(Message);
        }
        bool PopMessage(out string Message)
        {
            Message = string.Empty;
            App app = Windows.UI.Xaml.Application.Current as App;
            if (app != null)
                return app.MessageList.TryDequeue(out Message);
            return false;
        }
        /// <summary>
        /// Display Message on the application page
        /// </summary>
        /// <param name="Message">String to display</param>
        async void LogMessage(string Message)
        {
            string Text = string.Format("{0:d/M/yyyy HH:mm:ss.fff}", DateTime.Now) + " " + Message + "\n";
            PushMessage(Text);
            System.Diagnostics.Debug.WriteLine(Text);
            await DisplayLogMessage();
        }
        /// <summary>
        /// Display Message on the application page
        /// </summary>
        /// <param name="Message">String to display</param>
        async System.Threading.Tasks.Task<bool> DisplayLogMessage()
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {

                    string result;
                    while (PopMessage(out result))
                    {
                        logs.Text += result;
                        if (logs.Text.Length > 16000)
                        {
                            string LocalString = logs.Text;
                            while (LocalString.Length > 12000)
                            {
                                int pos = LocalString.IndexOf('\n');
                                if (pos == -1)
                                    pos = LocalString.IndexOf('\r');


                                if ((pos >= 0) && (pos < LocalString.Length))
                                {
                                    LocalString = LocalString.Substring(pos + 1);
                                }
                                else
                                    break;
                            }
                            logs.Text = LocalString;
                        }
                    }
                }
            );
            return true;
        }
        /// <summary>
        /// This method is called when the content of the Logs TextBox changed  
        /// The method scroll to the bottom of the TextBox
        /// </summary>
        void Logs_TextChanged(object sender, TextChangedEventArgs e)
        {
            //  logs.Focus(FocusState.Programmatic);
            // logs.Select(logs.Text.Length, 0);
            var tbsv = GetFirstDescendantScrollViewer(logs);
            tbsv.ChangeView(null, tbsv.ScrollableHeight, null, true);
        }
        /// <summary>
        /// Retrieve the ScrollViewer associated with a control  
        /// </summary>
        ScrollViewer GetFirstDescendantScrollViewer(DependencyObject parent)
        {
            var c = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < c; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var sv = child as ScrollViewer;
                if (sv != null)
                    return sv;
                sv = GetFirstDescendantScrollViewer(child);
                if (sv != null)
                    return sv;
            }

            return null;
        }
        #endregion

        #region Media
        /// <summary>
        /// Mute method 
        /// </summary>
        private void mute_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Toggle Mute");
            mediaPlayer.IsMuted = !mediaPlayer.IsMuted;
            UpdateControls();
        }
        /// <summary>
        /// Volume Up method 
        /// </summary>
        private void volumeUp_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Volume Up");
            mediaPlayer.Volume = (mediaPlayer.Volume + 0.10 <= 1 ? mediaPlayer.Volume + 0.10 : 1);
            UpdateControls();
        }
        /// <summary>
        /// Volume Down method 
        /// </summary>
        private void volumeDown_Click(object sender, RoutedEventArgs e)
        {
            LogMessage("Volume Down");
            mediaPlayer.Volume = (mediaPlayer.Volume - 0.10 >= 0 ? mediaPlayer.Volume - 0.10 : 0);
            UpdateControls();
        }

        /// <summary>
        /// GetFileFromLocalPathUrl
        /// Return the StorageFile associated with the url  
        /// </summary>
        /// <param name="PosterUrl">Url string of the content</param>
        /// <returns>StorageFile</returns>
        private async System.Threading.Tasks.Task<Windows.Storage.StorageFile> GetFileFromLocalPathUrl(string PosterUrl)
        {
            string path = null;
            Windows.Storage.StorageFolder folder = null;
            if (PosterUrl.ToLower().StartsWith("picture://"))
            {
                folder = Windows.Storage.KnownFolders.PicturesLibrary;
                path = PosterUrl.Replace("picture://", "");
            }
            else if (PosterUrl.ToLower().StartsWith("music://"))
            {
                folder = Windows.Storage.KnownFolders.MusicLibrary;
                path = PosterUrl.Replace("music://", "");
            }
            else if (PosterUrl.ToLower().StartsWith("video://"))
            {
                folder = Windows.Storage.KnownFolders.VideosLibrary;
                path = PosterUrl.Replace("video://", "");
            }
            else if (PosterUrl.ToLower().StartsWith("file://"))
            {
                path = PosterUrl.Replace("file://", "");
            }
            Windows.Storage.StorageFile file = null;
            try
            {
                if (folder != null)
                {
                    string ext = System.IO.Path.GetExtension(path);
                    string filename = System.IO.Path.GetFileName(path);
                    string directory = System.IO.Path.GetDirectoryName(path);
                    while (!string.IsNullOrEmpty(directory))
                    {

                        string subdirectory = directory;
                        int pos = -1;
                        if ((pos = directory.IndexOf('\\')) > 0)
                            subdirectory = directory.Substring(0, pos);
                        folder = await folder.GetFolderAsync(subdirectory);
                        if (folder != null)
                        {
                            if (pos > 0)
                                directory = directory.Substring(pos + 1);
                            else
                                directory = string.Empty;
                        }
                    }
                    if (folder != null)
                        file = await folder.GetFileAsync(filename);
                }
                else
                    file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            }
            catch (Exception e)
            {
                LogMessage("Exception while opening file: " + PosterUrl + " exception: " + e.Message);
            }
            return file;
        }

        /// <summary>
        /// StartPlay
        /// Start to play pictures, audio content or video content
        /// </summary>
        /// <param name="content">Url string of the content to play </param>
        /// <returns>true if success</returns>
        private async System.Threading.Tasks.Task<bool> StartPlay(string content)
        {

            try
            {

                bool result = false;
                if (string.IsNullOrEmpty(content))
                {
                    LogMessage("Empty Uri");
                    return result;
                }

                // Stop the current stream
                mediaPlayer.Source = null;
                mediaPlayerElement.PosterSource = null;
                mediaPlayer.AutoPlay = true;
                // if a picture will be displayed
                // display or not popup
                if (result == true)
                {
                    pictureElement.Visibility = Visibility.Visible;
                    mediaPlayerElement.Visibility = Visibility.Collapsed;
                }
                else
                {
                    pictureElement.Visibility = Visibility.Collapsed;
                    mediaPlayerElement.Visibility = Visibility.Visible;
                }
                // Audio or video
                Windows.Storage.StorageFile file = await GetFileFromLocalPathUrl(content);
                if (file != null)
                {
                    mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
                    mediaPlayer.Play();
                    return true;
                }
                else
                    LogMessage("Failed to load media file: " + Content);
            }
            catch (Exception ex)
            {
                LogMessage("Exception Playing: " + ex.Message.ToString());
            }
            return false;
        }

        private void MediaPlayer_MediaOpened(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            LogMessage("MediaPlayer media opened event");
            UpdateControls();

        }

        private void MediaPlayer_MediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
        {
            LogMessage("MediaPlayer media failed event");
            UpdateControls();
        }

        private void MediaPlayer_MediaEnded(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            LogMessage("MediaPlayer media ended event" );
            UpdateControls();
        }

        /// <summary>
        /// This method prepare the MediaElement to play any content (video, audio, pictures): SMOOTH, DASH, HLS, MP4, WMV, MPEG2-TS, JPG, PNG,...
        /// </summary>
        private async void PlayCurrentUrl()
        {

            await StartPlay(mediaUri.Text);
            UpdateControls();
        }

        /// <summary>
        /// Play method which plays the video with the MediaElement from position 0
        /// </summary>
        private void play_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("Start to play " + mediaUri.Text.ToString());
                PlayCurrentUrl();
            }
            catch (Exception ex)
            {
                LogMessage("Failed to play: " + mediaUri.Text + " Exception: " + ex.Message);
            }
        }
        /// <summary>
        /// Stop method which stops the video currently played by the MediaElement
        /// </summary>
        private void stop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(mediaUri.Text)) 
                {
                    LogMessage("Stop " + mediaUri.Text.ToString());
                    //          mediaPlayer.Stop();
                    mediaPlayer.Source = null;
                    UpdateControls();

                }
            }
            catch (Exception ex)
            {
                LogMessage("Failed to stop: " + mediaUri.Text + " Exception: " + ex.Message);
            }
        }
        /// <summary>
        /// Play method which plays the video currently paused by the MediaElement
        /// </summary>
        private void playPause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(mediaUri.Text))
                {
                    LogMessage("Play " + mediaUri.Text.ToString());
                    mediaPlayer.Play();
                    UpdateControls();

                }
            }
            catch (Exception ex)
            {
                LogMessage("Failed to play: " + mediaUri.Text + " Exception: " + ex.Message);
            }
        }
        /// <summary>
        /// Pause method which pauses the video currently played by the MediaElement
        /// </summary>
        private void pausePlay_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(mediaUri.Text))
                {
                    LogMessage("Pause " + mediaUri.Text.ToString());
                    mediaPlayer.Pause();
                    UpdateControls();
                }
            }
            catch (Exception ex)
            {
                LogMessage("Failed to play: " + mediaUri.Text + " Exception: " + ex.Message);
            }
        }

        #endregion Media

        #region LevelAndError
        DateTime LastLevelTime = DateTime.Now;
        double maxValue = 0;
        async void Client_AudioLevel(object sender, double reading)
        {
            if ((DateTime.Now - LastLevelTime).TotalMilliseconds > 200)
            {
                LastLevelTime = DateTime.Now;
                if (maxValue == 0)
                {
                    maxValue = 1;
                    return;
                }
                //LogMessage("Amplitude: " + reading.ToString());

                double value = reading > 32768 ? 32768 : reading;
                if (value > maxValue)
                    maxValue = value;
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () =>
                    {
                        DrawLevel(value);
                    });
            }
        }
        void DrawLevel(double value)
        {
            if (CanvasGraph.Children.Count > 0) CanvasGraph.Children.Clear();
            Windows.UI.Xaml.Shapes.Line line = new Windows.UI.Xaml.Shapes.Line() { X1 = 0, Y1 = CanvasGraph.Height / 2, X2 = ((value * CanvasGraph.Width) / maxValue), Y2 = CanvasGraph.Height / 2 };
            line.StrokeThickness = CanvasGraph.Height;
            line.Stroke = new SolidColorBrush(Windows.UI.Colors.Cyan);
            CanvasGraph.Children.Add(line);
        }
        void ClearLevel()
        {
            if (CanvasGraph.Children.Count > 0) CanvasGraph.Children.Clear();
        }
        private void Client_AudioCaptureError(object sender, string message)
        {
            LogMessage("Audio Capture Error: " + message );
            UpdateControls();
        }
        #endregion LevelAndError

        #region ui
        /// <summary>
        /// UpdateControls Method which update the controls on the page  
        /// </summary>
        async void UpdateControls()
        {

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                 () =>
                 {
                     {
                         if ((client == null) || (!client.IsRecording()))
                         {
                             convertAudioButton.Content = "\xE717";
                             recordAudioButton.Content = "\xE720";
                         }
                         else
                         {
                             convertAudioButton.Content = "\xE778";
                             recordAudioButton.Content = "\xE78C";
                         }
                         openButton.IsEnabled = true;
                         convertAudioButton.IsEnabled = true;
                         recordAudioButton.IsEnabled = true;
                         mediaUri.IsEnabled = true;

                         if (!string.IsNullOrEmpty(mediaUri.Text))
                         {
                             convertWAVButton.IsEnabled = true;

                             playButton.IsEnabled = true;


                             muteButton.IsEnabled = true;
                             volumeDownButton.IsEnabled = true;
                             volumeUpButton.IsEnabled = true;

                             playPauseButton.IsEnabled = false;
                             pausePlayButton.IsEnabled = false;
                             stopButton.IsEnabled = false;


                             if (mediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                             {
                                 //if (string.Equals(mediaUri.Text, CurrentMediaUrl))
                                 //{
                                 playPauseButton.IsEnabled = false;
                                 pausePlayButton.IsEnabled = true;
                                 stopButton.IsEnabled = true;
                                 //}
                             }
                             else if (mediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Paused)
                             {
                                 playPauseButton.IsEnabled = true;
                                 stopButton.IsEnabled = true;
                             }
                         }
                         // Volume buttons control
                         if (mediaPlayer.IsMuted)
                             muteButton.Content = "\xE767";
                         else
                             muteButton.Content = "\xE74F";
                         if (mediaPlayer.Volume == 0)
                         {
                             volumeDownButton.IsEnabled = false;
                             volumeUpButton.IsEnabled = true;
                         }
                         else if (mediaPlayer.Volume >= 1)
                         {
                             volumeDownButton.IsEnabled = true;
                             volumeUpButton.IsEnabled = false;
                         }
                         else
                         {
                             volumeDownButton.IsEnabled = true;
                             volumeUpButton.IsEnabled = true;
                         }
                     }
                 });
        }

        /// <summary>
        /// sendAudioBuffer method which :
        /// - record audio sample in the buffer
        /// - send the buffer to SpeechToText REST API once the recording is done
        /// </summary>
        private async void sendAudioBuffer_Click(object sender, RoutedEventArgs e)
        {
            if(client!=null)
            {
                if((!client.HasToken())&&(!string.IsNullOrEmpty(subscriptionKey.Text)))
                {
                    string token = await client.GetToken(subscriptionKey.Text);
                    if (string.IsNullOrEmpty(token))
                    {
                        // Save subscription key
                        SaveSettings();
                    }
                }
                if (client.HasToken())
                {
                    if (client.IsRecording() == false)
                    {
                        if (await client.CleanupRecording())
                        {
                            if (await client.StartRecording())
                            {
                                client.AudioLevel += Client_AudioLevel;
                                client.AudioCaptureError += Client_AudioCaptureError;
                                LogMessage("Start Recording...");
                            }
                            else
                                LogMessage("Start Recording failed");
                        }
                        else
                            LogMessage("CleanupRecording failed");
                    }
                    else
                    {
                        LogMessage("Stop Recording...");
                        await client.StopRecording();
                        client.AudioLevel -= Client_AudioLevel;
                        client.AudioCaptureError -= Client_AudioCaptureError;
                        ClearLevel();
                        string locale = language.SelectedItem.ToString();
                        SpeechToTextResponse result = await client.SendBuffer(locale);
                        if (result != null)
                        {
                            string httpError = result.GetHttpError();
                            if (!string.IsNullOrEmpty(httpError))
                            {
                                resultText.Text = httpError;
                                LogMessage("Http Error: " + httpError.ToString());
                            }
                            else
                            {
                                if (result.Status() == "error")
                                    resultText.Text = "error";
                                else
                                    resultText.Text = result.Result();
                                LogMessage("Result: " + result.ToString());
                            }
                        }
                        else
                            LogMessage("Error while sending buffer");

                    }
                }
                else
                    LogMessage("Authentication failed check your subscription Key: " + subscriptionKey.Text.ToString());
            }
            UpdateControls();

        }
        /// <summary>
        /// recordAudio method which :
        /// - record audio sample in the buffer
        /// - store the buffer in a storagefile on disk once the recording is done
        /// </summary>
        private async void recordAudio_Click(object sender, RoutedEventArgs e)
        {
            if (client != null)
            {
                if (client.IsRecording() == false)
                {
                    if (await client.CleanupRecording())
                    {
                        if (await client.StartRecording())
                        {
                            client.AudioLevel += Client_AudioLevel;
                            client.AudioCaptureError += Client_AudioCaptureError;
                            LogMessage("Start Recording...");
                        }
                        else
                            LogMessage("Start Recording failed");
                    }
                    else
                        LogMessage("CleanupRecording failed");
                }
                else
                {
                    LogMessage("Stop Recording...");
                    await client.StopRecording();
                    client.AudioLevel -= Client_AudioLevel;
                    client.AudioCaptureError -= Client_AudioCaptureError;
                    ClearLevel();
                    if (client.GetBufferLength() > 0)
                    {
                        var filePicker = new Windows.Storage.Pickers.FileSavePicker();
                        filePicker.DefaultFileExtension = ".wav";
                        filePicker.SuggestedFileName = "record.wav";
                        filePicker.FileTypeChoices.Add("WAV files", new List<string>() { ".wav" });
                        filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
                        filePicker.SettingsIdentifier = "WavPicker";
                        filePicker.CommitButtonText = "Save buffer into a WAV File";

                        var wavFile = await filePicker.PickSaveFileAsync();
                        if (wavFile != null)
                        {
                            string fileToken = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(wavFile);
                            if (await client.SaveBuffer(wavFile))
                            {
                                mediaUri.Text = "file://" + wavFile.Path;
                                LogMessage("Record buffer saved in file: " + wavFile.Path.ToString());
                                UpdateControls();
                            }
                            else
                                LogMessage("Error while saving record buffer in file: " + wavFile.Path.ToString());
                        }
                    }
                    else
                        LogMessage("Buffer empty nothing to save" );
                }
            }

            UpdateControls();
        }



        /// <summary>
        /// open method which select a WAV file on disk
        /// </summary>
        private async void open_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
                filePicker.FileTypeFilter.Add(".wav");
                filePicker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
                filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
                filePicker.SettingsIdentifier = "WavPicker";
                filePicker.CommitButtonText = "Open WAV File to Process";

                var wavFile = await filePicker.PickSingleFileAsync();
                if (wavFile != null)
                {
                    string fileToken = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(wavFile);
                    mediaUri.Text = "file://" + wavFile.Path;
                    LogMessage("Selected file: " + mediaUri.Text);
                    UpdateControls();
                }
            }
            catch (Exception ex)
            {
                LogMessage("Failed to select WAV file: Exception: " + ex.Message);
            }
        }
        /// <summary>
        /// sendWAVFile method which :
        /// - sends the audio sample in a WAV file towards the SpeechToText REST API
        /// </summary>
        private async void sendWAVFile_Click(object sender, RoutedEventArgs e)
        {
            if (client != null)
            {
                if ((!client.HasToken()) && (!string.IsNullOrEmpty(subscriptionKey.Text)))
                {
                    string token = await client.GetToken(subscriptionKey.Text);
                    if (string.IsNullOrEmpty(token))
                    {
                        // Save subscription key
                        SaveSettings();
                    }
                }

                if (client.HasToken())
                {
                    string locale = language.SelectedItem.ToString();
                    var file = await GetFileFromLocalPathUrl(mediaUri.Text);
                    if (file != null)
                    {
                        string convertedText = string.Empty;
                        LogMessage("Sending StorageFile: " + file.Path.ToString());
                        SpeechToTextResponse result = await client.SendStorageFile(file, locale);
                        if (result != null)
                        {
                            string httpError = result.GetHttpError();
                            if (!string.IsNullOrEmpty(httpError))
                            {
                                resultText.Text = httpError;
                                LogMessage("Http Error: " + httpError.ToString());
                            }
                            else
                            {
                                if (result.Status() == "error")
                                    resultText.Text = "error";
                                else
                                    resultText.Text = result.Result();
                                LogMessage("Result: " + result.ToString());
                            }
                        }
                        else
                            LogMessage("Error while sending file");
                    }
                }
                else
                    LogMessage("Authentication failed check your subscription Key: " + subscriptionKey.Text.ToString());
            }

        }

        #endregion ui
    }
}