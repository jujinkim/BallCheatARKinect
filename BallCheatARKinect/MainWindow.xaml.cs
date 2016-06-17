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
using Emgu.CV.Util;
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
        KinectSensor sensor;
        WriteableBitmap depthBitmap;
        WriteableBitmap colorBitmap;
        DepthImagePixel[] depthPixels;
        byte[] colorPixels;
        int blobCount = 0;

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

            if(sensor != null)
            {
                sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                colorPixels = new byte[sensor.ColorStream.FramePixelDataLength];
                depthPixels = new DepthImagePixel[sensor.DepthStream.FramePixelDataLength];
                colorBitmap = new WriteableBitmap(sensor.ColorStream.FrameWidth, sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                depthBitmap = new WriteableBitmap(sensor.DepthStream.FrameWidth, sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                imgOut.Source = colorBitmap;
                
                sensor.AllFramesReady += sensor_AllFramesReady;

                try
                {
                    sensor.Start();
                }
                catch (IOException ee)
                {
                    Console.WriteLine(ee.Message);
                    log(ee.Message);
                    sensor = null;
                }
            }

            if(sensor == null)
            {
                log("Kinect Connect Fail");
            }
        }


        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            BitmapSource depthBmp = null;
            blobCount = 0;

            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    if (depthFrame != null)
                    {

                        blobCount = 0;

                        depthBmp = depthFrame.SliceDepthImage((int)sldDepthMin.Value, (int)sldDepthMax.Value);

                        Image<Bgr, byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
                        Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

                        imgOut.Source = ImageHelpers.ToBitmapSource(openCVImg);
                        //txtBlobCount.Text = blobCount.ToString();
                    }
                }


                if (colorFrame != null)
                {

                    colorFrame.CopyPixelDataTo(colorPixels);
                    colorBitmap.WritePixels(
                        new Int32Rect(0, 0, colorBitmap.PixelWidth, colorBitmap.PixelHeight), colorPixels, colorBitmap.PixelWidth * sizeof(int), 0);

                }
            }
        }

        #region func
        private void frmMain_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sensor != null) sensor.Stop();
        }

        private void frmMain_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void log(string msg)
        {
            txtLog.AppendText(msg);
            txtLog.AppendText(Environment.NewLine);
        }
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion


    }
}
