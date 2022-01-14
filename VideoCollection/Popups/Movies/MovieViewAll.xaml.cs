﻿using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using VideoCollection.Database;
using VideoCollection.Helpers;
using VideoCollection.Movies;

namespace VideoCollection.Popups.Movies
{
    /// <summary>
    /// Interaction logic for ViewAll.xaml
    /// </summary>
    public partial class MovieViewAll : Window
    {
        private string _categoryId;
        private bool _categoryChanged = false;
        private Border _splash;
        private Action _callback;

        /// <summary> Don't use this constructur. It is only here to make resizing work </summary>
        public MovieViewAll() { }

        public MovieViewAll(string Id, ref Border splash, Action callback)
        {
            InitializeComponent();

            Closed += (a, b) => { Owner.Activate(); };

            _categoryId = Id;
            _splash = splash;
            _callback = callback;

            UpdateCategory();
        }

        // Refresh to show current database data
        private void UpdateCategory()
        {
            using (SQLiteConnection connection = new SQLiteConnection(App.databasePath))
            {
                connection.CreateTable<MovieCategory>();
                MovieCategory category = connection.Query<MovieCategory>("SELECT * FROM MovieCategory WHERE Id = " + _categoryId)[0];
                MovieCategoryDeserialized categoryDeserialized = new MovieCategoryDeserialized(category);
                labelCategory.Content = categoryDeserialized.Name;
                icVideos.ItemsSource = categoryDeserialized.Movies;
            }
        }

        // Scale based on the size of the window
        #region ScaleValue Depdency Property
        public static readonly DependencyProperty ScaleValueProperty = ScaleValueHelper.SetScaleValueProperty<MovieViewAll>();
        public double ScaleValue
        {
            get => (double)GetValue(ScaleValueProperty);
            set => SetValue(ScaleValueProperty, value);
        }
        #endregion
        private void MainGrid_SizeChanged(object sender, EventArgs e)
        {
            ScaleValue = ScaleValueHelper.CalculateScale(movieViewAllWindow, 400f, 780f);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            _splash.Visibility = Visibility.Collapsed;
            if (_categoryChanged)
            {
                _callback();
            }
            Close();
        }

