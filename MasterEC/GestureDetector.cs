namespace MasterEC
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Kinect;
    using Microsoft.Kinect.VisualGestureBuilder;


    public class GestureDetector : IDisposable
    {
        /// <summary> Path to the gesture database that was trained with VGB </summary>
        private readonly string gestureDatabase = @"Database\Database.gbd";

        /// <summary> Name of the discrete gesture in the database that we want to track </summary>
        private readonly string seatedGestureName = "Seated";

        private readonly string drinkingGestureName = "Drinking";

        //private readonly string seatedProgressGestureName = "SeatedProgress";

        private readonly string drinkingProgressGestureName = "DrinkingProgress";

        /// <summary> Gesture frame source which should be tied to a body tracking ID </summary>
        private VisualGestureBuilderFrameSource vgbFrameSource = null;

        /// <summary> Gesture frame reader which will handle gesture events coming from the sensor </summary>
        private VisualGestureBuilderFrameReader vgbFrameReader = null;

        /// <summary>
        /// Initializes a new instance of the GestureDetector class along with the gesture frame source and reader
        /// </summary>
        /// <param name="kinectSensor">Active sensor to initialize the VisualGestureBuilderFrameSource object with</param>
        /// <param name="gestureResultView">GestureResultView object to store gesture results of a single body to</param>
        public GestureDetector(KinectSensor kinectSensor, GestureResultView gestureResultView)
        {
            if (kinectSensor == null)
            {
                throw new ArgumentNullException("kinectSensor");
            }

            if (gestureResultView == null)
            {
                throw new ArgumentNullException("gestureResultView");
            }

            this.GestureResultView = gestureResultView;
            

            // create the vgb source. The associated body tracking ID will be set when a valid body frame arrives from the sensor.
            this.vgbFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            this.vgbFrameSource.TrackingIdLost += this.Source_TrackingIdLost;

            // open the reader for the vgb frames
            this.vgbFrameReader = this.vgbFrameSource.OpenReader();
            if (this.vgbFrameReader != null)
            {
                this.vgbFrameReader.IsPaused = true;
            }

            // load all gestures from the gesture database
            using (var database = new VisualGestureBuilderDatabase(this.gestureDatabase))
            {
                this.vgbFrameSource.AddGestures(database.AvailableGestures);
            }
        }

        /// <summary> Gets the GestureResultView object which stores the detector results for display in the UI </summary>
        public GestureResultView GestureResultView { get; private set; }

       
        /// <summary>
        /// Gets or sets the body tracking ID associated with the current detector
        /// The tracking ID can change whenever a body comes in/out of scope
        /// </summary>
        public ulong TrackingId
        {
            get
            {
                return this.vgbFrameSource.TrackingId;
            }

            set
            {
                if (this.vgbFrameSource.TrackingId != value)
                {
                    this.vgbFrameSource.TrackingId = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the detector is currently paused
        /// If the body tracking ID associated with the detector is not valid, then the detector should be paused
        /// </summary>
        public bool IsPaused
        {
            get
            {
                return this.vgbFrameReader.IsPaused;
            }

            set
            {
                if (this.vgbFrameReader.IsPaused != value)
                {
                    this.vgbFrameReader.IsPaused = value;
                }
            }
        }

        /// <summary>
        /// Disposes all unmanaged resources for the class
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the VisualGestureBuilderFrameSource and VisualGestureBuilderFrameReader objects
        /// </summary>
        /// <param name="disposing">True if Dispose was called directly, false if the GC handles the disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vgbFrameReader != null)
                {
                    this.vgbFrameReader.Dispose();
                    this.vgbFrameReader = null;
                }

                if (this.vgbFrameSource != null)
                {
                    this.vgbFrameSource.TrackingIdLost -= this.Source_TrackingIdLost;
                    this.vgbFrameSource.Dispose();
                    this.vgbFrameSource = null;
                }
            }
        }

        /// <summary>
        /// Retrieves the latest gesture detection results from the sensor
        /// </summary>
        public void UpdateGestureData()
        {
            // VisualGestureBuilderFrameReference frameReference = e.FrameReference;
            using (var frame = this.vgbFrameReader.CalculateAndAcquireLatestFrame())
            {
                if (frame != null)
                {
                    // get all discrete and continuous gesture results which arrived with the latest frame
                    IReadOnlyDictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;
                    IReadOnlyDictionary<Gesture, ContinuousGestureResult> continuousResults = frame.ContinuousGestureResults;

                    if (discreteResults != null)
                    {
                        bool detSeated = this.GestureResultView.DetectedSeated;
                        bool detDrinking = this.GestureResultView.DetectedDrinking;
                        float confSeated = this.GestureResultView.ConfidenceSeated;
                        float confDrinking = this.GestureResultView.ConfidenceDrinking;
                        float drinkingProgress = this.GestureResultView.DrinkingProgress;


                        // we only have one gesture in this source object, but you can get multiple gestures
                        foreach (Gesture gesture in this.vgbFrameSource.Gestures)
                        {
                            if (gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult result = null;
                                discreteResults.TryGetValue(gesture, out result);

                                if (result != null)
                                {
                                    if (gesture.Name.Equals(this.seatedGestureName))
                                    {
                                        detSeated = result.Detected;
                                        confSeated = result.Confidence;
                                    }
                                    else if (gesture.Name.Equals(this.drinkingGestureName))
                                    {
                                        detDrinking = result.Detected;
                                        confDrinking = result.Confidence;

                                    }
                                }
                            }
                            if (continuousResults != null)
                            {
                                if (gesture.Name.Equals(this.drinkingProgressGestureName) && gesture.GestureType == GestureType.Continuous)
                                {
                                    ContinuousGestureResult result = null;
                                    continuousResults.TryGetValue(gesture, out result);

                                    if (result != null)
                                    {
                                        drinkingProgress = result.Progress;
                                    }
                                }
                            }
                        }

                        // clamp the progress value between 0 and 1
                        if (drinkingProgress < 0)
                        {
                            drinkingProgress = 0;
                        }
                        else if (drinkingProgress > 1)
                        {
                            drinkingProgress = 1;
                        }


                        // update the GestureResultView object with new gesture result values
                        this.GestureResultView.UpdateGestureResult(true, detSeated, detDrinking, confSeated, confDrinking, drinkingProgress);
                    }
                }
            }
        }

        /// <summary>
        /// Handles the TrackingIdLost event for the VisualGestureBuilderSource object
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Source_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            // update the GestureResultView object to show the 'Not Tracked' image in the UI
            this.GestureResultView.UpdateGestureResult(false, false, false, 0.0f, 0.0f, -1.0f);
        }
    }
}
