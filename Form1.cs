using GMap.NET.MapProviders;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.Json;
using System.Globalization;
using System.Threading.Tasks.Dataflow;
using System.IO;
using GMap.NET.WindowsForms;
using GMap.NET;
using System.Collections;
using GMap.NET.WindowsForms.Markers;
using System.Threading;
using System.Device.Location;

namespace RouteTrack
{

    public partial class Form1 : Form
    {

        class Plane
        {
            public string FlightNumber;
            private float Latitude;
            private float Longitude;


            public int RequestNumber;
            public float AvgSpeed;
            public float AvgHight;
            public float AVGHightRate;

            public float AvgSpeedDiff;
            public float AvgHightDiff;
            public float AVGHightRateDiff;

            public float[] PlaneAttr;
            public List<PointLatLng> PlaneRoute;
            public List<PointLatLng> WeathePoints;

            public string LogPath;

            public Bitmap PlaneIcon;

            public Bitmap HeavyRain;
            public Bitmap LittleRain;
            public Bitmap Clouds;
            public Bitmap SunClouds;
            public Bitmap Sun;

            public GMapRoute planeRoutePoints;
            public GMarkerGoogle markerPoint;
            public bool is_ground;

            public List<GMarkerGoogle> WeatherMarkers;

            public List<string> WeatherDesc;

            public JsonElement respDataIn;

            public void GetFlightNumber(string FNumber)
            {
                this.FlightNumber = FNumber;
            }

            public void SetCoordinates(float Latit, float Longit)
            {
                this.Latitude = Latit;
                this.Longitude = Longit;
            }

            public float GetCurrentLat()
            {
                return this.Latitude;
            }
            public float GetCurrentLong()
            {
                return this.Longitude;
            }

            public JsonElement FlyApiGet()
            {
                string APIstring = "states/all?icao24=" + this.FlightNumber;

                var client = new RestClient("https://opensky-network.org/api/");
                client.Authenticator = new HttpBasicAuthenticator("", "");
                var request = new RestRequest(APIstring, Method.GET); //DataFormat.Json);
                var resp = client.Execute(request);
                JsonElement respData = new JsonElement();

                if (resp.IsSuccessful)
                {
                    respData = JsonDocument.Parse(resp.Content).RootElement;

                    return respData;
                }
                else
                {
                    return respData;
                }


            }

            public JsonElement WeatherApiGet(PointLatLng WeathePoint)
            {
                string APIstring = "data/2.5/onecall?lat=" + WeathePoint.Lat.ToString();
                APIstring = APIstring + "&lon=" + WeathePoint.Lng.ToString();
                APIstring = APIstring + "&exclude=minutely,daily,alerts&appid=";

                var client = new RestClient("https://api.openweathermap.org/");
                var request = new RestRequest(APIstring, Method.GET); //DataFormat.Json);
                var resp = client.Execute(request);
                JsonElement respData = new JsonElement();

                if (resp.IsSuccessful)
                {
                    respData = JsonDocument.Parse(resp.Content).RootElement;

                    return respData;
                }
                else
                {
                    return respData;
                }


            }

            public Bitmap RotateImage(Bitmap bmp, float angle)
            {
                Bitmap rotatedImage = new Bitmap(bmp.Width, bmp.Height);
                rotatedImage.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);

                using (Graphics g = Graphics.FromImage(rotatedImage))
                {
                    g.TranslateTransform(bmp.Width / 2, bmp.Height/2);
                    g.RotateTransform(angle);
                    g.TranslateTransform(-bmp.Width / 2, -bmp.Height/2);
                    g.DrawImage(bmp, new Point(0, 0));
                }

                return rotatedImage;
            }

            public double Getdistance (GeoCoordinate StartPoint , GeoCoordinate EndPoint)
            {
                return StartPoint.GetDistanceTo(EndPoint);
            }