        // Show the movie details when a movie thumbnail is clicked
        private void imageThumbnail_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                Window parentWindow = Window.GetWindow(this);
                MovieDetails popup = new MovieDetails((sender as Image).Tag.ToString(), ref Splash, () => { });
                popup.Width = parentWindow.ActualWidth;
                popup.Height = parentWindow.ActualHeight;
                popup.Owner = parentWindow;
                Splash.Visibility = Visibility.Visible;
                popup.Show();
            }
        }

        // Show info icon when hovering a thumbnail
        private void imageThumbnail_MouseEnter(object sender, MouseEventArgs e)
        {
            StaticHelpers.GetObject<Rectangle>((sender as Image).Parent).Visibility = Visibility.Visible;
            StaticHelpers.GetObject<Border>((sender as Image).Parent, "movieSplash").Visibility = Visibility.Visible;
            StaticHelpers.GetObject<Border>((sender as Image).Parent, "iconPlayMovie").Visibility = Visibility.Visible;
        }

        // Hide info icon when not hovering a thumbnail
        private void imageThumbnail_MouseLeave(object sender, MouseEventArgs e)
        {
            StaticHelpers.GetObject<Rectangle>((sender as Image).Parent).Visibility = Visibility.Collapsed;
            StaticHelpers.GetObject<Border>((sender as Image).Parent, "movieSplash").Visibility = Visibility.Collapsed;
            StaticHelpers.GetObject<Border>((sender as Image).Parent, "iconPlayMovie").Visibility = Visibility.Collapsed;
        }

        // Play the movie directly
        private void btnPlayMovie_Click(object sender, RoutedEventArgs e)
        {
            string id = (sender as Button).Tag.ToString();
            using (SQLiteConnection connection = new SQLiteConnection(App.databasePath))
            {
                connection.CreateTable<Movie>();
                Movie movie = connection.Query<Movie>("SELECT * FROM Movie WHERE Id = " + id)[0];
                try
                {
                    MovieDeserialized movieDeserialized = new MovieDeserialized(movie);

                    if (App.videoPlayer == null)
                    {
                        Window parentWindow = Application.Current.MainWindow;
                        try
                        {
                            VideoPlayer popup = new VideoPlayer(movieDeserialized);
                            App.videoPlayer = popup;
                            popup.Width = parentWindow.ActualWidth;
                            popup.Height = parentWindow.ActualHeight;
                            popup.Owner = parentWindow;
                            popup.Left = popup.LeftMultiplier = parentWindow.Left;
                            popup.Top = popup.TopMultiplier = parentWindow.Top;
                            popup.Show();
                        }
                        catch (Exception ex)
                        {
                            CustomMessageBox popup = new CustomMessageBox(ex.Message, CustomMessageBox.MessageBoxType.OK);
                            popup.Width = parentWindow.ActualWidth * 0.25;
                            popup.Height = popup.Width * 0.55;
                            popup.Owner = parentWindow;
                            popup.ShowDialog();
                        }
                    }
                    else
                    {
                        App.videoPlayer.updateVideo(movieDeserialized);
                    }
                }
                catch (Exception ex)
                {
                    Window parentWindow = Window.GetWindow(this).Owner;
                    CustomMessageBox popup = new CustomMessageBox("Error: " + ex.Message, CustomMessageBox.MessageBoxType.OK);
                    popup.Width = parentWindow.ActualWidth * 0.25;
                    popup.Height = popup.Width * 0.55;
                    popup.Owner = parentWindow;
                    Splash.Visibility = Visibility.Visible;
                    popup.ShowDialog();
                    Splash.Visibility = Visibility.Collapsed;
                    _callback();
                }
            }
        }

        // Show the movie details when the details setting button is clicked
        private void btnDetails_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            MovieDetails popup = new MovieDetails((sender as Button).Tag.ToString(), ref Splash, () => { });
            popup.Width = parentWindow.ActualWidth;
            popup.Height = parentWindow.ActualHeight;
            popup.Owner = parentWindow;
            Splash.Visibility = Visibility.Visible;
            popup.Show();
        }

        // Delete the movie from the database
        private void btnDeleteMovie_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string movieId = button.Tag.ToString();
            using (SQLiteConnection connection = new SQLiteConnection(App.databasePath))
            {
                connection.CreateTable<Movie>();
                Movie movie = connection.Query<Movie>("SELECT * FROM Movie WHERE Id = " + movieId)[0];

                Window parentWindow = Window.GetWindow(this).Owner;
                CustomMessageBox popup = new CustomMessageBox("Are you sure you want to delete " + movie.Title + " from the database? This only removes the movie from your video collection, it does not delete any movie files.", CustomMessageBox.MessageBoxType.YesNo);
                popup.Width = parentWindow.ActualWidth * 0.25;
                popup.Height = popup.Width * 0.55;
                popup.Owner = parentWindow;
                Splash.Visibility = Visibility.Visible;
                if (popup.ShowDialog() == true)
                {
                    _categoryChanged = true;
                    DatabaseFunctions.DeleteMovie(movie);
                    UpdateCategory();
                }
                Splash.Visibility = Visibility.Collapsed;
            }
        }

        // Show the update movie screen with the movie selected
        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Window parentWindow = Window.GetWindow(this);
            UpdateMovie popup = new UpdateMovie(ref Splash, () => 
            { 
                _categoryChanged = true;
                UpdateCategory(); 
            });
            popup.Width = parentWindow.ActualWidth * 0.67;
            popup.Height = popup.Width * 0.627;
            for (int i = 0; i < popup.lvMovieList.Items.Count; i++)
            {
                MovieDeserialized movie = (MovieDeserialized)popup.lvMovieList.Items[i];
                if (movie.Id.ToString() == button.Tag.ToString())
                {
                    popup.lvMovieList.SelectedIndex = i;
                }
            }
            popup.Owner = parentWindow;
            Splash.Visibility = Visibility.Visible;
            popup.Show();
        }

        // Remove the movie from the category list and the category from the list for the movie
        private void btnRemoveMovieFromCategory_Click(object sender, RoutedEventArgs e)
        {
            JavaScriptSerializer jss = new JavaScriptSerializer();
            string movieId = (sender as Button).Tag.ToString();
            using (SQLiteConnection connection = new SQLiteConnection(App.databasePath))
            {
                connection.CreateTable<MovieCategory>();
                MovieCategory category = connection.Query<MovieCategory>("SELECT * FROM MovieCategory WHERE Id = " + _categoryId)[0];
                DatabaseFunctions.RemoveMovieFromCategory(movieId, category);

                connection.CreateTable<Movie>();
                Movie movie = connection.Query<Movie>("SELECT * FROM Movie WHERE Id = " + movieId)[0];
                List<string> categories = jss.Deserialize<List<string>>(movie.Categories);
                categories.Remove(category.Name);
                movie.Categories = jss.Serialize(categories);
                connection.Update(movie);
            }

            _categoryChanged = true;

            UpdateCategory();
        }

        // When the size of the videos items control changes adjust padding to make it look good with a scroll bar
        private void icVideos_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Math.Round(icVideos.ActualHeight) > Math.Round(scrollVideos.ActualHeight))
            {
                icVideos.Margin = new Thickness(10, 0, 0, 0);
            } else
            {
                icVideos.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        // Popup update movie category window
        private void btnUpdateCategory_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            UpdateMovieCategory popup = new UpdateMovieCategory(_categoryId, ref Splash, () =>
            {
                _categoryChanged = true;
                UpdateCategory();
            });
            popup.Width = parentWindow.ActualWidth * 0.35;
            popup.Height = popup.Width * 1.201;
            popup.Owner = parentWindow;
            Splash.Visibility = Visibility.Visible;
            popup.Show();
        }

        // Remove the category from the database and from all movie category lists
        private void btnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            bool deleted = false;
            Window parentWindow = Window.GetWindow(this).Owner;
            JavaScriptSerializer jss = new JavaScriptSerializer();
            using (SQLiteConnection connection = new SQLiteConnection(App.databasePath))
            {
                connection.CreateTable<MovieCategory>();
                MovieCategory category = connection.Query<MovieCategory>("SELECT * FROM MovieCategory WHERE Id = " + _categoryId)[0];
                CustomMessageBox popup = new CustomMessageBox("Are you sure you want to delete the " + category.Name + " category?", CustomMessageBox.MessageBoxType.YesNo);
                popup.Width = parentWindow.ActualWidth * 0.25;
                popup.Height = popup.Width * 0.55;
                popup.Owner = parentWindow;
                Splash.Visibility = Visibility.Visible;
                if (popup.ShowDialog() == true)
                {
                    List<Movie> movies = connection.Table<Movie>().ToList();
                    foreach (Movie movie in movies)
                    {
                        List<string> categories = jss.Deserialize<List<string>>(movie.Categories);
                        categories.Remove(category.Name);
                        movie.Categories = jss.Serialize(categories);
                        connection.Update(movie);
                    }
                    connection.Delete<MovieCategory>(_categoryId);
                    deleted = true;
                }
                Splash.Visibility = Visibility.Collapsed;
            }

            if (deleted)
            {
                _splash.Visibility = Visibility.Collapsed;
                _callback();
                Close();
            }
        }
    }
}