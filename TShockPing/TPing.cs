using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace TShockPing
{
    class TPing
    {
        byte[] m_dataBuffer = new byte[10];
        IAsyncResult m_result;
        public AsyncCallback m_pfnCallBack;
        public Socket m_clientSocket;
        public static bool stopThread;
        public static int returnCode = -1;

        public static string serverIP;
        public static int serverPort;
        public static int sleepTime = 2 * 1000;
        public static int waitTime = 2 * 1000;
        public static int retryTimes = 2;
        public static bool ping = false;
        public static bool debug = false;
        public static bool verbose = false;

        enum ExitCode : int
        {
            Success = 0,
            Connected = 1,
            NotConnected = 2,
            NoPing = 3,
            BadArgs = 4,
            UnknownError = 10
        }

        [STAThread]
        public static void Main(string[] args)
        {
            serverIP = "";
            serverPort = 0;
            retryTimes = 1;
            sleepTime = 2;
            bool badArgs = false;
            if (args.Length == 0)
            {
                Console.WriteLine("No options given.");
                badArgs = true;
            }
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("-v"))
                    verbose = true;

                if (args[i].Equals("-ip"))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("No IP given.");
                        badArgs = true;
                        break;
                    }
                    serverIP = args[++i];
                }
                if (args[i].Equals("-port"))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("No port given.");
                        badArgs = true;
                        break;
                    }
                    if (!Int32.TryParse(args[++i], out serverPort))
                    {
                        Console.WriteLine("port value not integer.");
                        badArgs = true;
                        break;
                    }
                }
                if (args[i].Equals("-retry"))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("No retry value given.");
                        badArgs = true;
                        break;
                    }
                    if (!Int32.TryParse(args[++i], out retryTimes))
                    {
                        Console.WriteLine("retry value not integer.");
                        badArgs = true;
                        break;
                    }
                }
                if (args[i].Equals("-sleep"))
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.WriteLine("No sleep value given.");
                        badArgs = true;
                        break;
                    }
                    if (!Int32.TryParse(args[++i], out sleepTime))
                    {
                        Console.WriteLine("sleep value not integer.");
                        badArgs = true;
                        break;
                    }
                }
                if (args[i].Equals("-d"))
                {
                    debug = true;
                    Console.WriteLine("Debug on.");
                }
            }
            if (serverIP.Length == 0)
            {
                Console.WriteLine("No IP address given.");
                badArgs = true;
            }

            if (serverPort == 0)
            {
                Console.WriteLine("No port address given.");
                badArgs = true;
            }

            if (badArgs)
                Environment.Exit((int)ExitCode.BadArgs);
            else
            {
                if (verbose)
                {
                    Console.WriteLine("IP " + serverIP + ":" + serverPort);
                    Console.WriteLine("Return values");
                    Console.WriteLine("  Connected = 1");
                    Console.WriteLine("  NotConnected = 2");
                    Console.WriteLine("  NoPing = 3");
                    Console.WriteLine("  BadArgs = 4");
                }

                sleepTime = sleepTime * 1000;
                new Thread(() =>
                  {
                      Thread.CurrentThread.IsBackground = false;
                      /* run your code here */
                      new TPing();
                  }).Start();
                    
             }
        }

        public TPing()
        {
            if (Connect())
            {
                stopThread = false;

                for (int retry = 0; retry < retryTimes; retry++)
                {
                    SendMessage("");
                    if (stopThread)
                        break;
                    Thread.Sleep(waitTime);
                    if (stopThread)
                        break;
                    Thread.Sleep(sleepTime);
                }
                CloseConnection();
                DisconnectConnection();

                if (ping)
                    returnCode = (int)ExitCode.Connected;
                else
                   returnCode = (int)ExitCode.NoPing;

                Environment.Exit(returnCode);
            }
            else
            {
                returnCode = (int)ExitCode.NotConnected;
            Environment.Exit(returnCode);
            }
        }

        bool Connect()
        {
            try
            {
                // Create the socket instance
                m_clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Cet the remote IP address
                IPAddress ip = IPAddress.Parse(serverIP);
                int iPortNo = serverPort;
                // Create the end point 
                IPEndPoint ipEnd = new IPEndPoint(ip, iPortNo);
                // Connect to the remote host
                m_clientSocket.Connect(ipEnd);
                if (m_clientSocket.Connected)
                {
                    //Wait for data asynchronously 
                    WaitForData();
                }
                else
                {
                    Console.WriteLine("not connected.");
                }
            }
            catch (SocketException)
            {
                return false;
            }
            return true;
        }
        void SendMessage(string msg)
        {
            try
            {
                char s = (char)0x0B;
                byte[] message = System.Text.Encoding.ASCII.GetBytes("Terraria102");
                byte[] header = BitConverter.GetBytes(message.Length);
                byte[] connectRequest = { 0x10, 0x00, 0x01, 0x0b };
                //               if (BitConverter.IsLittleEndian)
                //                    Array.Reverse(header);
                // New code to send strings

                byte[] block = new byte[connectRequest.Length + message.Length];
                //              System.Buffer.BlockCopy(header, 0, block, 0, header.Length);
                System.Buffer.BlockCopy(connectRequest, 0, block, 0, connectRequest.Length);
                System.Buffer.BlockCopy(message, 0, block, connectRequest.Length, message.Length);

                NetworkStream networkStream = new NetworkStream(m_clientSocket);
                networkStream.Write(block, 0, block.Length);

                /* Use the following code to send bytes
                byte[] byData = System.Text.Encoding.ASCII.GetBytes(objData.ToString ());
                if(m_clientSocket != null){
                    m_clientSocket.Send (byData);
                }
                */
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
            }
        }
        public void WaitForData()
        {
            try
            {
                if (m_pfnCallBack == null)
                {
                    m_pfnCallBack = new AsyncCallback(OnDataReceived);
                }
                SocketPacket theSocPkt = new SocketPacket();
                theSocPkt.thisSocket = m_clientSocket;
                // Start listening to the data asynchronously
                m_result = m_clientSocket.BeginReceive(theSocPkt.dataBuffer, 0, theSocPkt.dataBuffer.Length, SocketFlags.None, m_pfnCallBack, theSocPkt);
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
            }

        }
        public class SocketPacket
        {
            public System.Net.Sockets.Socket thisSocket;
            public byte[] dataBuffer = new byte[1024];
        }

        public void OnDataReceived(IAsyncResult asyn)
        {
            try
            {
                SocketPacket theSockId = (SocketPacket)asyn.AsyncState;
                int iRx = theSockId.thisSocket.EndReceive(asyn);
                char[] chars = new char[iRx];
                System.Text.Decoder d = System.Text.Encoding.UTF8.GetDecoder();
                int charLen = d.GetChars(theSockId.dataBuffer, 0, iRx, chars, 0);
                System.String szData = new System.String(chars);
                var hexString = BitConverter.ToString(theSockId.dataBuffer);
                byte[] connectResponse = { 0x4, 0x00, 0x03, 0x03 };
                if (debug)
                    Console.WriteLine(hexString.Substring(0, 10));
                bool equal = true;
                for (int i = 0; i < 3; i++)
                {
                    if (theSockId.dataBuffer[0] != connectResponse[0])
                    {
                        equal = false;
                        break;
                    }
                }
                if (equal)
                {
                    stopThread = true;
                    ping = true;
                }
                else
                    WaitForData();
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                Console.WriteLine(se.Message);
            }
        }
 
        void CloseConnection()
        {
            if (m_clientSocket != null)
            {
                m_clientSocket.Close();
                m_clientSocket = null;
            }
        }

        void DisconnectConnection()
        {
            if (m_clientSocket != null)
            {
                m_clientSocket.Close();
                m_clientSocket = null;
            }
        }
    }
}
