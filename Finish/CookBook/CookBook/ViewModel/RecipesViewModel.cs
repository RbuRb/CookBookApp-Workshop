﻿using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Training.Models;

using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Essentials;

using System.Linq;
using CookBook.Model;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using Plugin.Media;
using Plugin.Media.Abstractions;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using Newtonsoft.Json;
using CookBook.Helpers;
using Plugin.Permissions.Abstractions;

namespace CookBook.ViewModel
{
    public class RecipesViewModel : BaseViewModel
    {
        #region Properties

        public ObservableCollection<Recipe> Recipes { get; }

        #endregion

        #region Commands

        public Command GetRecipesCommand { get; }
        public Command GetClosestCommand { get; }
        public Command GetByPictureCommand { get; }

        #endregion

        #region Constructors

        public RecipesViewModel() : base(title: "Recipes")
        {
            Recipes = new ObservableCollection<Recipe>();

            GetRecipesCommand = new Command(async () => await GetRecipesAsync());
            GetClosestCommand = new Command(async () => await GetClosestAsync());
            GetByPictureCommand = new Command(async () => await GetRecipeByImage());
        }

        #endregion

        #region Methods

        private async Task GetRecipesAsync()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                string jsonRecipes = await Client.GetStringAsync("http://www.croustipeze.com/ressources/recipesdata.json");
                Recipe[] recipes = JsonConvert.DeserializeObject<Recipe[]>(jsonRecipes, Converter.Settings);

                Recipes.Clear();
                foreach (var recipe in recipes)
                    Recipes.Add(recipe);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to get recipes: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error!", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task GetRecipesByCategoryAsync(string category)
        {
            try
            {
                string jsonRecipes = await Client.GetStringAsync("http://www.croustipeze.com/ressources/recipesdata.json");
                Recipe[] recipes = JsonConvert.DeserializeObject<Recipe[]>(jsonRecipes, Converter.Settings);

                var results = recipes.Where(x => x.Category.ToLower() == category.ToLower()).ToList(); ;

                Recipes.Clear();
                foreach (var recipe in results)
                    Recipes.Add(recipe);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to get recipes: {ex.Message}");
            }
        }

        private async Task GetClosestAsync()
        {
            if (IsBusy || Recipes == null || !Recipes.Any())
                return;

            try
            {
                var location = await Geolocation.GetLastKnownLocationAsync();
                if (location == null)
                {
                    location = await Geolocation.GetLocationAsync(new GeolocationRequest
                    {
                        DesiredAccuracy = GeolocationAccuracy.Medium,
                        Timeout = TimeSpan.FromSeconds(30)
                    });
                }

                var closestRecipe = Recipes
                    .OrderBy(m => location.CalculateDistance(new Xamarin.Essentials.Location(m.Latitude, m.Longitude), DistanceUnits.Miles))
                    .FirstOrDefault();

                if(closestRecipe == null)
                    await Application.Current.MainPage.DisplayAlert("No recipe found", "Something went wrong !", "OK");
                else
                    await Application.Current.MainPage.DisplayAlert("Closest recipe", closestRecipe.Name + " at " + closestRecipe.Location, "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to query location: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error!", ex.Message, "OK");
            }
        }

        private async Task GetRecipeByImage()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                if (!await PermissionsManager.RequestPermissions(new[] { Permission.Camera, Permission.Storage }))
                    return;

                var image = await TakePhoto();
                if(image != null)
                {
                    var tag = await ExtractInfoFromPicture(image);
                    if (tag.Probability > 0.40)
                    {
                        await Application.Current.MainPage.DisplayAlert("Yummy :D", $"Let's cook {tag.TagName} ! \n\n -- {tag.Probability:P1} accuracy --", "OK");
                        await GetRecipesByCategoryAsync(tag.TagName);
                    }
                    else
                    {
                        await Application.Current.MainPage.DisplayAlert("Hum... :/", $"I've no idea what I'm looking at", "OK");

                    }
                    
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unable to get tags: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error!", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        async Task<PredictionModel> ExtractInfoFromPicture(Stream image)
        {
            string SouthCentralUsEndpoint = "https://southcentralus.api.cognitive.microsoft.com";

            // Add your training & prediction key from the settings page of the portal
            string trainingKey = "";
            string predictionKey = "";

            // Create the Api, passing in the training key
            CustomVisionTrainingClient trainingApi = new CustomVisionTrainingClient()
            {
                ApiKey = trainingKey,
                Endpoint = SouthCentralUsEndpoint
            };

            // Create a new project
            Guid projectId = new Guid("");
            var project = trainingApi.GetProject(projectId);

            // Now there is a trained endpoint, it can be used to make a prediction

            // Create a prediction endpoint, passing in obtained prediction key
            CustomVisionPredictionClient endpoint = new CustomVisionPredictionClient()
            {
                ApiKey = predictionKey,
                Endpoint = SouthCentralUsEndpoint
            };

            // Make a prediction against the new project
            var result = endpoint.PredictImage(project.Id, image);
            var tag = result.Predictions.FirstOrDefault();

            return tag;
        }

        private async Task<Stream> TakePhoto()
        {
            if (!CrossMedia.Current.IsCameraAvailable || !CrossMedia.Current.IsTakePhotoSupported)
            {
                await Application.Current.MainPage.DisplayAlert("Error!", "No camera detected", "Your device does not have or can not find a camera.");
            }

            var file = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions
            {
                CompressionQuality = 92,
            });

            Stream fileStream = file.GetStream();

            return fileStream;
        }

        #endregion
    }
}
