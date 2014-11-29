//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using System;

    //Para mostrar la imagen a color
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        //Variables de imagen en color
        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the color data received from the camera
        /// </summary>
        private byte[] colorPixels;
        
        //Informacion de feedback
        private string info = "Iniciando ejercicio...";

        //Variable para controlar la secuencia de movimientos
        private int estado = 0;

        //Variable para comprobar el numero de repeticiones
        private int repes = 0;


        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.infoFeedbackText.Text = this.info;
            this.repeticionesText.Text = "Mov / Rep:\n" + this.estado + " / " + this.repes;
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (this.sensor != null)
            {
                // Display the drawing using our image control
                this.Image.Source = this.imageSource;

                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;


                // Turn on the color stream to receive color frames
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

                // Set the image we display to point to the bitmap where we'll put the image data
                this.ImageColor.Source = this.colorBitmap;

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.infoFeedbackText.Text = Properties.Resources.NoKinectReady;
            }

        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// Se añade la funcion para mostrar la imagen en color captada por la kinect
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }
        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            //Se comprueba en que estado del ejercicio se encuentra
            this.comprobarEstado(skeleton, drawingContext);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        //Comprueba en que estado se encuentra el ejercicio
        private void comprobarEstado(Skeleton skeleton, DrawingContext drawingContext)
        {
            //Estado inicial, no cambia de estado hasta que el usuario se encuentra en la posicion inicial
            //Piernas abiertas a la altura de los hombros y los brazos relajados pegados al tronco
            if (this.estado == 0)
            {
                this.infoFeedbackText.Text = "Separa las piernas a la altura de los hombros y baja los brazos hasta pegarlos al tronco";
                this.marcasMov0(skeleton, drawingContext);
                if (this.posicionInicial(skeleton)) this.estado = 1;
                this.repeticionesText.Text = "Mov / Rep:\n" + this.estado + " / " + this.repes;
            }
            //Completada la posicion inicial se comprueba el movimiento uno
            //Extender los brazos hacia los lados a la altura de los hombros y flexionar un poco las rodillas
            if (this.estado == 1)
            {
                this.infoFeedbackText.Text = "Bien, has completado la posicion inicial\nAhora extiende los brazos hacia los lados, en cruz y flexiona las rodillas";
                this.marcasMov1(skeleton, drawingContext);
                if (this.movimiento1(skeleton)) this.estado = 2;
                this.repeticionesText.Text = "Mov / Rep:\n" + this.estado + " / " + this.repes;
            }
            //Completado el movimiento uno comprueba el movimiento dos
            //Levantar los brazos por encima de la cabeza en el eje X hasta juntar las manos
            if (this.estado == 2)
            {
                this.infoFeedbackText.Text = "Bien, falta poco! Flexiona mas las rodillas y da una palmada levantando las manos por encima de la cabeza";
                this.marcasMov2(skeleton, drawingContext);
                if (this.movimiento2(skeleton)) this.estado = 3;
                this.repeticionesText.Text = "Mov / Rep:\n" + this.estado + " / " + this.repes;
            }
            //Completado el ejercicio se incrementa el contador de repeticiones, se vuelve al estado inicial para repetir el ejercicio
            if (this.estado == 3)
            {
                this.infoFeedbackText.Text = "Muy bien, has completado el ejercicio!!! Vuelve a la posicion inicial";
                this.repes++;
                this.repeticionesText.Text = "Repeticiones:\n" + this.repes;
                this.estado = 0;
                this.repeticionesText.Text = "Mov / Rep:\n" + this.estado + " / " + this.repes;
            }
        }

        //Comprueba que se encuentra en la posicion inicial pies separados a la altura de los hombros y las manos abajo pegadas al tronco
        private bool posicionInicial(Skeleton skeleton)
        {
            Joint tobilloDe = skeleton.Joints[JointType.AnkleRight];
            Joint tobilloIz = skeleton.Joints[JointType.AnkleLeft];
            Joint caderaDe = skeleton.Joints[JointType.KneeRight];
            Joint caderaIz = skeleton.Joints[JointType.KneeLeft];
            Joint rodillaDe = skeleton.Joints[JointType.HipRight];
            Joint rodillaIz = skeleton.Joints[JointType.HipLeft];
            Joint hombroDe = skeleton.Joints[JointType.ShoulderRight];
            Joint hombroIz = skeleton.Joints[JointType.ShoulderLeft];
            Joint codoDe = skeleton.Joints[JointType.ElbowRight];
            Joint codoIz = skeleton.Joints[JointType.ElbowLeft];
            Joint muniecaDe = skeleton.Joints[JointType.WristRight];
            Joint muniecaIz = skeleton.Joints[JointType.WristLeft];

            bool pos = false;
            double separacion = Math.Abs(hombroDe.Position.X - hombroIz.Position.X);

            //Comprueba que los brazos estan alineados hacia abajo
            if (Math.Abs(tobilloDe.Position.X - tobilloIz.Position.X) > separacion && this.brazosAlineados(skeleton) &&
                hombroDe.Position.Y > muniecaDe.Position.Y && hombroIz.Position.Y > muniecaIz.Position.Y &&
                Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) < 0.2 && Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) > 0.0 &&
                Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) > 0.85 &&
                Math.Abs(tobilloIz.Position.X - caderaIz.Position.X) < 0.2 && Math.Abs(tobilloIz.Position.X - caderaIz.Position.X) > 0.0 &&
                Math.Abs(rodillaIz.Position.Y - tobilloIz.Position.Y) > 0.85)
            {
                pos = true;
            }
            else
            {
                //this.infoFeedbackText.Text += "\nX-TOd-CAd:" + Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) + "\terror: " + 0.2;
                //this.infoFeedbackText.Text += "\nY-TOd-ROd:" + Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) + "\terror: " + 0.85;
                //this.infoFeedbackText.Text += "\nTOd:" + tobilloDe.Position.X + "TOi:" + tobilloIz.Position.X + "\tSeparacion: " + separacion;
                //this.infoFeedbackText.Text += "\nMU-HOd:" + Math.Abs(muniecaDe.Position.X - hombroDe.Position.X) + "MU-COd:" + Math.Abs(muniecaDe.Position.X - codoDe.Position.X) + "\terror: " + hombroDe.Position.X * 0.15;
                //this.infoFeedbackText.Text += "\nMU-HOi:" + Math.Abs(muniecaIz.Position.X - hombroIz.Position.X) + "MU-COi:" + Math.Abs(muniecaIz.Position.X - codoIz.Position.X) + "\terror: " + hombroIz.Position.X * 0.15;
            }

            return pos;
        }

        //Brazos levantados a la altura del hombro
        private bool movimiento1(Skeleton skeleton)
        {
            Joint tobilloDe = skeleton.Joints[JointType.AnkleRight];
            Joint tobilloIz = skeleton.Joints[JointType.AnkleLeft];
            Joint rodillaDe = skeleton.Joints[JointType.HipRight];
            Joint rodillaIz = skeleton.Joints[JointType.HipLeft];
            Joint caderaDe = skeleton.Joints[JointType.HipRight];
            Joint caderaIz = skeleton.Joints[JointType.HipLeft];
            Joint hombroDe = skeleton.Joints[JointType.ShoulderRight];
            Joint hombroIz = skeleton.Joints[JointType.ShoulderLeft];
            Joint codoDe = skeleton.Joints[JointType.ElbowRight];
            Joint codoIz = skeleton.Joints[JointType.ElbowLeft];
            Joint muniecaDe = skeleton.Joints[JointType.WristRight];
            Joint muniecaIz = skeleton.Joints[JointType.WristLeft];

            bool pos = false;
            double errorIz = 0.15;
            double errorDe = 0.15;
            double separacion = Math.Abs(hombroDe.Position.X - hombroIz.Position.X);

            //Comprueba que las articulaciones hombro, codo y muñeca izquierda y derecha estan alineadas en el eje Y
            if (Math.Abs(tobilloDe.Position.X - tobilloIz.Position.X) > separacion &&
                Math.Abs(muniecaDe.Position.Y - hombroDe.Position.Y) < errorDe && Math.Abs(muniecaDe.Position.Y - hombroDe.Position.Y) > 0.0 &&
                Math.Abs(muniecaDe.Position.Y - codoDe.Position.Y) < errorDe && Math.Abs(muniecaDe.Position.Y - codoDe.Position.Y) > 0.0 &&
                Math.Abs(muniecaIz.Position.Y - hombroIz.Position.Y) < errorIz && Math.Abs(muniecaIz.Position.Y - hombroIz.Position.Y) > 0.0 &&
                Math.Abs(muniecaIz.Position.Y - codoIz.Position.Y) < errorIz && Math.Abs(muniecaIz.Position.Y - codoIz.Position.Y) > 0.0 &&
                Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) < 0.2 && Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) > 0.0 &&
                Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) < 0.8 && Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) > 0.0 &&
                Math.Abs(tobilloIz.Position.X - caderaIz.Position.X) < 0.2 && Math.Abs(tobilloIz.Position.X - caderaIz.Position.X) > 0.0 &&
                Math.Abs(rodillaIz.Position.Y - tobilloDe.Position.Y) < 0.8 && Math.Abs(rodillaIz.Position.Y - tobilloDe.Position.Y) > 0.0)
            {
                pos = true;
            }
            else
            {
                //this.infoFeedbackText.Text += "\nTOd-CAd:" + Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) + "\terror: " + 0.2;
                //this.infoFeedbackText.Text += "\nTOd-CAd:" + Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) + "\terror: " + 0.8;
                //this.infoFeedbackText.Text += "\nMU-HOd:" + Math.Abs(muniecaDe.Position.Y - hombroDe.Position.Y) + "MU-COd:" + Math.Abs(muniecaDe.Position.Y - codoDe.Position.Y) + "\terror: " + hombroDe.Position.Y * 0.15;
                //this.infoFeedbackText.Text += "\nMU-HOi:" + Math.Abs(muniecaIz.Position.Y - hombroIz.Position.Y) + "MU-COi:" + Math.Abs(muniecaIz.Position.Y - codoIz.Position.Y) + "\terror: " + hombroIz.Position.Y * 0.15;
            }

            return pos;
        }

        //Brazos levantados por encima de la cabeza
        private bool movimiento2(Skeleton skeleton)
        {
            Joint tobilloDe = skeleton.Joints[JointType.AnkleRight];
            Joint tobilloIz = skeleton.Joints[JointType.AnkleLeft];
            Joint rodillaDe = skeleton.Joints[JointType.HipRight];
            Joint rodillaIz = skeleton.Joints[JointType.HipLeft];
            Joint caderaDe = skeleton.Joints[JointType.HipRight];
            Joint caderaIz = skeleton.Joints[JointType.HipLeft];
            Joint hombroDe = skeleton.Joints[JointType.ShoulderRight];
            Joint hombroIz = skeleton.Joints[JointType.ShoulderLeft];
            Joint codoDe = skeleton.Joints[JointType.ElbowRight];
            Joint codoIz = skeleton.Joints[JointType.ElbowLeft];
            Joint muniecaDe = skeleton.Joints[JointType.WristRight];
            Joint muniecaIz = skeleton.Joints[JointType.WristLeft];

            bool pos = false;
            double separacion = Math.Abs(hombroDe.Position.X - hombroIz.Position.X);

            //Comprueba que los brazos estan alineados hacia arriba
            if ((Math.Abs(tobilloDe.Position.X - tobilloIz.Position.X) > separacion) && this.brazosAlineados(skeleton) &&
                hombroDe.Position.Y < muniecaDe.Position.Y && hombroIz.Position.Y < muniecaIz.Position.Y &&
                Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) < 0.2 && Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) > 0.0 &&
                Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) < 0.8 && Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) > 0.0 &&
                Math.Abs(tobilloIz.Position.X - caderaIz.Position.X) < 0.2 && Math.Abs(tobilloIz.Position.X - caderaIz.Position.X) > 0.0 &&
                Math.Abs(rodillaIz.Position.Y - tobilloDe.Position.Y) < 0.8 && Math.Abs(rodillaIz.Position.Y - tobilloDe.Position.Y) > 0.0)
            {
                pos = true;
            }
            else
            {
                //this.infoFeedbackText.Text += "\nTOd-CAd:" + Math.Abs(tobilloDe.Position.X - caderaDe.Position.X) + "\terror: " + 0.2;
                //this.infoFeedbackText.Text += "\nTOd-CAd:" + Math.Abs(rodillaDe.Position.Y - tobilloDe.Position.Y) + "\terror: " + 0.75;
                //this.infoFeedbackText.Text += "\nTOd:" + tobilloDe.Position.X + "TOi:" + tobilloIz.Position.X + "\tSeparacion: " + separacion;
                //this.infoFeedbackText.Text += "\nMU-HOd:" + Math.Abs(muniecaDe.Position.X - hombroDe.Position.X) + "MU-COd:" + Math.Abs(muniecaDe.Position.X - codoDe.Position.X) + "\terror: " + hombroDe.Position.X * 0.15;
                //this.infoFeedbackText.Text += "\nMU-HOi:" + Math.Abs(muniecaIz.Position.X - hombroIz.Position.X) + "MU-COi:" + Math.Abs(muniecaIz.Position.X - codoIz.Position.X) + "\terror: " + hombroIz.Position.X * 0.15;
            }

            return pos;
        }

        //Comprueba que los brazos estan alineados (llamada desde la funcion movimientoInicial y movimiento2)
        private bool brazosAlineados(Skeleton skeleton)
        {
            bool pos=false;

            Joint hombroDe = skeleton.Joints[JointType.ShoulderRight];
            Joint hombroIz = skeleton.Joints[JointType.ShoulderLeft];
            Joint codoDe = skeleton.Joints[JointType.ElbowRight];
            Joint codoIz = skeleton.Joints[JointType.ElbowLeft];
            Joint muniecaDe = skeleton.Joints[JointType.WristRight];
            Joint muniecaIz = skeleton.Joints[JointType.WristLeft];

            double errorIz = 0.15;
            double errorDe = 0.15;

            //Comprueba que las articulaciones hombro, codo y muñeca izquierda y derecha estan alineadas en el eje X
            if ((Math.Abs(muniecaDe.Position.X - hombroDe.Position.X) < errorDe) && (Math.Abs(muniecaDe.Position.X - hombroDe.Position.X) > 0) &&
                (Math.Abs(muniecaDe.Position.X - codoDe.Position.X) < errorDe) && (Math.Abs(muniecaDe.Position.X - codoDe.Position.X) > 0) &&
                (Math.Abs(muniecaIz.Position.X - hombroIz.Position.X) < errorIz) && (Math.Abs(muniecaIz.Position.X - hombroIz.Position.X) > 0) &&
                (Math.Abs(muniecaIz.Position.X - codoIz.Position.X) < errorIz) && (Math.Abs(muniecaIz.Position.X - codoIz.Position.X) > 0))
            {
                pos=true;
            }

            return pos;
        }

        //Dibuja las marcas para guiar al usuario
        private void marcasMov0(Skeleton sk, DrawingContext dr)
        {
            SkeletonPoint pos = new SkeletonPoint();
            Point p;
            pos.X = sk.Joints[JointType.HipRight].Position.X + 0.2f;
            pos.Y = sk.Joints[JointType.HipRight].Position.Y - 0.2f;
            pos.Z = sk.Joints[JointType.HipRight].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.HipLeft].Position.X - 0.2f;
            pos.Y = sk.Joints[JointType.HipLeft].Position.Y - 0.2f;
            pos.Z = sk.Joints[JointType.HipLeft].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.HipRight].Position.X + 0.15f;
            pos.Y = sk.Joints[JointType.AnkleRight].Position.Y;
            pos.Z = sk.Joints[JointType.AnkleRight].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.HipLeft].Position.X - 0.15f;
            pos.Y = sk.Joints[JointType.AnkleLeft].Position.Y;
            pos.Z = sk.Joints[JointType.AnkleLeft].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);


        }

        //Dibuja las marcas para guiar al usuario
        private void marcasMov1(Skeleton sk, DrawingContext dr)
        {
            SkeletonPoint pos = new SkeletonPoint();
            Point p;
            pos.X = sk.Joints[JointType.ShoulderCenter].Position.X + 0.7f;
            pos.Y = sk.Joints[JointType.ShoulderCenter].Position.Y - 0.2f;
            pos.Z = sk.Joints[JointType.ShoulderCenter].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.ShoulderCenter].Position.X - 0.7f;
            pos.Y = sk.Joints[JointType.ShoulderCenter].Position.Y - 0.2f;
            pos.Z = sk.Joints[JointType.ShoulderCenter].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.HipRight].Position.X + 0.15f;
            pos.Y = sk.Joints[JointType.AnkleRight].Position.Y;
            pos.Z = sk.Joints[JointType.AnkleRight].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.HipLeft].Position.X - 0.15f;
            pos.Y = sk.Joints[JointType.AnkleLeft].Position.Y;
            pos.Z = sk.Joints[JointType.AnkleLeft].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);


        }

        //Dibuja las marcas para guiar al usuario
        private void marcasMov2(Skeleton sk, DrawingContext dr)
        {
            SkeletonPoint pos = new SkeletonPoint();
            Point p;
            pos.X = sk.Joints[JointType.ShoulderCenter].Position.X + 0.1f;
            pos.Y = sk.Joints[JointType.ShoulderCenter].Position.Y + 0.5f;
            pos.Z = sk.Joints[JointType.ShoulderCenter].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.ShoulderCenter].Position.X - 0.1f;
            pos.Y = sk.Joints[JointType.ShoulderCenter].Position.Y + 0.5f;
            pos.Z = sk.Joints[JointType.ShoulderCenter].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.HipRight].Position.X + 0.15f;
            pos.Y = sk.Joints[JointType.AnkleRight].Position.Y;
            pos.Z = sk.Joints[JointType.AnkleRight].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);

            pos.X = sk.Joints[JointType.HipLeft].Position.X - 0.15f;
            pos.Y = sk.Joints[JointType.AnkleLeft].Position.Y;
            pos.Z = sk.Joints[JointType.AnkleLeft].Position.Z;
            p = this.SkeletonPointToScreen(pos);
            dr.DrawEllipse(this.trackedJointBrush, null, p, 5, 5);


        }

    }
}