using Microsoft.Extensions.Logging;
using NanoComms.Base;
using NanoComms.FrameWrappers;

namespace NanoComms.Communicators
{
    public static class CommunicatorFactory
    {
        public static CommunicatorBase<T> CreateCommunicator<T>(ConnUri uri, FrameWrapperBase<T> frameWrapper = null, ILogger logger = null)
        {
            CommunicatorBase<T> c = null ;
            switch (uri.UriType)
            {
                case ConnUri.TYPE.TCP:
                    c = new TCPNETCommunicator<T>(logger, frameWrapper);
                    break;
                case ConnUri.TYPE.UDP:
                    //c = new UDPNETCommunicator<T>(frameWrapper);
                    break;
            }

            return c;
        }
    }

    public abstract class CommunicatorBase<T> : ICommunicator
    {
        public event DataReadyEventHandler DataReadyEvent;
        public event ConnectionStateDelegate ConnectionStateEvent;
        public event DataRateDelegate DataRateEvent;

        protected readonly ILogger _logger;

        public CommunicatorBase(ILogger logger)
        {
            _logger = logger;
        }


        public enum STATE
        {
            RUNNING,
            STOP
        }
        public STATE State;

        public ConnUri CommsUri { get; protected set; }
        public ushort[] IpChunks { get; protected set; } = new ushort[4];
        public string ID { get; set; }
        public bool WaitForAnswer { get; set; }

        public abstract void Init(ConnUri uri, bool persistent, string ID, int inactivityMS, int sendGAP = 0);
        public abstract void Start();
        public abstract void Stop();
        public abstract void SendASync(byte[] bytes, int length);
        public abstract bool SendSync(byte[] bytes, int offset, int length);
        public abstract void SendSync(T protoBufMessage);
        public abstract void SendASync(T protoBufMessage);
        public abstract FrameWrapperBase<T> FrameWrapper { get; }
        

        public virtual void FireDataEvent(string ip, int port, long time, byte[] bytes, int offset, int length, string ID, ushort[] ipChunks = null)
        {
            DataReadyEvent?.Invoke(ip, port, time, bytes, offset, length, ID, ipChunks);
        }

        public virtual void FireConnectionEvent(string ID, ConnUri uri, bool connected)
        {
            ConnectionStateEvent?.Invoke(ID, uri, connected);
        }

        public virtual void FireDataRateEvent(string ID, float dataRateMbpsRX, float dataRateMbpsTX)
        {
            DataRateEvent?.Invoke(ID, dataRateMbpsRX, dataRateMbpsTX);
        }

        protected virtual void SetIPChunks(string _ip)
        {
            string[] chunks = _ip.Split('.');
            if (chunks.Length == 4)
                for (int i = 0; i < 4; i++)
                    IpChunks[i] = ushort.Parse(chunks[i]);
        }

        public void UnsubscribeEventHandlers()
        {
            if (DataReadyEvent != null)
                foreach (var d in DataReadyEvent.GetInvocationList())
                    DataReadyEvent -= (d as DataReadyEventHandler);

            if (ConnectionStateEvent != null)
                foreach (var d in ConnectionStateEvent.GetInvocationList())
                    ConnectionStateEvent -= (d as ConnectionStateDelegate);
        }

        #region IDisposable Support
        protected bool disposedValue = false; 

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    UnsubscribeEventHandlers();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        
    }
}