            /*public double[] NewPointCord(GeoCoordinate startPoint, double distance, double bearing)
            {
                double lat1 = startPoint.Latitude * (Math.PI / 180);
                double lon1 = startPoint.Longitude * (Math.PI / 180);
                double brng = bearing * (Math.PI / 180);
                double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(distance / 6371000) + Math.Cos(lat1) * Math.Sin(distance / 6371000) * Math.Cos(brng));
                double lon2 = lon1 + Math.Atan2(Math.Sin(brng) * Math.Sin(distance / 6371000) * Math.Cos(lat1), Math.Cos(distance / 6371000) - Math.Sin(lat1) * Math.Sin(lat2));

                double[] watherCoords = new double[2];
                watherCoords[0] = lat2;
                watherCoords[1] = lon2;
                return watherCoords;
            }*/

            public double[] NewPointCord(GeoCoordinate startPoint, double distanceKilometres, double initialBearingRadians)
            {
                const double radiusEarthKilometres = 6371.01;
                var distRatio = distanceKilometres / radiusEarthKilometres;
                var distRatioSine = Math.Sin(distRatio);
                var distRatioCosine = Math.Cos(distRatio);

                var startLatRad = Math.PI * startPoint.Latitude / 180.0;
                var startLonRad = Math.PI * startPoint.Longitude / 180.0;
                var Startdirection = Math.PI * initialBearingRadians / 180.0;


                var startLatCos = Math.Cos(startLatRad);
                var startLatSin = Math.Sin(startLatRad);

                var endLatRads = Math.Asin((startLatSin * distRatioCosine) + (startLatCos * distRatioSine * Math.Cos(Startdirection)));

                var endLonRads = startLonRad
                    + Math.Atan2(
                        Math.Sin(Startdirection) * distRatioSine * startLatCos,
                        distRatioCosine - startLatSin * Math.Sin(endLatRads));

                double[] newLoc = new double[2];

                newLoc[0]= 180.0 * endLatRads / Math.PI;
                newLoc[1] = 180.0 * endLonRads / Math.PI;

                return newLoc;
            }

        }

        Plane PlaneObj = new Plane();

        System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
        GMapOverlay polyOverlay = new GMapOverlay("polygons");

        //GMapOverlay markersOverlay = new GMapOverlay("markers");

        //----------------------------------------------------------------------------//
        //-------------------------Inicjlizacja obiektów------------------------------//

        public Form1()
        {
            InitializeComponent();
            MainMap.MapProvider = GMapProviders.GoogleMap;
            
            t.Interval = 15000;
            t.Tick += new EventHandler(this.Button1_Click);


            PlaneObj.LogPath = "LOG\\LogFile.log";
            PlaneObj.PlaneIcon = new Bitmap("PlaneIcon.png");

            PlaneObj.HeavyRain = new Bitmap("heavyrain.png");


            PlaneObj.PlaneAttr = new float[7];
            PlaneObj.PlaneRoute = new List<PointLatLng>();
            PlaneObj.WeathePoints = new List<PointLatLng>();
            PlaneObj.WeatherMarkers = new List<GMarkerGoogle>();
            PlaneObj.WeatherDesc = new List<string>();
            PlaneObj.RequestNumber = 0;
            PlaneObj.AvgSpeed = 0;
            PlaneObj.AvgHight = 0;
            PlaneObj.AVGHightRate = 0;

        }

