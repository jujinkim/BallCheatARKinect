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
using Emgu.CV;
using Microsoft.Kinect;
using System.IO;
using Emgu.CV.Structure;

namespace BallCheatARKinect
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum DisplayImg { TOTAL, BALL, DEPTH, CUE, DISPLAY }

        #region Kinect Objects
        KinectSensor sensor;
        WriteableBitmap depthBitmap; 
        WriteableBitmap ballBitmap;  
        WriteableBitmap colorBitmap; 
        WriteableBitmap cueBitmap;   
        WriteableBitmap displayBitmap; 
        DepthImagePixel[] depthPixels;
        byte[] colorPixels;
        #endregion

        #region OpenCV Images
        Image<Bgr, byte> imgDepth;
        Image<Bgr, byte> imgBall;
        Image<Bgr, byte> imgColor;
        Image<Bgr, byte> imgCue;
        Image<Bgr, byte> imgDisplay;
        #endregion

        #region private variables
        int poolHeight = 0;             //당구대 높이값
        int poolHeightRange = 10;       //당구대 높이값 오차범위
        int ballHeight = 10;            //공 높이값
        int ballHeightRange = 3;        //공 높이값 오차범위

        int ballDrawingRadiusMultiply = 2;  //출력할 때 공 동그라미 그리는거 크기 반지름배수


        int blobCount = 0;
        
        #endregion


        public MainWindow()
        {
            InitializeComponent();
        }

        private void frmMain_Loaded(object sender, RoutedEventArgs e)
        {
            //Kinect connect
            foreach(var potentialSensor in KinectSensor.KinectSensors)
                if(potentialSensor.Status == KinectStatus.Connected)
                {
                    sensor = potentialSensor;
                    break;
                }

            //initialize Kinect sensor
            if(sensor != null)
            {
                sensor.DepthStream.Range = DepthRange.Near;
                sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                cueBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                displayBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                SetDisplay();

                sensor.AllFramesReady += sensor_AllFramesReady;

                try
                {
                    sensor.Start();
                }
                catch (IOException ee)
                {
                    log(ee.Message);
                    sensor = null;
                }
            }

            if(sensor == null)
            {
                log("Kinect Connect Fail");
                pnlSetting.IsEnabled = false;
            }
        }


        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {

                if (colorFrame != null)
                {

                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels, this.colorBitmap.PixelWidth * sizeof(int), 0);
                    imgColor = new Image<Bgr, byte>(colorBitmap.ToBitmap());
                }
            }

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    imgBall = new Image<Bgr, byte>(depthFrame.SliceDepthImage((int)sldDepthMin.Value, (int)sldDepthMax.Value).ToBitmap());

                    depthFrame.CopyDepthImagePixelDataTo(depthPixels);
                    depthBitmap.WritePixels(
                        new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                        this.depthPixels, this.depthBitmap.PixelWidth * sizeof(int), 0);
                    imgDepth = new Image<Bgr, byte>(depthBitmap.ToBitmap());
                }
            }

            imgColor.CopyTo(imgCue);
            
            
        }

        /// <summary>
        /// Set display image
        /// </summary>
        private void SetDisplay()
        {
            switch((DisplayImg)imgOutMain.Tag)
            {
                case DisplayImg.TOTAL: imgOutMain.Source = colorBitmap; break;
                case DisplayImg.BALL: imgOutMain.Source = ballBitmap; break;
                case DisplayImg.DEPTH: imgOutMain.Source = depthBitmap; break;
                case DisplayImg.CUE: imgOutMain.Source = cueBitmap; break;
                case DisplayImg.DISPLAY: imgOutMain.Source = displayBitmap; break;
            }
            switch ((DisplayImg)imgOutSub0.Tag)
            {
                case DisplayImg.TOTAL: imgOutSub0.Source = colorBitmap; break;
                case DisplayImg.BALL: imgOutMain.Source = ballBitmap; break;
                case DisplayImg.DEPTH: imgOutMain.Source = depthBitmap; break;
                case DisplayImg.CUE: imgOutMain.Source = cueBitmap; break;
                case DisplayImg.DISPLAY: imgOutMain.Source = displayBitmap; break;
            }
            switch ((DisplayImg)imgOutSub1.Tag)
            {
                case DisplayImg.TOTAL: imgOutSub1.Source = colorBitmap; break;
                case DisplayImg.BALL: imgOutSub1.Source = ballBitmap; break;
                case DisplayImg.DEPTH: imgOutSub1.Source = depthBitmap; break;
                case DisplayImg.CUE: imgOutSub1.Source = cueBitmap; break;
                case DisplayImg.DISPLAY: imgOutSub1.Source = displayBitmap; break;
            }
            switch ((DisplayImg)imgOutSub2.Tag)
            {
                case DisplayImg.TOTAL: imgOutSub2.Source = colorBitmap; break;
                case DisplayImg.BALL: imgOutSub2.Source = ballBitmap; break;
                case DisplayImg.DEPTH: imgOutSub2.Source = depthBitmap; break;
                case DisplayImg.CUE: imgOutSub2.Source = cueBitmap; break;
                case DisplayImg.DISPLAY: imgOutSub2.Source = displayBitmap; break;
            }
        }

        /// <summary>
        /// Swap main and sub image.
        /// </summary>
        /// <param name="subImgNum">Sub image number which will swapped with main image.</param>
        private void SwapDisplay(Image subImg)
        {
            int tmp = (int)imgOutMain.Tag;
            subImg.Tag = imgOutMain.Tag;
            imgOutMain.Tag = tmp;

            SetDisplay();
        }

        /// <summary>
        /// Logging Method
        /// </summary>
        /// <param name="msg">Message to log</param>
        private void log(string msg)
        {
            string tmpt = txtLog.Text;
            txtLog.Text = msg;
            txtLog.AppendText(Environment.NewLine);
            txtLog.AppendText(tmpt);
        }

        #region win func
        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sensor != null) sensor.Stop();
        }

        private void frmMain_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion

        private void imgOutSub_MouseUp(object sender, MouseButtonEventArgs e)
        {
            SwapDisplay((Image)sender);
        }
    }
}
