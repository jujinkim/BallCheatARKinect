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
        enum DisplayImg { TOTAL, DEPTH, BALL, CUE, DISPLAY }

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
        Image<Gray, byte> imgBallGray;
        #endregion

        #region private variables
        bool isDisplayOn = true;

        int poolDepth = 0;             //당구대 높이값
        int poolDepthRange = 10;       //당구대 높이값 오차범위
        int ballDepth = 10;            //공 높이값
        int ballDepthRange = 3;        //공 높이값 오차범위
        int ballSizeMin = 2;
        int ballSizeMax = 10;

        int ballDrawingRadiusMultiply = 20;  //출력할 때 공 동그라미 그리는거 크기 반지름배수

        bool isPoolDepthSetting = true;
        bool isBallDepthSetting = true;
        int poolPosSetting = 1;

        System.Drawing.Point[] poolPos = new System.Drawing.Point[2]; //Region of Interest
        int poolWidth = 640, poolHeight = 480;  //poolPos 설정시에 자동으로 설정

        System.Drawing.PointF[] srcs = new System.Drawing.PointF[4];
        System.Drawing.PointF[] dest = new System.Drawing.PointF[4];

        int ballCount = 0;
        HomographyMatrix mywarpmat;
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
                { sensor.Start(); }
                catch (IOException ee)
                {
                    log(ee.Message);
                    sensor = null;
                }
           
                //Kinect Tilt
                //sldKinectTilt.Value = sensor.ElevationAngle;
                sldKinectTilt.Maximum = sensor.MaxElevationAngle;
                sldKinectTilt.Minimum = sensor.MinElevationAngle;
                CvInvoke.cvNamedWindow("Display");

                poolPos[0] = new System.Drawing.Point(0, 0);
                poolPos[1] = new System.Drawing.Point(0, 0);

                //output warping point setting
                srcs[0] = new System.Drawing.PointF(0, 0);
                srcs[1] = new System.Drawing.PointF(640, 0);
                srcs[2] = new System.Drawing.PointF(640, 480);
                srcs[3] = new System.Drawing.PointF(0, 480);
                srcs.CopyTo(dest, 0);

                //image initialize
                imgDisplay = new Image<Bgr, byte>(poolWidth, poolHeight, new Bgr(System.Drawing.Color.Black));
                imgDepth = new Image<Bgr, byte>(640, 480);
                imgBall = new Image<Bgr, byte>(640, 480);
                imgColor = new Image<Bgr, byte>(640, 480);
                imgBallGray = new Image<Gray, byte>(640, 480);

                mywarpmat = CameraCalibration.GetPerspectiveTransform(srcs, dest);

            }

            if (sensor == null)
            {
                log("Kinect Connect Fail");
                pnlSetting.IsEnabled = false;
                pnlSetting2.IsEnabled = false;
            }
        }

        /// <summary>
        /// Kinect All Frames Ready Update callback method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            bool isCalculate = !isPoolDepthSetting && !isBallDepthSetting && (poolPosSetting == 0);

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    //Clear imgDisplay
                    imgDisplay.Resize(poolWidth, poolHeight, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR);
                    imgDisplay.SetZero();
                    //imgDisplay.SetZero();
                    imgDisplay.Draw(new System.Drawing.Rectangle(0, 0, imgDisplay.Width, imgDisplay.Height), new Bgr(System.Drawing.Color.White), 3);

                    if (depthFrame != null)
                    {
                        depthFrame.CopyDepthImagePixelDataTo(depthPixels);

                        ballCount = 0;

                        depthBitmap.WritePixels(
                            new Int32Rect(0, 0, this.depthBitmap.PixelWidth, this.depthBitmap.PixelHeight),
                            this.depthPixels, this.depthBitmap.PixelWidth * sizeof(int), 0);
                        imgDepth.Bitmap = depthBitmap.ToBitmap();

                        //Calculate (all settings are done)
                        if (isCalculate)
                        {
                            imgBall.Bitmap = depthFrame.SliceDepthImage(ballDepth - ballDepthRange, ballDepth + ballDepthRange).ToBitmap();

                            imgBallGray = imgBall.Convert<Gray, byte>();
                            //CvInvoke.cvCvtColor(imgBall, imgBallGray, Emgu.CV.CvEnum.COLOR_CONVERSION.BGR2GRAY);
                            imgBallGray.ROI = new System.Drawing.Rectangle((int)poolPos[0].X, (int)poolPos[0].Y, poolWidth, poolHeight);

                            //imgBall labeling
                            using (MemStorage stor = new MemStorage())
                            {
                                //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                                Contour<System.Drawing.Point> contours = imgBallGray.FindContours(
                                 Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                 Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL, stor);

                                for (int i = 0; contours != null; contours = contours.HNext)
                                {
                                    i++;

                                    if ((contours.Area > Math.Pow(ballSizeMin, 2)) && (contours.Area < Math.Pow(ballSizeMax, 2)))
                                    {
                                        //Draw balls
                                        MCvBox2D box = contours.GetMinAreaRect();
                                        //CvInvoke.cvCircle(imgBall, new System.Drawing.Point((int)box.center.X, (int)box.center.Y), ballDrawingRadiusMultiply, new MCvScalar(0, 0, 255), 2, Emgu.CV.CvEnum.LINE_TYPE.FOUR_CONNECTED, 0);
                                        imgBall.Draw(new CircleF(box.center, ballDrawingRadiusMultiply), new Bgr(System.Drawing.Color.Blue), 2);
                                        System.Drawing.Point ballPos = new System.Drawing.Point((int)(box.center.X - poolPos[0].X), (int)(box.center.Y - poolPos[0].Y));
                                        imgDisplay.Draw(new CircleF(ballPos, ballDrawingRadiusMultiply), new Bgr(System.Drawing.Color.White), 1);
                                        //CvInvoke.cvCircle(imgDisplay, ballPos, ballDrawingRadiusMultiply, new MCvScalar(255, 255, 255), 2, Emgu.CV.CvEnum.LINE_TYPE.FOUR_CONNECTED, 0);
                                        ballCount++;
                                    }
                                }
                            }   //end labeling
                            lblSttBallCountVal.Text = ballCount.ToString();

                            
                        }   //End Calculate
                    }
                }

                if (colorFrame != null)
                {

                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels, this.colorBitmap.PixelWidth * sizeof(int), 0);
                    imgColor.Bitmap = colorBitmap.ToBitmap();

                    //draw pool position dot
                    //CvInvoke.cvCircle(imgColor, poolPos[0] )
                    //imgColor.Draw(new CircleF(new System.Drawing.PointF((float)poolPos[0].X, (float)poolPos[0].Y), 1), new Bgr(System.Drawing.Color.Red), 1);

                    //draw pool position setting guideline
                    if (!isPoolDepthSetting && !isBallDepthSetting)
                        try
                        {
                            if (poolPosSetting == 1)
                            {
                                if (lblPoolPos1Val.Text.IndexOf(',') > 0)
                                {
                                    System.Windows.Point mP = System.Windows.Point.Parse(lblPoolPos1Val.Text);
                                    System.Drawing.PointF mPF = new System.Drawing.PointF((float)mP.X, (float)mP.Y);
                                    imgColor.Draw(new Cross2DF(mPF, 1280, 960), new Bgr(System.Drawing.Color.LightGreen), 1);
                                 }
                            }
                            else if (poolPosSetting == 2)
                            {
                                if (lblPoolPos2Val.Text.IndexOf(',') > 0)
                                {
                                    System.Windows.Point mP = System.Windows.Point.Parse(lblPoolPos2Val.Text);
                                    System.Drawing.PointF mPF = new System.Drawing.PointF((float)mP.X, (float)mP.Y);
                                    imgColor.Draw(new Cross2DF(mPF, 1280, 960), new Bgr(System.Drawing.Color.LightGreen), 1);
                                }
                            }
                        }
                        catch { }

                    if(isCalculate)
                    {
                        //Draw pool area rectangle
                        imgColor.Draw(new System.Drawing.Rectangle(poolPos[0].X, poolPos[0].Y, poolWidth, poolHeight), new Bgr(System.Drawing.Color.White), 2);
                        //CvInvoke.cvRectangle(imgColor, poolPos[0], poolPos[1], new MCvScalar(255, 255, 255), 1, Emgu.CV.CvEnum.LINE_TYPE.FOUR_CONNECTED, 0);
                    }
                }


            }

            if (isDisplayOn)
                SetDisplay();
            else
                imgOutMain.Source = null;

            //imgDisplay = imgDisplay.WarpPerspective(mywarpmat, Emgu.CV.CvEnum.INTER.CV_INTER_NN, Emgu.CV.CvEnum.WARP.CV_WARP_FILL_OUTLIERS, new Bgr(0, 0, 0));
            CvInvoke.cvShowImage("Display", imgDisplay);// imgDisplay.WarpPerspective(mywarpmat, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR, Emgu.CV.CvEnum.WARP.CV_WARP_DEFAULT, new Bgr(0, 0, 0)));

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
                case DisplayImg.DEPTH: imgOutMain.Source = depthBitmap; break;// ImageHelpers.ToBitmapSource(imgDepth); break;
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

        #region Window function
        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sensor != null) sensor.Stop();
        }
        private void textBlock_MouseDown(object sender, MouseButtonEventArgs e)
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
            GC.Collect();
            Application.Current.Shutdown();
        }
        #endregion


        private void sldKinectTilt_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            sensor.ElevationAngle = (int)sldKinectTilt.Value;
        }

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
            System.Windows.Point mPos = e.GetPosition(imgOutMain);
            Point mPosF = new Point((float)mPos.X, (float)mPos.Y);

            if(isBallDepthSetting)
            {
                ballDepth = depthPixels[(int)(mPos.X + (mPos.Y * 640))].Depth;
                lblBallDepthVal.Text = ballDepth.ToString();
                lblBallDepthVal.FontWeight = FontWeights.Normal;
                isBallDepthSetting = false;
            }
            else if(isPoolDepthSetting)
            {
                poolDepth = depthPixels[(int)(mPos.X + (mPos.Y * 640))].Depth;
                lblPoolDepthVal.Text = poolDepth.ToString();
                lblPoolDepthVal.FontWeight = FontWeights.Normal;
                isPoolDepthSetting = false;
            }
            else
            {
                if(poolPosSetting == 1)
                {
                    poolPos[0] = WPoint2DPoint(mPosF);
                    lblPoolPos1Val.Text = mPos.ToString();
                    lblPoolPos1Val.FontWeight = FontWeights.Normal;
                    poolPosSetting = 2;
                }
                else if(poolPosSetting == 2)
                {
                    poolPos[1] = WPoint2DPoint(mPosF);
                    //Calculate poolPos's width and height
                    poolWidth = (int)Math.Abs(poolPos[1].X - poolPos[0].X);
                    poolHeight = (int)Math.Abs(poolPos[1].Y - poolPos[0].Y);
                    lblPoolPos2Val.Text = mPos.ToString();
                    //Set imgDisplay's Size
                    imgDisplay.Resize(poolWidth, poolHeight, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR);
                    lblPoolPos2Val.FontWeight = FontWeights.Normal;
                    poolPosSetting = 0;
                }
            }
        }
        private void imgOutMain_MouseMove(object sender, MouseEventArgs e)
        {
            System.Windows.Point mPos = e.GetPosition(imgOutMain);
            if (isBallDepthSetting)
            {
                if ((int)(mPos.X + (mPos.Y * 640)) < depthPixels.Length) 
                    ballDepth = depthPixels[(int)(mPos.X + (mPos.Y * 640))].Depth;
                lblBallDepthVal.Text = ballDepth.ToString();
                lblBallDepthVal.FontWeight = FontWeights.Bold;
            }
            else if (isPoolDepthSetting)
            {
                if ((int)(mPos.X + (mPos.Y * 640)) < depthPixels.Length)
                    poolDepth = depthPixels[(int)(mPos.X + (mPos.Y * 640))].Depth;
                lblPoolDepthVal.Text = poolDepth.ToString();
                lblPoolDepthVal.FontWeight = FontWeights.Bold;
            }
            else if (poolPosSetting != 0)
            {
                if (poolPosSetting == 1) { lblPoolPos1Val.Text = mPos.ToString(); lblPoolPos1Val.FontWeight = FontWeights.Bold; }
                if (poolPosSetting == 2) { lblPoolPos2Val.Text = mPos.ToString(); lblPoolPos2Val.FontWeight = FontWeights.Bold; }
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

        #region Parameter Setting Button Binding
        private void btnSetPoolPos_Click(object sender, RoutedEventArgs e)
        {
            poolPosSetting = 1;
        }

        private void btnSetBallDepth_Click(object sender, RoutedEventArgs e)
        {
            isBallDepthSetting = true;
        }

        private void btnSetPoolDepth_Click(object sender, RoutedEventArgs e)
        {
            isPoolDepthSetting = true;
        }
        #endregion

        /// <summary>
        /// Output Image Button Warping Adjust Method
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOutWrap_Click(object sender, RoutedEventArgs e)
        {
            string tag = ((Button)sender).Tag.ToString();
            string pos = tag.Substring(0, 2);
            char dir = tag[2];
            System.Drawing.PointF nextPos;
            int pointNum = 0;
            switch(pos)
            {
                case "LT": pointNum = 0; break;
                case "RT": pointNum = 1; break;
                case "RB": pointNum = 2; break;
                case "LB": pointNum = 3; break;
            }
            nextPos = dest[pointNum];

            switch(dir)
            {
                case 'U': nextPos.Y -= nextPos.Y > 0 ? 1 : 0; break;
                case 'R': nextPos.X += nextPos.X < poolWidth-1 ? 1 : 0; break;
                case 'D': nextPos.Y += nextPos.Y < poolHeight-1 ? 1 : 0; break;
                case 'L': nextPos.X -= nextPos.X > 0 ? 1 : 0; break;
            }

            dest[pointNum] = nextPos;

            mywarpmat = CameraCalibration.GetPerspectiveTransform(srcs, dest);
        }

        #region Drawing.Point Windows.Point Converter
        System.Drawing.Point WPoint2DPoint(Point p)
        { return new System.Drawing.Point((int)p.X, (int)p.Y);}
        Point DPoint2WPoint(System.Drawing.Point p)
        { return new Point(p.X, p.Y); }
        #endregion
    }
}