        //----------------------------------------------------------------------------//
        //----------------------------------------------------------------------------//
        public int PipelineAsync()
        {
            BroadcastBlock<Plane> LogPathBrodcast = new BroadcastBlock<Plane>(null);
            BroadcastBlock<List<PointLatLng>> WeatherPointsBrodcast = new BroadcastBlock<List<PointLatLng>>(null);
            LogPathBrodcast.Post(PlaneObj);

            BroadcastBlock<float[]> PlaneAttrBrodcast = new BroadcastBlock<float[]>(null);
            //Pobranie danych z API
            TransformBlock<Plane, float[]> PosiotionBlock = new TransformBlock<Plane, float[]>(plane =>
            {
                plane.respDataIn = plane.FlyApiGet();

                try
                {   
                    //geo coordinates
                    plane.PlaneAttr[0] = float.Parse(plane.respDataIn.GetProperty("states")[0][6].ToString(), CultureInfo.InvariantCulture.NumberFormat);
                    plane.PlaneAttr[1] = float.Parse(plane.respDataIn.GetProperty("states")[0][5].ToString(), CultureInfo.InvariantCulture.NumberFormat);
                    //direction
                    plane.PlaneAttr[2] = float.Parse(plane.respDataIn.GetProperty("states")[0][10].ToString(), CultureInfo.InvariantCulture.NumberFormat);
                    //velocity
                    plane.PlaneAttr[3] = float.Parse(plane.respDataIn.GetProperty("states")[0][9].ToString(), CultureInfo.InvariantCulture.NumberFormat);
                    //Verticl rate
                    plane.PlaneAttr[4] = float.Parse(plane.respDataIn.GetProperty("states")[0][11].ToString(), CultureInfo.InvariantCulture.NumberFormat);
                    //geo altitude
                    plane.PlaneAttr[5] = float.Parse(plane.respDataIn.GetProperty("states")[0][13].ToString(), CultureInfo.InvariantCulture.NumberFormat);

                    plane.is_ground = bool.Parse(plane.respDataIn.GetProperty("states")[0][8].ToString());
                }
                catch
                {
                    if (plane.PlaneRoute.Count > 1)
                    {
                        plane.PlaneAttr[0] = float.Parse(plane.PlaneRoute[plane.PlaneRoute.Count - 1].Lat.ToString(), CultureInfo.InvariantCulture.NumberFormat);
                        plane.PlaneAttr[1] = float.Parse(plane.PlaneRoute[plane.PlaneRoute.Count - 1].Lng.ToString(), CultureInfo.InvariantCulture.NumberFormat);
                    }
                    else
                    {
                        MessageBox.Show("Error occured while searching plane", "Unecpected Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                //PlaneAttrBrodcast.SendAsync(plane.PlaneAttr);

                return plane.PlaneAttr;

            });

            ActionBlock<float[]> PosiotionBlockLOG = new ActionBlock<float[]>(PlaneAttr =>
            {

                using (StreamWriter outputFile = new StreamWriter(LogPathBrodcast.Receive().LogPath, append: true))
                {
                    outputFile.WriteAsync(PlaneAttr[0].ToString() + ';' + PlaneAttr[1].ToString() + ";" + PlaneAttr[2].ToString() + ";" + PlaneAttr[3].ToString()+";" + PlaneAttr[4].ToString() +  "\n");
                }


            });

            TransformBlock<float[], Plane> ValidateDataBlock = new TransformBlock<float[], Plane>(PlaneAttr =>
            {
                Plane plane = LogPathBrodcast.Receive();
                if ((plane.PlaneRoute.Count>2) && (PlaneAttr.Count()>0))
                {
                    var SPoint = new GeoCoordinate(plane.PlaneRoute[plane.PlaneRoute.Count-1].Lat, plane.PlaneRoute[plane.PlaneRoute.Count - 1].Lng);
                    var EPoint = new GeoCoordinate(PlaneAttr[0], PlaneAttr[1]);

                    double PlaneDist = plane.Getdistance(SPoint, EPoint);

                    if((plane.PlaneAttr[3]*10< PlaneDist) && (PlaneDist< plane.PlaneAttr[3] * 100) )
                    {
                        plane.PlaneRoute.Add(new PointLatLng(plane.PlaneAttr[0], plane.PlaneAttr[1]));
                    }

                    if (plane.PlaneRoute.Count > 100)
                    {
                        plane.PlaneRoute.RemoveAt(0);
                    }
                }
                else if (PlaneAttr.Count() > 0)
                {
                    plane.PlaneRoute.Add(new PointLatLng(PlaneAttr[0], PlaneAttr[1]));
                }
                else if (plane.PlaneRoute.Count > 0)
                {
                    plane.PlaneRoute.Add(plane.PlaneRoute[plane.PlaneRoute.Count-1]);
                }
                
                return plane;

            });

            TransformBlock<float[], float[]> FlightStats = new TransformBlock<float[], float[]>(PlaneAttr =>
            {
                Plane plane = LogPathBrodcast.Receive();
                float[] CurAvg = new float[7];
                if(plane.RequestNumber>0)
                {
                    int LocRequestNumber = plane.RequestNumber+ 1;
                    float LocAvgSpeed = (plane.AvgSpeed + PlaneAttr[3]) / plane.RequestNumber;
                    float LocAVGHightRate = (plane.AVGHightRate + PlaneAttr[4]) / plane.RequestNumber;
                    float LocAvgHight = (plane.AvgHight + PlaneAttr[5]) / plane.RequestNumber;

                    float SpeedDiff = plane.AvgSpeed - LocAvgSpeed;
                    float HightRatediff = plane.AVGHightRate- LocAVGHightRate;
                    float Hightdiff = plane.AvgHight- LocAvgHight;

                    CurAvg[0] = LocRequestNumber;
                    CurAvg[1] = LocAvgSpeed;
                    CurAvg[2] = LocAVGHightRate;
                    CurAvg[3] = LocAvgHight;
                    CurAvg[4] = SpeedDiff;
                    CurAvg[5] = HightRatediff;
                    CurAvg[6] = Hightdiff;

                }
                else
                {
                    CurAvg[0] = 1;
                    CurAvg[1] = PlaneAttr[3];
                    CurAvg[2] = PlaneAttr[4];
                    CurAvg[3] = PlaneAttr[5];
                    CurAvg[4] = 0;
                    CurAvg[5] = 0;
                    CurAvg[6] = 0;
                }


                return CurAvg;
            });


            TransformBlock<float[], List<PointLatLng>> WeatherPositions = new TransformBlock<float[], List<PointLatLng>>(PlaneAttr =>
            {
                Plane plane = LogPathBrodcast.Receive();
                plane.WeathePoints.Clear();

                //40 km 
                double[] WeatherPointLoc25 = plane.NewPointCord(new GeoCoordinate(PlaneAttr[0], PlaneAttr[1]), 25, PlaneAttr[2]);

                //80 km 
                double[] WeatherPointLoc75 = plane.NewPointCord(new GeoCoordinate(PlaneAttr[0], PlaneAttr[1]), 75, PlaneAttr[2]);

                //120 km 
                double[] WeatherPointLoc120 = plane.NewPointCord(new GeoCoordinate(PlaneAttr[0], PlaneAttr[1]), 120, PlaneAttr[2]);



                plane.WeathePoints.Add(new PointLatLng(WeatherPointLoc25[0], WeatherPointLoc25[1]));
                plane.WeathePoints.Add(new PointLatLng(WeatherPointLoc75[0], WeatherPointLoc75[1]));
                plane.WeathePoints.Add(new PointLatLng(WeatherPointLoc120[0], WeatherPointLoc120[1]));

                WeatherPointsBrodcast.Post(plane.WeathePoints);

                return plane.WeathePoints;
            });

            TransformBlock<List<PointLatLng>, List<string>> WeatherAPI = new TransformBlock<List<PointLatLng>, List<string>>(WeatherList =>
            {
                Plane plane = LogPathBrodcast.Receive();
                long unixTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                int secondTo25 = 0;
                int secondTo75 = 0;
                int secondTo120 = 0;
                plane.WeatherDesc.Clear();
                int final_iter = 0;

                for (int i = 0; i< WeatherList.Count;i++)
                {
                    JsonElement WeatherApiVar = PlaneObj.WeatherApiGet(WeatherList[i]);
                    int ok = 0;
                    int j = 0;




                    if (i == 0)
                    {
                        secondTo25 = (int)(25000 / plane.PlaneAttr[3]);
                    }
                    else if (i==1)
                    {
                        secondTo75 = (int)(75000 / plane.PlaneAttr[3]);
                    }
                    else if(i==2)
                    {
                        secondTo120 = (int)(120000 / plane.PlaneAttr[3]);
                    }


                    while (ok <1)
                    {
                        long ApiUnixHour = long.Parse(WeatherApiVar.GetProperty("hourly")[j].GetProperty("dt").ToString());

                        if(i==0)
                        {
                            if((unixTime+ secondTo25)< ApiUnixHour)
                            {
                                ok = 1;
                                final_iter = j;
                            }
                        }
                        else if (i == 1)
                        {
                            if ((unixTime + secondTo75) < ApiUnixHour)
                            {
                                ok = 1;
                                final_iter = j;
                            }
                        }
                        else if (i == 2)
                        {
                            if ((unixTime + secondTo120) < ApiUnixHour)
                            {
                                ok = 1;
                                final_iter = j;
                            }
                        }
                        j = j + 1;
                    }

                    plane.WeatherDesc.Add(WeatherApiVar.GetProperty("hourly")[final_iter].GetProperty("weather")[0].GetProperty("description").ToString());

                }

                return plane.WeatherDesc;
            });

            TransformBlock<Plane, Plane> PlaneMapObject = new TransformBlock<Plane, Plane>(plane =>
            {
                Bitmap FinalIcon = plane.RotateImage(plane.PlaneIcon, plane.PlaneAttr[2]);
                plane.WeatherMarkers.Clear();

                plane.planeRoutePoints = new GMapRoute(plane.PlaneRoute, "RouteLine");
                plane.planeRoutePoints.Stroke = new Pen(Color.Red, 1);

                //GPoint posPoint = gc.FromLatLngToLocal(new PointLatLng(plane.PlaneAttr[0], plane.PlaneAttr[1]));
                //PointLatLng offsetMarker = gc.FromLocalToLatLng((int)posPoint.X, (int)(posPoint.Y + FinalIcon.Size.Height / 2));

                PointLatLng markerPoint = new PointLatLng(plane.PlaneAttr[0], plane.PlaneAttr[1]);

                plane.markerPoint = new GMarkerGoogle(markerPoint, FinalIcon);

                float[] PlaneAvgParams = FlightStats.Receive();
                //List<PointLatLng> WeatherPos = WeatherPositions.Receive();
                List<PointLatLng> WeatherPos = WeatherPointsBrodcast.Receive();
                List<string> WeatherDesc = WeatherAPI.Receive();

                for (int i = 0; i< WeatherPos.Count;i++)
                {
                    plane.WeatherMarkers.Add(new GMarkerGoogle(WeatherPos[i], plane.HeavyRain));
                }

                if (PlaneAvgParams[0]==1)
                {
                    plane.RequestNumber = 1;
                }
                else if (PlaneAvgParams[0] > 1)
                {
                    plane.RequestNumber = (int)(PlaneAvgParams[0]);
                    plane.AvgSpeed = PlaneAvgParams[1];
                    plane.AVGHightRate = PlaneAvgParams[2];
                    plane.AvgHight = PlaneAvgParams[3];

                    plane.AvgSpeedDiff = PlaneAvgParams[4];
                    plane.AVGHightRateDiff = PlaneAvgParams[5];
                    plane.AvgHightDiff = PlaneAvgParams[6];
                }


                return plane;

            });

            PosiotionBlock.LinkTo(PlaneAttrBrodcast, new DataflowLinkOptions() { PropagateCompletion = true });
            PlaneAttrBrodcast.LinkTo(PosiotionBlockLOG, new DataflowLinkOptions() { PropagateCompletion = true });
            PlaneAttrBrodcast.LinkTo(ValidateDataBlock, new DataflowLinkOptions() { PropagateCompletion = true });
            PlaneAttrBrodcast.LinkTo(FlightStats, new DataflowLinkOptions() { PropagateCompletion = true });
            PlaneAttrBrodcast.LinkTo(WeatherPositions, new DataflowLinkOptions() { PropagateCompletion = true });
                
            //PosiotionBlockLOG.LinkTo(ValidateDataBlock);
            ValidateDataBlock.LinkTo(PlaneMapObject);
            WeatherPositions.LinkTo(WeatherAPI);

            PosiotionBlock.SendAsync(PlaneObj);
            PosiotionBlock.Complete();

            PlaneObj = PlaneMapObject.Receive();

            PlaneMapObject.Complete();
            PlaneMapObject.Completion.Wait();


            return 1;
        }




        private void Button1_Click(object sender, EventArgs e)
        {
            UpdateTimer.Start();
            int ok_valid;
            PlaneObj.GetFlightNumber(ICAOText.Text.ToLower());
            t.Start();
            polyOverlay.Clear();

            //Task PipelineTaskAsync = new Task(()=>PipelineAsync());

            try
            {
                Task PipelinTask = Task.Run(() => PipelineAsync());
                ok_valid = 1;
                PipelinTask.Wait();
            }
            catch
            {
                ok_valid = 0;
            }

            if (ok_valid==1 && PlaneObj.is_ground == false)
            {
                CurLat.Text = PlaneObj.PlaneAttr[0].ToString();
                CurLong.Text = PlaneObj.PlaneAttr[1].ToString();
                PlaneObj.SetCoordinates(PlaneObj.PlaneAttr[0], PlaneObj.PlaneAttr[1]);

                //Set offset of plane icon on the map
                MainMap.Position = new GMap.NET.PointLatLng(PlaneObj.GetCurrentLat(), PlaneObj.GetCurrentLong());
                GPoint posPoint = MainMap.FromLatLngToLocal(PlaneObj.markerPoint.Position);
                PointLatLng offsetMarker = MainMap.FromLocalToLatLng((int)posPoint.X, (int)(posPoint.Y + PlaneObj.PlaneIcon.Size.Height / 2));
                PlaneObj.markerPoint = new GMarkerGoogle(offsetMarker, PlaneObj.RotateImage(PlaneObj.PlaneIcon, PlaneObj.PlaneAttr[2]));
                //--------------------------

                polyOverlay.Markers.Add(PlaneObj.markerPoint);

                for(int i =0; i<PlaneObj.WeatherMarkers.Count;i++)
                {
                    polyOverlay.Markers.Add(PlaneObj.WeatherMarkers[i]);
                }

                polyOverlay.Routes.Add(PlaneObj.planeRoutePoints);

                //MainMap.Position = new GMap.NET.PointLatLng(PlaneObj.GetCurrentLat(), PlaneObj.GetCurrentLong());
                MainMap.Overlays.Add(polyOverlay);


                JsonElement testAPI = PlaneObj.WeatherApiGet(PlaneObj.WeathePoints[0]);

                testBOX.Text = testAPI.ToString();
                //testBOX.Text = PlaneObj.WeathePoints[0].ToString();

                MainMap.Zoom = 7;

                VelocityBox.Text = PlaneObj.PlaneAttr[3].ToString();
                VelRate.Text = PlaneObj.PlaneAttr[4].ToString();
                PlaneHeight.Text = PlaneObj.PlaneAttr[5].ToString();

                AvgSpeed.Text = PlaneObj.AvgSpeed.ToString();
                SpeedDiff.Text = PlaneObj.AvgSpeedDiff.ToString();
                AVGHeight.Text = PlaneObj.AvgHight.ToString();
                HeightDiff.Text = PlaneObj.AvgHightDiff.ToString();
                AvgHeightRate.Text = PlaneObj.AVGHightRate.ToString();
                HeightRateDiff.Text = PlaneObj.AVGHightRateDiff.ToString();


            }

            IsGroundCheck.Checked = PlaneObj.is_ground;

            if (PlaneObj.is_ground == true)
            {
                t.Stop();
            }
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            t.Stop();
            UpdateTimer.Stop();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            int timeInt;
            if (TimeLabel.Text == "0:00")
            {
                TimeLabel.Text = "0:14";
            }
            else
            {
                timeInt = (int.Parse(TimeLabel.Text.Replace(":", "")) - 1);
                if(timeInt<10)
                {
                    TimeLabel.Text = "0:0"+timeInt.ToString();
                }
                else
                {
                    TimeLabel.Text = "0:" + timeInt.ToString();
                }
                
            }

        }
    }
}
