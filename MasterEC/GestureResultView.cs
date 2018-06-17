namespace MasterEC
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
   
    public sealed class GestureResultView : INotifyPropertyChanged
    {
       /// <summary> Current confidence value reported by the discrete gesture </summary>
        private float confidenceSeated = 0.0f;
        private float confidenceDrinking = 0.0f;

        /// <summary> True, if the discrete gesture is currently being detected </summary>
        private bool detectedSeated = false;
        private bool detectedDrinking = false;

        /// <summary> True, if the body is currently being tracked </summary>
        private bool isTracked = false;

        private float drinkingProgress = 0.0f;
   
        /// <summary>
        /// Initializes a new instance of the GestureResultView class and sets initial property values
        /// </summary>
        /// <param name="isTracked">True, if the body is currently tracked</param>
        /// <param name="detected">True, if the gesture is currently detected for the associated body</param>
        /// <param name="confidence">Confidence value for detection of the 'Seated' gesture</param>
        public GestureResultView(bool isTracked, bool detectedSeated, bool detectedDrinking, float confidenceSeated, float confidenceDrinking, float progress)
        {           
            this.IsTracked = isTracked;
            this.DetectedSeated = detectedSeated;
            this.DetectedDrinking = detectedDrinking;
            this.ConfidenceSeated = confidenceSeated;
            this.ConfidenceDrinking = confidenceDrinking;
         }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary> 
        /// Gets a value indicating whether or not the body associated with the gesture detector is currently being tracked 
        /// </summary>
        public bool IsTracked
        {
            get
            {
                return this.isTracked;
            }

            private set
            {
                if (this.IsTracked != value)
                {
                    this.isTracked = value;
                    this.NotifyPropertyChanged();
                }
            }
        }

        /// <summary> 
        /// Gets a value indicating whether or not the discrete gesture has been detected
        /// </summary>
        public bool DetectedSeated
        {
            get
            {
                return this.detectedSeated;
            }

            private set
            {
                if (this.detectedSeated != value)
                {
                    this.detectedSeated = value;
                    this.NotifyPropertyChanged();
                }
            }
        }

        /// <summary> 
        /// Gets a value indicating whether or not the discrete gesture has been detected
        /// </summary>
        public bool DetectedDrinking
        {
            get
            {
                return this.detectedDrinking;
            }

            private set
            {
                if (this.detectedDrinking != value)
                {
                    this.detectedDrinking = value;
                    this.NotifyPropertyChanged();
                }
            }
        }
        /// <summary> 
        /// Gets a float value which indicates the detector's confidence that the gesture is occurring for the associated body 
        /// </summary>
        public float ConfidenceSeated
        {
            get
            {
                return this.confidenceSeated;
            }

            private set
            {
                if (this.confidenceSeated != value)
                {
                    this.confidenceSeated = value;
                    this.NotifyPropertyChanged();
                }
            }
        }

        public float ConfidenceDrinking
        {
            get
            {
                return this.confidenceDrinking;
            }

            private set
            {
                if (this.confidenceDrinking != value)
                {
                    this.confidenceDrinking = value;
                    this.NotifyPropertyChanged();
                }
            }
        }

        /// <summary> 
        /// Gets a value indicating the progress associated with the 'DrinkingProgress' gesture for the tracked body 
        /// </summary>
        public float DrinkingProgress
        {
            get
            {
                return this.drinkingProgress;
            }

            private set
            {
                if (this.drinkingProgress != value)
                {
                    this.drinkingProgress = value;
                    this.NotifyPropertyChanged();
                }            
            }
        }

       
        /// <summary>
        /// Updates the values associated with the discrete gesture detection result
        /// </summary>
        /// <param name="isBodyTrackingIdValid">True, if the body associated with the GestureResultView object is still being tracked</param>
        /// <param name="isGestureDetected">True, if the discrete gesture is currently detected for the associated body</param>
        /// <param name="detectionConfidence">Confidence value for detection of the discrete gesture</param>
        public void UpdateGestureResult(bool isBodyTrackingIdValid, bool isGestureSeatedDet, bool isGestureDrinkingDet, float detectionConfSeated, float detectionConfDrinking,float progress )
        {
            this.IsTracked = isBodyTrackingIdValid;
            this.ConfidenceSeated = 0.0f;
            this.ConfidenceDrinking = 0.0f;

            if (!this.IsTracked)
            {
               
                this.DetectedSeated = false;
                this.DetectedDrinking = false;
                this.DrinkingProgress = -1.0f;
            }
            else
            {
               
                this.DetectedSeated = isGestureSeatedDet;
                this.DetectedDrinking = isGestureDrinkingDet;
                this.DrinkingProgress = progress;

                if (this.DetectedSeated)
                {
                    this.ConfidenceSeated = detectionConfSeated;
                    
                  
                }
                else if (this.DetectedDrinking)
                {
                    this.ConfidenceDrinking = detectionConfDrinking;                 
                }
            }
        }

        /// <summary>
        /// Notifies UI that a property has changed
        /// </summary>
        /// <param name="propertyName">Name of property that has changed</param> 
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}