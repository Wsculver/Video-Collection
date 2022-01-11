﻿using DirectShowLib;
using DirectShowLib.DES;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using NReco.VideoInfo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VideoCollection.Movies;
using VideoCollection.Subtitles;

namespace VideoCollection.Helpers
{
    internal static class StaticHelpers
    {
        // Get a relative path from the current application directory to a file
        // Returns a Uri
        public static Uri GetRelativePathUriFromCurrent(string fileName)
        {
            // Make sure path ends with a slash because it is a directory
            Uri currentPath = new Uri(Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri filePath = new Uri(fileName);
            Uri relativePath = currentPath.MakeRelativeUri(filePath);
            return new Uri(Uri.UnescapeDataString(relativePath.OriginalString));
        }

        // Get a relative path from the current application directory to a file
        // Returns a string
        public static string GetRelativePathStringFromCurrent(string fileName)
        {
            // Make sure path ends with a slash because it is a directory
            Uri currentPath = new Uri(Directory.GetCurrentDirectory().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri filePath = new Uri(fileName);
            Uri relativePath = currentPath.MakeRelativeUri(filePath);
            return Uri.UnescapeDataString(relativePath.OriginalString);
        }

        // Create a file dialog to choose a video
        public static OpenFileDialog CreateVideoFileDialog()
        {
            OpenFileDialog filePath = new OpenFileDialog();
            filePath.DefaultExt = ".m4v";
            filePath.CheckFileExists = true;
            filePath.CheckPathExists = true;
            filePath.Multiselect = false;
            filePath.ValidateNames = true;
            filePath.Filter = "Video Files|*.m4v;*.mp4;*.MOV;*.mkv";
            return filePath;
        }

        // Create a file dialog to choose an image
        public static OpenFileDialog CreateImageFileDialog()
        {
            OpenFileDialog imagePath = new OpenFileDialog();
            imagePath.DefaultExt = ".png";
            imagePath.CheckFileExists = true;
            imagePath.CheckPathExists = true;
            imagePath.Multiselect = false;
            imagePath.ValidateNames = true;
            imagePath.Filter = "Image Files|*.png;*.jpg;*.jpeg";
            return imagePath;
        }

        // Create a file dialog to choose a folder
        public static CommonOpenFileDialog CreateFolderFileDialog()
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog();
            dlg.IsFolderPicker = true;
            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;
            return dlg;
        }

        // Create a file dialog to choose a subtitle file
        public static OpenFileDialog CreateSubtitleFileDialog()
        {
            OpenFileDialog filePath = new OpenFileDialog();
            filePath.DefaultExt = ".srt";
            filePath.CheckFileExists = true;
            filePath.CheckPathExists = true;
            filePath.Multiselect = false;
            filePath.ValidateNames = true;
            filePath.Filter = "Subtitle Files|*.srt";
            return filePath;
        }

        // Convert a Uri into an ImageSource
        public static ImageSource BitmapFromUri(Uri source)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = source;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }

        private static async Task<Tuple<string, string, string, List<SubtitleSegment>, string>> ParseBonusVideo(string movieFolderPath, string videoFile, IEnumerable<string> subtitleFiles)
        {
            SubtitleParser subParser = new SubtitleParser();
            string bonusSection = Path.GetFileName(Path.GetDirectoryName(videoFile)).ToUpper();
            string bonusTitle = Path.GetFileNameWithoutExtension(videoFile);
            string bonusSubtitleFile = "";
            List<SubtitleSegment> bonusSubtitles = new List<SubtitleSegment>();
            var bonusSubtitleFiles = subtitleFiles.Where(s => s.EndsWith(bonusTitle + ".srt"));
            if (bonusSubtitleFiles.Any())
            {
                bonusSubtitleFile = bonusSubtitleFiles.FirstOrDefault();
                bonusSubtitles = subParser.ExtractSubtitles(bonusSubtitleFile);
            }

            var bonusImageFiles = Directory.GetFiles(movieFolderPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(bonusTitle + ".png") || s.EndsWith(bonusTitle + ".jpg") || s.EndsWith(bonusTitle + ".jpeg"));
            string bonusThumbnail = "";
            if (bonusImageFiles.Any())
            {
                bonusThumbnail = ImageSourceToBase64(BitmapFromUri(new Uri(GetRelativePathStringFromCurrent(bonusImageFiles.First()))));
            }
            else
            {
                await Task.Run(() =>
                {
                    using (MemoryStream thumbnailStream = new MemoryStream())
                    {
                        CreateThumbnailFromVideoFile(thumbnailStream, videoFile, 5);
                        Image image = Image.FromStream(thumbnailStream);
                        bonusThumbnail = ImageToBase64(image, ImageFormat.Jpeg);
                    }
                });
            }

            return Tuple.Create(bonusSection, bonusTitle, bonusThumbnail, bonusSubtitles, videoFile);
        }

