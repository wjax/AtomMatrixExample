using NanoComms.Base;
using System;

namespace NanoComms
{
    public delegate void DataReadyEventHandler(string ip, int port, long time, byte[] bytes, int offset, int length, string ID, ushort[] ipChunks);
    public delegate void ConnectionStateDelegate(string ID, ConnUri uri, bool connected);
    public delegate void DataRateDelegate(string ID, float MbpsRX, float MbpsTX);

    public interface ICommunicator : IDisposable
    {
        public event DataRateDelegate DataRateEvent;
        public event ConnectionStateDelegate ConnectionStateEvent;
        public event DataReadyEventHandler DataReadyEvent;

        string ID { get; set; }
        ushort[] IpChunks { get; }
        ConnUri CommsUri {get;}

        void Init(ConnUri uri, bool persistent, string ID, int inactivityMS, int sendGAP = 0);
        void Start();
        void Stop();
        void SendASync(byte[] bytes, int length);
        bool SendSync(byte[] bytes, int offset, int length);
    }
}
