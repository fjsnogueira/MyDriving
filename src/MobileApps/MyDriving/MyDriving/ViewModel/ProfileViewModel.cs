﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

using Microsoft.WindowsAzure.MobileServices;
using MyDriving.AzureClient;
using MyDriving.Utils;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace MyDriving.ViewModel
{
    public class ProfileViewModel : ViewModelBase
    {
        const int DrivingSkillsBuckets = 4;
        int _drivingSkills; //percentage


        DrivingSkillsBucket _drivingSkillsPlacementBucket;

        double _fuelUsed;

        long _hardAccelerations;

        long _hardStops;

        double _maxSpeed;

        double _totalDistance;

        double _totalTime;

        long _totalTrips;

        public ProfileViewModel()
        {
            InitializeDrivingSkills();
        }

        public int DrivingSkills
        {
            get { return _drivingSkills; }
            set
            {
                SetProperty(ref _drivingSkills, value);
                UpdatePlacementBucket(_drivingSkills);
            }
        }

        public DrivingSkillsBucket DrivingSkillsPlacementBucket
        {
            get { return _drivingSkillsPlacementBucket; }
            set { SetProperty(ref _drivingSkillsPlacementBucket, value); }
        }

        public string FuelUnits => Settings.MetricUnits ? "L" : "gal.";

        public double FuelConverted => Settings.MetricUnits ? FuelUsed/.264172 : FuelUsed;

        public string FuelDisplayNoUnits => FuelConverted.ToString("F");

        public string FuelDisplay => $"{FuelDisplayNoUnits} {FuelUnits.ToLowerInvariant()}";

        public string DistanceUnits => Settings.MetricDistance ? "km" : "miles";

        public string TotalDistanceDisplayNoUnits => DistanceConverted.ToString("F");

        public string TotalDistanceDisplay => $"{TotalDistanceDisplayNoUnits} {DistanceUnits}";

        public double DistanceConverted => (Settings.Current.MetricDistance ? (TotalDistance*1.60934) : TotalDistance);

        public string SpeedUnits => Settings.MetricDistance ? "km/h" : "mph";

        public double MaxSpeedConverted => Settings.MetricDistance ? MaxSpeed : MaxSpeed*0.621371;

        public string MaxSpeedDisplayNoUnits => MaxSpeedConverted.ToString("F");

        public string MaxSpeedDisplay => $"{MaxSpeedDisplayNoUnits} {SpeedUnits}";

        public string TotalTimeDisplay
        {
            get
            {
                var time = TimeSpan.FromSeconds(TotalTime);
                if (time.TotalMinutes < 1)
                    return $"{time.Seconds}s";

                if (time.TotalHours < 1)
                    return $"{time.Minutes}m {time.Seconds}s";

                return $"{(int) time.TotalHours}h {time.Minutes}m {time.Seconds}s";
            }
        }

        public double TotalDistance
        {
            get { return _totalDistance; }
            set
            {
                if (!SetProperty(ref _totalDistance, value))
                    return;

                OnPropertyChanged(nameof(DistanceUnits));
                OnPropertyChanged(nameof(TotalDistanceDisplay));
                OnPropertyChanged(nameof(TotalDistanceDisplayNoUnits));
                OnPropertyChanged(nameof(DistanceConverted));
            }
        }

        public double FuelUsed
        {
            get { return _fuelUsed; }
            set
            {
                if (!SetProperty(ref _fuelUsed, value))
                    return;

                OnPropertyChanged(nameof(FuelUnits));
                OnPropertyChanged(nameof(FuelDisplay));
                OnPropertyChanged(nameof(FuelDisplayNoUnits));
                OnPropertyChanged(nameof(FuelConverted));
            }
        }

        public double TotalTime
        {
            get { return _totalTime; }
            set
            {
                if (!SetProperty(ref _totalTime, value))
                    return;

                OnPropertyChanged(nameof(TotalTimeDisplay));
            }
        }

        public double MaxSpeed
        {
            get { return _maxSpeed; }
            set
            {
                if (!SetProperty(ref _maxSpeed, value))
                    return;

                OnPropertyChanged(nameof(SpeedUnits));
                OnPropertyChanged(nameof(MaxSpeedConverted));
                OnPropertyChanged(nameof(MaxSpeedDisplayNoUnits));
                OnPropertyChanged(nameof(MaxSpeedDisplay));
            }
        }

        public long HardStops
        {
            get { return _hardStops; }
            set { SetProperty(ref _hardStops, value); }
        }

        public long HardAccelerations
        {
            get { return _hardAccelerations; }
            set { SetProperty(ref _hardAccelerations, value); }
        }

        public long TotalTrips
        {
            get { return _totalTrips; }
            set { SetProperty(ref _totalTrips, value); }
        }

        DrivingSkillsBucket[] Skills { get; set; }

        public async Task<bool> UpdateProfileAsync()
        {
            if (IsBusy)
                return false;

            var progress = Acr.UserDialogs.UserDialogs.Instance.Loading("Loading profile...",
                maskType: Acr.UserDialogs.MaskType.Clear);
            var error = false;
            try
            {
                IsBusy = true;

                var users = await StoreManager.UserStore.GetItemsAsync(0, 100, true);

                var currentUser = users.FirstOrDefault(s => s.UserId == Settings.UserUID);

                if (currentUser == null)
                {
                    error = true;
                }
                else
                {
                    TotalDistance = currentUser.TotalDistance;
                    HardStops = currentUser.HardStops;
                    HardAccelerations = currentUser.HardAccelerations;
                    DrivingSkills = currentUser.Rating;
                    TotalTime = currentUser.TotalTime;
                    TotalTrips = currentUser.TotalTrips;
                    FuelUsed = currentUser.FuelConsumption;
                    MaxSpeed = currentUser.MaxSpeed;
#if DEBUG || XTC
                    if (currentUser.Rating == 0)
                        DrivingSkills = 86;
#endif
                    OnPropertyChanged("Stats");
                }
                //update stats here.
            }
            catch (Exception ex)
            {
                Logger.Instance.Report(ex);
                error = true;
            }
            finally
            {
                progress?.Dispose();
                IsBusy = false;
            }

            return !error;
        }

        async Task UpdatePictureAsync()
        {
            IMobileServiceClient client = ServiceLocator.Instance.Resolve<IAzureClient>()?.Client;
            await Helpers.UserProfileHelper.GetUserProfileAsync(client);
        }

        void InitializeDrivingSkills()
        {
            // to do find specifications for colors/desription 
            Skills = new[]
            {
                new DrivingSkillsBucket() {BetterThan = 0, Description = "Poor"},
                new DrivingSkillsBucket() {BetterThan = 45, Description = "Average"},
                new DrivingSkillsBucket() {BetterThan = 75, Description = "Great!"},
                new DrivingSkillsBucket() {BetterThan = 90, Description = "Amazing!"}
            };
        }

        void UpdatePlacementBucket(int skills)
        {
            for (int i = DrivingSkillsBuckets - 1; i >= 0; i--)
            {
                if (skills > Skills[i].BetterThan)
                {
                    DrivingSkillsPlacementBucket = Skills[i];
                    return;
                }
            }
        }
    }

    public struct DrivingSkillsBucket
    {
        public int BetterThan { get; set; }
        public string Description { get; set; }
    }
}