        // Parse a movie bonus folder to format all videos in it
        public static async Task<Movie> ParseMovieVideos(string movieFolderPath)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            jss.MaxJsonLength = Int32.MaxValue;

            var videoFiles = Directory.GetFiles(movieFolderPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".m4v") || s.EndsWith(".mp4") || s.EndsWith(".MOV") || s.EndsWith(".mkv"));
            var subtitleFiles = Directory.GetFiles(movieFolderPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".srt"));

            // The first video found should be the movie
            string movieFile = videoFiles.FirstOrDefault();
            string movieTitle = "";
            string movieFilePath = "";
            if (movieFile != null)
            {
                movieTitle = Path.GetFileNameWithoutExtension(movieFile);
                movieFilePath = GetRelativePathStringFromCurrent(movieFile);
            }

            // All other videos are bonus
            List<MovieBonusVideo> bonusVideos = new List<MovieBonusVideo>();
            HashSet<string> bonusSectionsSet = new HashSet<string>();
            int numVideoFiles = videoFiles.Count();
            var tasks = new List<Task<Tuple<string, string, string, List<SubtitleSegment>, string>>>();
            for(int i = 1; i < numVideoFiles; i++)
            {
                string videoFile = videoFiles.ElementAt(i);
                tasks.Add(ParseBonusVideo(movieFolderPath, videoFile, subtitleFiles));
            }
            foreach (var task in await Task.WhenAll(tasks))
            {
                bonusSectionsSet.Add(task.Item1);
                MovieBonusVideo video = new MovieBonusVideo()
                {
                    Title = task.Item2.ToUpper(),
                    Thumbnail = task.Item3,
                    FilePath = task.Item5,
                    Section = task.Item1,
                    Runtime = GetVideoDuration(task.Item5),
                    Subtitles = jss.Serialize(task.Item4)
                };
                bonusVideos.Add(video);
            }

            List<MovieBonusSection> bonusSections = new List<MovieBonusSection>();
            foreach(string sectionName in bonusSectionsSet)
            {
                MovieBonusSection section = new MovieBonusSection()
                {
                    Name = sectionName,
                    Background = jss.Serialize(System.Windows.Media.Color.FromArgb(0, 0, 0, 0))
                };
                bonusSections.Add(section);
            }

            // Get the thumbnail file if it exists, otherwise create one
            var imageFiles = Directory.GetFiles(movieFolderPath, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(movieTitle + ".png") || s.EndsWith(movieTitle + ".jpg") || s.EndsWith(movieTitle + ".jpeg"));
            string movieThumbnail = "";
            if (imageFiles.Any())
            {
                movieThumbnail = ImageSourceToBase64(BitmapFromUri(new Uri(GetRelativePathStringFromCurrent(imageFiles.First()))));
            }
            else if(movieFile != null)
            {
                using (MemoryStream thumbnailStream = new MemoryStream())
                {
                    CreateThumbnailFromVideoFile(thumbnailStream, movieFile, 60);
                    Image image = Image.FromStream(thumbnailStream);
                    movieThumbnail = ImageToBase64(image, ImageFormat.Jpeg);
                }
            }

            SubtitleParser subParser = new SubtitleParser();
            // Get the subtitle file path and parse it
            string subtitleFile = subtitleFiles.Where(s => s.EndsWith(movieTitle + ".srt")).FirstOrDefault();
            List<SubtitleSegment> subtitles = new List<SubtitleSegment>();
            if (subtitleFile != null)
            {
                subtitles = subParser.ExtractSubtitles(subtitleFile);
            }

            Movie movie = new Movie()
            {
                Title = movieTitle.ToUpper(),
                Thumbnail = movieThumbnail,
                MovieFilePath = movieFilePath,
                BonusSections = jss.Serialize(bonusSections),
                BonusVideos = jss.Serialize(bonusVideos),
                Categories = "",
                Subtitles = jss.Serialize(subtitles),
                IsChecked = false
            };

            return movie;
        }

        // Create a thumbnail from a provided video file at the frame seconds in
        // Output thumbnail to provided thumbnailStream
        public static void CreateThumbnailFromVideoFile(Stream thumbnailStream, string videoFile, int seconds)
        {
            var ffMpeg = new NReco.VideoConverter.FFMpegConverter();
            NReco.VideoConverter.ConvertSettings convertSettings = new NReco.VideoConverter.ConvertSettings()
            {
                VideoFrameSize = "640x360"
            };
            ffMpeg.GetVideoThumbnail(videoFile, thumbnailStream, seconds, convertSettings);
        }

        // Convert an Image to a base 64 string
        public static string ImageToBase64(Image image, ImageFormat format)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // Convert Image to byte[]
                image.Save(ms, format);
                byte[] imageBytes = ms.ToArray();

                // Convert byte[] to Base64 String
                string base64String = Convert.ToBase64String(imageBytes);
                return base64String;
            }
        }

        // Convert an ImageSource to a base 64 string
        public static string ImageSourceToBase64(ImageSource image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                var encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image as BitmapSource));
                encoder.Save(ms);
                return ImageToBase64(Image.FromStream(ms), ImageFormat.Jpeg);
            }
        }

        // Convert a base 64 string to an ImageSource
        public static ImageSource Base64ToImageSource(string base64String)
        {
            // Convert Base64 String to byte[]
            byte[] imageBytes = Convert.FromBase64String(base64String);
            using (MemoryStream ms = new MemoryStream(imageBytes, 0, imageBytes.Length))
            {
                ms.Seek(0, SeekOrigin.Begin);

                // Convert byte[] to ImageSource
                ms.Write(imageBytes, 0, imageBytes.Length);
                Image image = Image.FromStream(ms, true);

                using (MemoryStream ms2 = new MemoryStream())
                {
                    image.Save(ms2, ImageFormat.Jpeg);
                    ms2.Seek(0, SeekOrigin.Begin);

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms2;
                    bitmapImage.EndInit();

                    return bitmapImage;
                }
            }
        }

        // Get the first child of type T
        public static T GetObject<T>(DependencyObject o)
            where T : DependencyObject
        {
            if (o is T)
            { return (T)o; }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);

                var result = GetObject<T>(child);
                if (result == null)
                {
                    continue;
                }
                else
                {
                    return result;
                }
            }
            return null;
        }

        // Get the first child of type T with name
        public static T GetObject<T>(DependencyObject o, string name)
            where T : DependencyObject
        {
            if (o is T && (o as FrameworkElement).Name == name)
            { return (T)o; }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);

                var result = GetObject<T>(child, name);
                if (result == null)
                {
                    continue;
                }
                else
                {
                    return result;
                }
            }
            return null;
        }

        // Get the duration of a video file
        public static string GetVideoDuration(string filePath)
        {
            if (File.Exists(filePath))
            {
                var ffProbe = new FFProbe();
                var videoInfo = ffProbe.GetMediaInfo(filePath);
                return videoInfo.Duration.ToString(@"h\:mm\:ss");
            }
            return "0:00:00";
        }
    }
}
