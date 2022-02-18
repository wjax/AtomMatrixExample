using NanoComms.Base;
using NanoComms.Communicators;
using System;
using System.Diagnostics;
using System.Threading;

namespace TinyPicoExample
{
    public class Program
    {
        public static void Main()
        {
            Debug.WriteLine("Hello from nanoFramework for TinyPico!");

            var connUri = new ConnUri("tcp://192.168.1.128:5800");
            var comm = CommunicatorFactory.CreateCommunicator<object>(connUri);
            comm.Init(connUri, true, "TEST", 5000);
            comm.ConnectionStateEvent += OnConnection;
            comm.Start();

            Thread.Sleep(Timeout.Infinite);

            // Browse our samples repository: https://github.com/nanoframework/samples
            // Check our documentation online: https://docs.nanoframework.net/
            // Join our lively Discord community: https://discord.gg/gCyBu8T
        }

        private static void OnConnection(string ID, ConnUri uri, bool connected)
        {
            Debug.WriteLine($"Connected {ID} - {connected}");
        }
    }
}
