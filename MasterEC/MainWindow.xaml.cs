namespace MasterEC
{
    using System.Windows;
    using Microsoft.Kinect;
    using System.ComponentModel;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Windows.Controls;
    using Microsoft.Kinect.VisualGestureBuilder;
    using LiveCharts;
    using LiveCharts.Wpf;
    using System.Diagnostics;
    using Microsoft.Speech.AudioFormat;
    using Microsoft.Speech.Recognition;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Interaction logic for the MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary> Active Kinect sensor </summary>
        private KinectSensor kinectSensor = null;

        /// <summary> Array for the bodies (Kinect will track up to 6 people simultaneously) </summary>
        private Body[] bodies = null;

        /// <summary>  Index of the active body (first tracked person in the body array) </summary>
        private int activeBodyIndex = 0;

        /// <summary> Reader for body frames </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary> Current status text to display </summary>
        private string statusText = null;

        /// <summary> KinectBodyView object which handles drawing the Kinect bodies to a View box in the UI </summary>
        private KinectBodyView kinectBodyView = null;

        /// <summary> Gesture detector which will be tied to the active body (closest skeleton to the sensor) </summary>
        private GestureDetector gestureDetector = null;

        /// <summary> GestureResultView for displaying gesture results associated with the tracked person in the UI </summary>
        private GestureResultView gestureResultView = null;

        /// <summary>
        /// Stream for 32b-16b conversion.
        /// </summary>
        private KinectAudioStream kinectAudioStream = null;

        /// <summary>
        /// Speech recognition engine using audio data from Kinect.
        /// </summary>
        private SpeechRecognitionEngine speechEngine = null;

        int alarmCount = 0;

        /// <summary>
        /// Initializes a new instance of the MainWindow class
        /// </summary>
        public MainWindow()
        {
            // initialize the MainWindow
            this.InitializeComponent();

            // only one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // set the BodyFramedArrived event notifier
            this.bodyFrameReader.FrameArrived += this.Reader_BodyFrameArrived;
            
            // initialize the BodyViewer object for displaying tracked bodies in the UI
            this.kinectBodyView = new KinectBodyView(this.kinectSensor);

            // initialize the GestureDetector object
            this.gestureResultView = new GestureResultView(false, false, false, 0.0f, 0.0f, -1.0f);
            this.gestureDetector = new GestureDetector(this.kinectSensor, this.gestureResultView);

            this.gestureResultGrid.DataContext = this.gestureResultView;

            // set our data context objects for display in UI
            this.DataContext = this;
            this.kinectBodyViewbox.DataContext = this.kinectBodyView;

            using (MasterEntities db = new MasterEntities())
            {
                //Retrieve data from stored procedure
                var data = db.TodayMob();

                foreach (var x in data)
                {
                    this.today_mobility.Text = Convert.ToString(x.Minute.Value) + "  minutes";
                }

                //Retrieve data from stored procedure
                var liters = db.TodayHydratation();

                foreach (var x in liters)
                {
                    this.today_hydratation.Text = Convert.ToString(x.Liters.Value) + "  Liters";
                }

                //Retrieve data from stored procedure
                var alarms = db.GetTotalAlarms();

                foreach (var x in alarms)
                {
                    this.totalAlarms.Text = Convert.ToString(x.Value);
                }

            }

            //SPEECH RECOGNITION
            // grab the audio stream
            IReadOnlyList<AudioBeam> audioBeamList = this.kinectSensor.AudioSource.AudioBeams;
            System.IO.Stream audioStream = audioBeamList[0].OpenInputStream();

            // create the convert stream
            this.kinectAudioStream = new KinectAudioStream(audioStream);

            RecognizerInfo ri = TryGetKinectRecognizer();

            if (null != ri)
            {
                this.speechEngine = new SpeechRecognitionEngine(ri.Id);
                Choices commands = new Choices();
                commands.Add(new SemanticResultValue("help", "HELP"));
                commands.Add(new SemanticResultValue("please help", "HELP"));
                commands.Add(new SemanticResultValue("please", "PLEASE"));
                commands.Add(new SemanticResultValue("ambulance", "AMBULANCE"));
                commands.Add(new SemanticResultValue("police", "POLICE"));

                var gb = new GrammarBuilder { Culture = ri.Culture };
                gb.Append(commands);
                var g = new Grammar(gb);
                this.speechEngine.LoadGrammar(g);

                this.speechEngine.SpeechRecognized += this.SpeechRecognized;

                this.kinectAudioStream.SpeechActive = true;
                this.speechEngine.SetInputToAudioStream(
                this.kinectAudioStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000,
                16, 1, 32000, 2, null));
                this.speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                Application.Current.Shutdown();
            }
        }


        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.FrameArrived -= this.Reader_BodyFrameArrived;
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.gestureDetector != null)
            {
                // The GestureDetector contains disposable members (VisualGestureBuilderFrameSource and VisualGestureBuilderFrameReader)
                this.gestureDetector.Dispose();
                this.gestureDetector = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.IsAvailableChanged -= this.Sensor_IsAvailableChanged;
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the event when the sensor becomes unavailable (e.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        /// <summary>
        /// Gets the first body in the bodies array that is currently tracked by the Kinect sensor
        /// </summary>
        /// <returns>Index of first tracked body, or -1 if no body is tracked</returns>
        private int GetActiveBodyIndex()
        {
            int activeBodyIndex = -1;
            int maxBodies = this.kinectSensor.BodyFrameSource.BodyCount;

            for (int i = 0; i < maxBodies; ++i)
            {
                // find the first tracked body and verify it has hands tracking enabled (by default, Kinect will only track handstate for 2 people)
                if (this.bodies[i].IsTracked && (this.bodies[i].HandRightState != HandState.NotTracked || this.bodies[i].HandLeftState != HandState.NotTracked))
                {
                    activeBodyIndex = i;
                    break;
                }
            }

            return activeBodyIndex;
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor and updates the associated gesture detector object.
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

           using (var bodyFrame = this.bodyFrameReader.AcquireLatestFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        // creates an array of 6 bodies, which is the max number of bodies that Kinect can track simultaneously
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);

                    if (!this.bodies[this.activeBodyIndex].IsTracked)
                    {
                        // we lost tracking of the active body, so update to the first tracked body in the array
                        int bodyIndex = this.GetActiveBodyIndex();

                        if (bodyIndex > 0)
                        {
                            this.activeBodyIndex = bodyIndex;
                        }
                    }

                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                Body activeBody = this.bodies[this.activeBodyIndex];

                // visualize the new body data
                this.kinectBodyView.UpdateBodyFrame(activeBody);

                // visualize the new gesture data
                if (activeBody.TrackingId != this.gestureDetector.TrackingId)
                {
                    // if the tracking ID changed, update the detector with the new value
                    this.gestureDetector.TrackingId = activeBody.TrackingId;
                }

                if (this.gestureDetector.TrackingId == 0)
                {
                    // the active body is not tracked, pause the detector and update the UI
                    this.gestureDetector.IsPaused = true;
                    this.gestureResultView.UpdateGestureResult(false, false, false, 0.0f, 0.0f, -1.0f);
                }
                else
                {
                    // the active body is tracked, unpause the detector
                    this.gestureDetector.IsPaused = false;

                    // get the latest gesture frame from the sensor and updates the UI with the results
                    this.gestureDetector.UpdateGestureData();

                   
                    //****mobility
                    if (activeBody.IsTracked && this.gestureDetector.GestureResultView.ConfidenceSeated > 0.8f && this.gestureDetector.GestureResultView.DetectedSeated)
                    {
                        using (MasterEntities context = new MasterEntities())
                        {

                            var pacient = new PacientInfo()
                            {
                                Confidence = this.gestureDetector.GestureResultView.ConfidenceSeated,
                                Seated = this.gestureDetector.GestureResultView.DetectedSeated.ToString(),
                                Date = DateTime.Now

                            };

                            context.PacientInfoes.Add(pacient);
                            context.SaveChanges();
                        }
                    }

                    //****drinking
                    if (activeBody.IsTracked && this.gestureDetector.GestureResultView.ConfidenceDrinking > 0.5f && this.gestureDetector.GestureResultView.DetectedDrinking)
                    {
                        using (MasterEntities context = new MasterEntities())
                        {

                            var drinkinginfo = new PacientDrinkingInfo()
                            {
                                Confidence = this.gestureDetector.GestureResultView.ConfidenceDrinking,
                                Drinking = this.gestureDetector.GestureResultView.DetectedDrinking.ToString(),
                                DrinkingProgress = this.gestureDetector.GestureResultView.DrinkingProgress,
                                Date = DateTime.Now

                            };

                            context.PacientDrinkingInfoes.Add(drinkinginfo);
                            context.SaveChanges();
                        }
                    }
                }
            }
        }


        private void CartesianChart_Loaded(object sender, RoutedEventArgs e)
        {
            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart2.Series.Clear();
                cartesianChart2.AxisX.Clear();
                cartesianChart2.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.WeeklyHydratation();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<double>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();
                foreach (var x in data)
                {
                    col.Values.Add(x.Liters.Value);

                    ax.Labels.Add(x.DayOfWeek.ToString());
                }

                cartesianChart2.Series.Add(col);
                cartesianChart2.AxisX.Add(ax);

                cartesianChart2.AxisY.Add(new Axis
                {
                    Title = "Liters",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }
        }

        private void CartesianChart_Loaded_1(object sender, RoutedEventArgs e)
        {

            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart1.Series.Clear();
                cartesianChart1.AxisX.Clear();
                cartesianChart1.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.FinWeeklyMob();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<int>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();

                //Add data to your chart
                foreach (var x in data)
                {
                    col.Values.Add(Convert.ToInt32(x.Minute.Value));

                    ax.Labels.Add(x.DayOfWeek.ToString());
                }

                cartesianChart1.Series.Add(col);
                cartesianChart1.AxisX.Add(ax);

                cartesianChart1.AxisY.Add(new Axis
                {
                    Title = "Activity minutes",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }


        }


        //Mobility daily
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart1.Series.Clear();
                cartesianChart1.AxisX.Clear();
                cartesianChart1.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.FinWeeklyMob();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<int>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();

                //Add data to your chart
                foreach (var x in data)
                {
                    col.Values.Add(Convert.ToInt32(x.Minute.Value));

                    ax.Labels.Add(x.DayOfWeek.ToString());
                }

                cartesianChart1.Series.Add(col);
                cartesianChart1.AxisX.Add(ax);

                cartesianChart1.AxisY.Add(new Axis
                {
                    Title = "Minute miscare",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }

        }

        //Mobility monthly
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart1.Series.Clear();
                cartesianChart1.AxisX.Clear();
                cartesianChart1.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.MonthlyMob();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<int>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();

                //Add data to your chart
                foreach (var x in data)
                {
                    col.Values.Add(Convert.ToInt32(x.Minute.Value));

                    ax.Labels.Add(x.MonthName.ToString());
                }

                cartesianChart1.Series.Add(col);
                cartesianChart1.AxisX.Add(ax);

                cartesianChart1.AxisY.Add(new Axis
                {
                    Title = "Activity minutes",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }
        }

        //Mobility yearly
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart1.Series.Clear();
                cartesianChart1.AxisX.Clear();
                cartesianChart1.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.YearlyMob();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<int>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();

                //Add data to your chart
                foreach (var x in data)
                {
                    col.Values.Add(Convert.ToInt32(x.Minute.Value));

                    ax.Labels.Add(x.Year.ToString());
                }

                cartesianChart1.Series.Add(col);
                cartesianChart1.AxisX.Add(ax);

                cartesianChart1.AxisY.Add(new Axis
                {
                    Title = "Activity minutes",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }
        }

        //HYDRATATION DAILY
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart2.Series.Clear();
                cartesianChart2.AxisX.Clear();
                cartesianChart2.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.WeeklyHydratation();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<double>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();
                // ax.Title = "aaaaa";
                //Add data to your chart
                foreach (var x in data)
                {
                    col.Values.Add(x.Liters.Value);

                    ax.Labels.Add(x.DayOfWeek.ToString());
                }

                cartesianChart2.Series.Add(col);
                cartesianChart2.AxisX.Add(ax);

                cartesianChart2.AxisY.Add(new Axis
                {
                    Title = "Liters",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }
        }

        //HYDRATATION monthly
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart2.Series.Clear();
                cartesianChart2.AxisX.Clear();
                cartesianChart2.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.MonthlyHydratation();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<double>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();

                //Add data to your chart
                foreach (var x in data)
                {
                    col.Values.Add(x.Liters.Value);

                    ax.Labels.Add(x.MonthName.ToString());
                }

                cartesianChart2.Series.Add(col);
                cartesianChart2.AxisX.Add(ax);

                cartesianChart2.AxisY.Add(new Axis
                {
                    Title = "Liters",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }
        }

        //hydration yearly
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            using (MasterEntities db = new MasterEntities())
            {
                cartesianChart2.Series.Clear();
                cartesianChart2.AxisX.Clear();
                cartesianChart2.AxisY.Clear();
                //Retrieve data from stored procedure
                var data = db.YearlyHydratation();
                //Create columns chart
                ColumnSeries col = new ColumnSeries() { DataLabels = true, Values = new ChartValues<double>(), LabelPoint = point => point.Y.ToString() };
                Axis ax = new Axis() { Separator = new LiveCharts.Wpf.Separator() { Step = 1, IsEnabled = false } };
                ax.Labels = new List<string>();

                //Add data to your chart
                foreach (var x in data)
                {
                    col.Values.Add(x.Liters.Value);

                    ax.Labels.Add(x.Year.ToString());
                }

                cartesianChart2.Series.Add(col);
                cartesianChart2.AxisX.Add(ax);

                cartesianChart2.AxisY.Add(new Axis
                {
                    Title = "Liters",
                    LabelFormatter = value => value.ToString(),
                    Separator = new LiveCharts.Wpf.Separator()
                });
            }
        }

        private void TextBlock_TextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {

        }

        //SPEECH RECOGNITION METHODS

        /// <summary>
        /// Gets the metadata for the speech recognizer (acoustic model) most suitable to
        /// process audio from Kinect device.
        /// </summary>
        /// <returns>
        /// RecognizerInfo if found, <code>null</code> otherwise.
        /// </returns>
        private static RecognizerInfo TryGetKinectRecognizer()
        {
            IEnumerable<RecognizerInfo> recognizers;

            // This is required to catch the case when an expected recognizer is not installed.
            // By default - the x86 Speech Runtime is always expected. 
            try
            {
                recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            }
            catch (COMException)
            {
                return null;
            }

            foreach (RecognizerInfo recognizer in recognizers)
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }


        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.75; //0.35


            if (e.Result.Confidence >= ConfidenceThreshold)
            {

                switch (e.Result.Semantics.Value.ToString())
                {
                    case "HELP":
                        MessageBox.Show("The patient might need HELP");
                        alarmCount++;
                        this.alarmCountText.Text = Convert.ToString(alarmCount);

                        using (MasterEntities context = new MasterEntities())
                        {
                            var alarm = new TotalAlarm()
                            {
                                Date = DateTime.Now

                            };
                            context.TotalAlarms.Add(alarm);
                            context.SaveChanges();


                            //Retrieve data from stored procedure
                            var alarms = context.GetTotalAlarms();

                            foreach (var x in alarms)
                            {
                                this.totalAlarms.Text = Convert.ToString(x.Value);
                            }
                        }

                        break;

                    case "PLEASE HELP":
                        MessageBox.Show("Alarm!! Patient just said PLEASE HELP!!!");
                        alarmCount++;
                        this.alarmCountText.Text = Convert.ToString(alarmCount);

                        using (MasterEntities context = new MasterEntities())
                        {
                            var alarm = new TotalAlarm()
                            {
                                Date = DateTime.Now

                            };
                            context.TotalAlarms.Add(alarm);
                            context.SaveChanges();


                            //Retrieve data from stored procedure
                            var alarms = context.GetTotalAlarms();

                            foreach (var x in alarms)
                            {
                                this.totalAlarms.Text = Convert.ToString(x.Value);
                            }
                        }

                        break;


                    case "AMBULANCE":
                        MessageBox.Show("The patient needs an AMBULANCE...");
                        alarmCount++;
                        this.alarmCountText.Text = Convert.ToString(alarmCount);

                        using (MasterEntities context = new MasterEntities())
                        {
                            var alarm = new TotalAlarm()
                            {
                                Date = DateTime.Now

                            };
                            context.TotalAlarms.Add(alarm);
                            context.SaveChanges();


                            //Retrieve data from stored procedure
                            var alarms = context.GetTotalAlarms();

                            foreach (var x in alarms)
                            {
                                this.totalAlarms.Text = Convert.ToString(x.Value);
                            }
                        }

                        break;

                    case "POLICE":
                        MessageBox.Show("The patient calls for police...");
                        alarmCount++;
                        this.alarmCountText.Text = Convert.ToString(alarmCount);

                        using (MasterEntities context = new MasterEntities())
                        {
                            var alarm = new TotalAlarm()
                            {
                                Date = DateTime.Now

                            };
                            context.TotalAlarms.Add(alarm);
                            context.SaveChanges();


                            //Retrieve data from stored procedure
                            var alarms = context.GetTotalAlarms();

                            foreach (var x in alarms)
                            {
                                this.totalAlarms.Text = Convert.ToString(x.Value);
                            }
                        }

                        break;
                    default:
                        Console.WriteLine("Default case");
                        break;

                }
            }


        }
    }
}