//using NanoComms.Base;
//using NanoComms.Communicators;
using nanoFramework.AtomMatrix;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace AtomMatrixExample
{
    public class Program
    {
        public static void Main()
        {
            Debug.WriteLine("Hello from nanoFramework!");

            AtomMatrix.LedMatrix.SetColor(0, Color.Blue);
            AtomMatrix.LedMatrix.Update();

            //var connUri = new ConnUri("tcp://192.168.1.128:5800");
            //var comm = CommunicatorFactory.CreateCommunicator<object>(connUri);
            //comm.Init(connUri, true, "TEST", 5000);
            //comm.ConnectionStateEvent += OnConnection;
            //comm.Start();

            Thread.Sleep(Timeout.Infinite);
        }

        //private static void OnConnection(string ID, ConnUri uri, bool connected)
        //{
        //    Debug.WriteLine($"Connected {ID} - {connected}");

        //    if (connected)
        //        AtomMatrix.LedMatrix.SetColor(2, Color.Green);
        //    else
        //        AtomMatrix.LedMatrix.SetColor(2, Color.Red);

        //    AtomMatrix.LedMatrix.Update();
        //}
    }
}
