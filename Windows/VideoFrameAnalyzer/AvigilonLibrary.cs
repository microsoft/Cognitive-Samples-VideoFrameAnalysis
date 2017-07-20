using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoFrameAnalyzer
{
    class AvigilonLibrary
    {

        public static bool GetCamerabyLogicalId()
        {
            // Try to get our device, give a few seconds because it may take awhile
            // for the device info to come in from the NVR
            System.DateTime waitEnd = System.DateTime.Now + new System.TimeSpan(0, 0, k_cameraInfoWait);
            AvigilonDotNet.IEntityCamera camera = null;

            while (System.DateTime.Now < waitEnd && camera == null)
            {
                if (m_camSpecifiedLogical)
                {
                    // Try to grab the entity from each device
                    System.Collections.Generic.List<AvigilonDotNet.IDevice> devices = nvr.Devices;
                    foreach (AvigilonDotNet.IDevice device in devices)
                    {
                        camera = (AvigilonDotNet.IEntityCamera)device.GetEntityByLogicalId(m_cameraLogicalId);
                        if (camera != null)
                            break;
                    }
                }
                else
                {
                    camera = (AvigilonDotNet.IEntityCamera)nvr.GetEntityById(m_cameraDeviceId);
                }

                if (camera == null)
                    System.Threading.Thread.Sleep(500);
            }

            // Device isn't a part of the NVR
            if (camera == null)
            {
                Console.WriteLine("Camera is still null, returning back to main");
                return false;
            }
            Console.WriteLine(camera.Name);

            m_streamGroup = m_controlCenter.CreateStreamGroup(AvigilonDotNet.PlaybackMode.Live);

            if (m_streamGroup != null)
            {
                m_controlCenter.CreateStreamCallback(camera, m_streamGroup, AvigilonDotNet.MediaCoding.Jpeg, out m_streamcallback);
            }
            else
            {
                Console.WriteLine("stream group is NULL");
            }

            if (m_streamcallback != null)
            {
                m_streamcallback.Enable = true;
                return true;
            }


            return false;
        }

        public static bool LogintoNVRs()
        {
            System.Net.IPAddress address;
            if (System.Net.IPAddress.TryParse("127.0.0.1", out address))
            {
                m_address = address;
            }
            if (m_address != null)
            {
                m_endPoint = new System.Net.IPEndPoint(m_address, m_controlCenter.DefaultNvrPortNumber);
            }
            m_controlCenter.AddNvr(m_endPoint);

            // Wait for the NVR to appear in the list
            System.DateTime waitEnd = System.DateTime.Now + new System.TimeSpan(0, 0, k_nvrConnectWait);


            while (System.DateTime.Now < waitEnd &&
                nvr == null)
            {
                nvr = m_controlCenter.GetNvr(m_endPoint.Address);

                if (nvr == null)
                    System.Threading.Thread.Sleep(500);
            }

            if (nvr == null)
            {
                Console.WriteLine("Failed is fetching NVR");
                return false;
            }

            // Log in to the NVR
            AvigilonDotNet.LoginResult loginResult = nvr.Login(
                m_userName,
                m_password);
            if (loginResult != AvigilonDotNet.LoginResult.Successful)
            {
                Console.WriteLine("Login successful");
                return false;
            }
            return true;
        }

        public static bool IniializeAvigilonSDK()
        {
            // Create and initialize the control center SDK.
            AvigilonDotNet.SdkInitParams initParams = new AvigilonDotNet.SdkInitParams(
                AvigilonDotNet.AvigilonSdk.MajorVersion,
                AvigilonDotNet.AvigilonSdk.MinorVersion);
            initParams.AutoDiscoverNvrs = false;
            initParams.ServiceMode = false;

            m_sdk = new AvigilonDotNet.AvigilonSdk();
            m_controlCenter = m_sdk.CreateInstance(initParams);

            if (m_controlCenter == null)
            {
                return false;
            }

            System.Console.WriteLine("Avigilon SDK initialized");
            return true;
        }
        public static System.Net.IPAddress m_address;
        public static bool m_camSpecifiedLogical = true;
        public static System.UInt32 m_cameraLogicalId = 1208;
        public static System.String m_cameraDeviceId = "";
        public static System.Net.IPEndPoint m_endPoint;
        public static string m_userName = "administrator";
        public static string m_password = "";
        public static AvigilonDotNet.AvigilonSdk m_sdk = null;
        public static AvigilonDotNet.IAvigilonControlCenter m_controlCenter = null;
        public static AvigilonDotNet.IEntityCamera m_camera = null;
        public static AvigilonDotNet.IStreamWindow m_streamWindow = null;
        public static AvigilonDotNet.IStreamGroup m_streamGroup = null;
        public static AvigilonDotNet.IAlarm m_alarm = null;
        public static AvigilonDotNet.INvr nvr = null;
        public static AvigilonDotNet.IStream m_stream = null;
        public static AvigilonDotNet.IStreamCallback m_streamcallback = null;
        public  const int k_nvrConnectWait = 10; // (seconds)
        public static int k_cameraInfoWait = 10; // (seconds)
        public static AvigilonDotNet.IRequestor m_irequestor;
    }
}
