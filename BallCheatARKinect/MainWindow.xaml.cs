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
        bool isDisplayOn = true;

        int poolDepth = 0;             //당구대 높이값
        int poolDepthRange = 10;       //당구대 높이값 오차범위
        int ballDepth = 10;            //공 높이값
        int ballDepthRange = 3;        //공 높이값 오차범위
        int ballSizeMin = 2;
        int ballSizeMax = 10;

        int ballDrawingRadiusMultiply = 2;  //출력할 때 공 동그라미 그리는거 크기 반지름배수

        bool isPoolDepthSetting = false;
        bool isBallDepthSetting = false;
        int poolPosSetting = 0;

        Point[] poolPos = new Point[2]; //Region of Interest
        int poolWidth, poolHeight;  //poolPos 설정시에 자동으로 설정

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
                depthBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                //SetDisplay();

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

            CvInvoke.cvNamedWindow("Display");
        }


        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            bool isCalculate = !isPoolDepthSetting && !isBallDepthSetting && (poolPosSetting == 0);

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    if (depthFrame != null)
                    {
                        depthFrame.CopyDepthImagePixelDataTo(depthPixels);
                        depthBitmap.WritePixels(
                            new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                            this.depthPixels, this.depthBitmap.PixelWidth * sizeof(int), 0);
                        imgDepth = new Image<Bgr, byte>(depthBitmap.ToBitmap());

                        if(isCalculate)
                        imgBall = new Image<Bgr, byte>(depthFrame.SliceDepthImage(ballDepth - ballDepthRange, ballDepth + ballDepthRange).ToBitmap());
                        
                    }
                }

                if (colorFrame != null)
                {

                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels, this.colorBitmap.PixelWidth * sizeof(int), 0);
                    imgColor = new Image<Bgr, byte>(colorBitmap.ToBitmap());

                }

               
            }

            if (isDisplayOn)
                SetDisplay();
            else
                imgOutMain.Source = null;
            CvInvoke.cvShowImage("Display", imgColor);

        }

        /// <summary>
        /// Set display image
        /// </summary>
        private void SetDisplay()
        {
            switch ((DisplayImg)Convert.ToInt32(imgOutMain.Tag))
            {
                case DisplayImg.TOTAL: imgOutMain.Source = ImageHelpers.ToBitmapSource(imgColor); break;
                case DisplayImg.BALL: imgOutMain.Source = ImageHelpers.ToBitmapSource(imgBall); break;
                case DisplayImg.DEPTH: imgOutMain.Source = depthBitmap; break;//ImageHelpers.ToBitmapSource(imgDepth); break;
                case DisplayImg.CUE: imgOutMain.Source = ImageHelpers.ToBitmapSource(imgCue); break;
                case DisplayImg.DISPLAY: imgOutMain.Source = ImageHelpers.ToBitmapSource(imgDisplay); break;
            }
        }

        /// <summary>
        /// Swap main and sub image.
        /// </summary>
        /// <param name="subImgNum">Sub image number which will swapped with main image.</param>
        private void ChangeDisplay(Button btn)
        {
            isDisplayOn = true;
            imgOutMain.Tag = btn.Tag;
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

        /// <summary>
        /// Exit
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            if (imgDisplay != null) imgDisplay.Dispose();
            if(imgDepth != null) imgDepth.Dispose();
            if(imgCue != null) imgCue.Dispose();
            if(imgBall != null) imgBall.Dispose();
            if(imgColor != null) imgColor.Dispose();
            sensor.AllFramesReady -= sensor_AllFramesReady;
            //Close();
            if (sensor != null) sensor.Stop();
            Application.Current.Shutdown();
        }
        #endregion

        private void btnDisplay_Click(object sender, RoutedEventArgs e)
        {
            ChangeDisplay((Button)sender);
        }

        private void btnDisplayOff_Click(object sender, RoutedEventArgs e)
        { isDisplayOn = false; }

        /// <summary>
        /// ImageMain Mousedown Method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void imgOutMain_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point mPos = e.GetPosition(imgOutMain);

            if(isBallDepthSetting)
            {
                ballDepth = depthPixels[(int)(mPos.X + (mPos.Y * 640))].Depth;
                lblBallDepthVal.Text = ballDepth.ToString();
                isBallDepthSetting = false;
            }
            else if(isPoolDepthSetting)
            {
                poolDepth = depthPixels[(int)(mPos.X + (mPos.Y * 640))].Depth;
                lblPoolDepthVal.Text = poolDepth.ToString();
                isPoolDepthSetting = false;
            }
            else
            {
                if(poolPosSetting == 1)
                {
                    poolPos[0] = mPos;
                    lblPoolPos1Val.Text = mPos.ToString();
                    poolPosSetting = 2;
                }
                else if(poolPosSetting == 2)
                {
                    poolPos[1] = mPos;
                    //Calculate poolPos's width and height
                    poolWidth = (int)Math.Abs(poolPos[1].X - poolPos[0].X);
                    poolHeight = (int)Math.Abs(poolPos[1].Y - poolPos[0].Y);
                    lblPoolPos2Val.Text = mPos.ToString();
                    poolPosSetting = 0;
                }
            }
        }
        private void imgOutMain_MouseMove(object sender, MouseEventArgs e)
        {
            Point mPos = e.GetPosition(imgOutMain);
            if (isBallDepthSetting)
            {
                lblBallDepthVal.Text = ballDepth.ToString();
            }
            else if (isPoolDepthSetting)
            {
                lblPoolDepthVal.Text = poolDepth.ToString();
            }
            else if (poolPosSetting != 0)
            {
                if (poolPosSetting == 1) lblPoolPos1Val.Text = mPos.ToString();
                if (poolPosSetting == 2) lblPoolPos2Val.Text = mPos.ToString();
                CvInvoke.cvLine(imgColor, new System.Drawing.Point((int)mPos.X, 0), new System.Drawing.Point((int)mPos.X, 480), new MCvScalar(100, 100, 100), 1, Emgu.CV.CvEnum.LINE_TYPE.FOUR_CONNECTED, 0);
                CvInvoke.cvLine(imgColor, new System.Drawing.Point(0, (int)mPos.Y), new System.Drawing.Point(640, (int)mPos.Y), new MCvScalar(100, 100, 100), 1, Emgu.CV.CvEnum.LINE_TYPE.FOUR_CONNECTED, 0);
            }
        }

        #region Slider Variable Binding
        private void sldPoolDepthRange_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            poolDepthRange = (int)((Slider)sender).Value;
        }

        private void sldBallDepthRange_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ballDepthRange = (int)((Slider)sender).Value;
        }
        private void sldBallSizeMin_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ballSizeMin = (int)((Slider)sender).Value;
        }


        private void sldBallSizeMax_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ballSizeMax = (int)((Slider)sender).Value;
        }
        #endregion

    }
}
