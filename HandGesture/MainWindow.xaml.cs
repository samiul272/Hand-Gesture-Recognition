using Emgu.CV;
using Emgu.Util;
using Emgu.CV.Structure;
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
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Controls.Primitives;
using MahApps.Metro.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO.Ports;
using System.Diagnostics;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;
using System.Drawing;

namespace HandGesture
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, INotifyPropertyChanged
    {

        #region Slider bindings
        int hueLower = 50;
        public int HueLower
        {
            get { return hueLower; }
            set { hueLower = value; RaisePropertyChanged(); }
        }
        int hueHigher = 100;
        public int HueHigher
        {
            get { return hueHigher; }
            set { hueHigher = value; RaisePropertyChanged(); }
        }
        int hueMax = 180;
        public int HueMax
        {
            get { return hueMax; }
            set { hueMax = value; RaisePropertyChanged(); }
        }
        int hueMin = 0;
        public int HueMin
        {
            get { return hueMin; }
            set { hueMin = value; RaisePropertyChanged(); }
        }

        int saturationLower = 100;
        public int SaturationLower
        {
            get { return saturationLower; }
            set { saturationLower = value; RaisePropertyChanged(); }
        }
        int saturationHigher = 200;
        public int SaturationHigher
        {
            get { return saturationHigher; }
            set { saturationHigher = value; RaisePropertyChanged(); }
        }
        int saturationMax = 255;
        public int SaturationMax
        {
            get { return saturationMax; }
            set { saturationMax = value; RaisePropertyChanged(); }
        }
        int saturationMin = 0;
        public int SaturationMin
        {
            get { return saturationMin; }
            set { saturationMin = value; RaisePropertyChanged(); }
        }

        int valueLower = 100;
        public int ValueLower
        {
            get { return valueLower; }
            set { valueLower = value; RaisePropertyChanged(); }
        }
        int valueHigher = 200;
        public int ValueHigher
        {
            get { return valueHigher; }
            set { valueHigher = value; RaisePropertyChanged(); }
        }
        int valueMax = 255;
        public int ValueMax
        {
            get { return valueMax; }
            set { valueMax = value; RaisePropertyChanged(); }
        }
        int valueMin = 0;
        public int ValueMin
        {
            get { return valueMin; }
            set { valueMin = value; RaisePropertyChanged(); }
        }

        int cannyThresh = 160;
        public int CannyThresh
        {
            get { return cannyThresh; }
            set { cannyThresh = value; RaisePropertyChanged(); }
        }
        int cannyThreshLink = 80;
        public int CannyThreshLink
        {
            get { return cannyThreshLink; }
            set { cannyThreshLink = value; RaisePropertyChanged(); }
        }

        public object up { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string prop = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
        #endregion

        #region Bitamp Source
        [DllImport("gdi32")]
        private static extern int DeleteObject(IntPtr o);

        public static BitmapSource ToBitmapSource(IImage image)
        {
            using (System.Drawing.Bitmap source = image.Bitmap)
            {
                IntPtr ptr = source.GetHbitmap(); //obtain the Hbitmap

                BitmapSource bs = System.Windows.Interop
                  .Imaging.CreateBitmapSourceFromHBitmap(
                  ptr,
                  IntPtr.Zero,
                  Int32Rect.Empty,
                  System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());

                DeleteObject(ptr); //release the HBitmap
                return bs;
            }
        }
        #endregion

        Stopwatch sw = new Stopwatch();
        private Capture capture;
        DispatcherTimer timer;
        Image<Hsv, Byte> currentFrame;
        Image<Hsv, Byte> smoothedFrame;
        Image<Gray, Byte> filteredFrame;
        Image<Gray, Byte> outFrame;
        Image<Hsv, Byte> backgroundFrame = new Image<Hsv, byte>(640, 480);
        Image<Hsv, Byte> image = new Image<Hsv, byte>(640, 480);
        VectorOfInt cvh = new VectorOfInt();
        Mat cvd = new Mat();
        System.Drawing.Point[] polyline = new System.Drawing.Point[640*480];
        VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
        VectorOfVectorOfPoint trained = new VectorOfVectorOfPoint();
        List<string> words = new List<string>();
        MCvMoments moments = new MCvMoments();

        System.Drawing.Point centroid = new System.Drawing.Point();
        System.Drawing.Point startPoint = new System.Drawing.Point();
        System.Drawing.Point endPoint = new System.Drawing.Point();
        System.Drawing.Point midPoint = new System.Drawing.Point();
        System.Drawing.Point farthestPoint = new System.Drawing.Point();

        IOutputArray hierarchy;
        double maxArea = 0;
        int idx = 0;
        public MainWindow()
        {
            Hsv hsvLower = new Hsv(hueLower, saturationLower, valueLower);
            Hsv hsvHigher = new Hsv(hueHigher, saturationHigher, valueHigher);
            Gray cannyThreshold = new Gray(cannyThresh);
            Gray cannyThresholdLinking = new Gray(cannyThresh);
            new Gray(cannyThreshLink);

            InitializeComponent();
            try
            {
                capture = new Capture();
            }
            catch (NullReferenceException except)
            {
                MessageBox.Show(except.Message);
            }

            Image<Hsv, Byte> currentFrame = capture.QueryFrame().ToImage<Hsv, byte>();
            imgOrig.Source = ToBitmapSource(currentFrame);
            outFrame = currentFrame.CopyBlank().Convert<Gray, byte>();
            timer = new DispatcherTimer();
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = new TimeSpan(0, 0, 0, 0, 80);
            timer.Start();
        }
        void timer_Tick(object sender, EventArgs e)
        {
            sw.Start();

            currentFrame = capture.QueryFrame().ToImage<Hsv, byte>();
            currentFrame = currentFrame.AbsDiff(backgroundFrame);
            smoothedFrame = currentFrame.PyrDown().PyrUp();
            smoothedFrame._SmoothGaussian(3);
            filteredFrame = smoothedFrame.InRange(new Hsv(hueLower, saturationLower, valueLower), new Hsv(hueHigher, saturationHigher, valueHigher));

            outFrame = filteredFrame;//.Canny((cannyThresh), (cannyThreshLink));
            CvInvoke.FindContours(outFrame, contours, hierarchy, Emgu.CV.CvEnum.RetrType.List, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
            for (int i = 0; i < contours.Size; i++)
            {
                double area = CvInvoke.ContourArea(contours[i], false);
                if (area > maxArea)
                {
                    maxArea = area;
                    idx = i;
                }
            }
            image.SetValue(new MCvScalar(180, 0, 255));
            CvInvoke.DrawContours(image, contours, idx, new MCvScalar(255, 255, 255), 3);
            ContourArea.Text = maxArea.ToString();
            if (maxArea != 0)
            {
                CvInvoke.ConvexHull(contours[idx], cvh, false);
                CvInvoke.ConvexityDefects(contours[idx], cvh, cvd);
                moments = CvInvoke.Moments(contours[idx], true);
                centroid = new System.Drawing.Point((int) moments.GravityCenter.X, (int) moments.GravityCenter.Y);
                CvInvoke.Circle(image, centroid, 5, new MCvScalar(100,50,25), 1);
                if (contours.Size != 0)
                {
                    polyline = contours[idx].ToArray();

                    if (!cvd.IsEmpty && contours[idx].Size > 10)
                    {
                        Matrix<int> m = new Matrix<int>(cvd.Rows, cvd.Cols,
                                          cvd.NumberOfChannels);
                        cvd.CopyTo(m);
                        for (int i = 0; i < m.Rows; i++)
                        {
                            int startIdx = m.Data[i, 0];
                            int endIdx = m.Data[i, 1];
                            int fpIdx = m.Data[i, 2];
                            int depth = m.Data[i, 3];
                            
                            startPoint = polyline[startIdx];
                            endPoint = polyline[endIdx];
                            midPoint = new System.Drawing.Point(
                                (startPoint.X + endPoint.X) / 2, (startPoint.Y + endPoint.Y) / 2);
                            farthestPoint = polyline[fpIdx];
                            CvInvoke.Line(image, midPoint, farthestPoint, new MCvScalar(180, 255, 0));
                            CvInvoke.Line(image, startPoint, endPoint, new MCvScalar(180, 255, 255));
                        }
                    }

                    if(trained.Size!=0)
                    {
                        double match=1000000;
                        
                        int d = 0;
                        for (int i = 0; i  < trained.Size; i++)
                        {
                            double curr = CvInvoke.MatchShapes(contours[idx], trained[i], ContoursMatchType.I3);
                            if(curr < match)
                            {
                                d = i;
                                match = curr;
                            }

                        }
                        if(match<0.25)
                        {
                            ContourArea.Text = words[d];
                            image.Draw(words[d], centroid, FontFace.HersheyTriplex, 1, new Hsv(90,100, 100));
                        }
                        
                    }
                }
            }



            if (currentFrame != null)
            {
                sw.Stop();
                imgPros.Source = ToBitmapSource(outFrame);
                imgOrig.Source = ToBitmapSource(currentFrame);
                imgSmooth.Source = ToBitmapSource(image);

                
                sw.Reset();
            }

            maxArea = 0;
            idx = 0;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            backgroundFrame = capture.QueryFrame().ToImage<Hsv, byte>();
        }

        private void train(object sender, RoutedEventArgs e)
        {
            CvInvoke.FindContours(outFrame, contours, hierarchy, Emgu.CV.CvEnum.RetrType.List, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
            for (int i = 0; i < contours.Size; i++)
            {
                double area = CvInvoke.ContourArea(contours[i], false);
                if (area > maxArea)
                {
                    maxArea = area;
                    idx = i;
                }
            }
            words.Add(Word.Text);
            trained.Push(contours[idx]);
            
            maxArea = 0;
            idx = 0;
        }
    }
}
