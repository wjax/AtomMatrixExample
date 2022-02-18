using Microsoft.Extensions.Logging;
using NanoComms.Base;
using NanoComms.FrameWrappers;
using NanoComms.Helper;
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NanoComms.Communicators
{
    public class TCPNETCommunicator<T> : CommunicatorBase<T>
    {
        #region global defines
        private int RECEIVE_TIMEOUT = 4000;
        private const int CONNECTION_TIMEOUT = 5000;
        private const int SEND_TIMEOUT = 100; // Needed on linux as socket will not throw exception when send buffer full, instead blocks "forever"
        private int MINIMUM_SEND_GAP = 0;
        #endregion

        #region fields
        private long LastTX = 0;

        private ICommsQueue messageQueu;

        private Thread senderTask;
        private Thread receiverTask;

        private bool exit = false;
        private bool tcpClientProvided = false;

        private CommEquipmentObject<Socket> tcpEq;
        private FrameWrapperBase<T> frameWrapper;

        private byte[] rxBuffer = new byte[1024];
        private byte[] txBuffer = new byte[1024];

        private Timer dataRateTimer;
        private int bytesAccumulatorRX = 0;
        private int bytesAccumulatorTX = 0;

        private object lockSerializer = new object();
        private ManualResetEvent sendAllowEvent;
        #endregion

        public TCPNETCommunicator(ILogger iloger = null, FrameWrapperBase<T> _frameWrapper = null) : base(iloger)
        {
            frameWrapper = _frameWrapper;
            tcpClientProvided = false;
            WaitForAnswer = false;

            if (frameWrapper is not null)
                frameWrapper.FrameAvailableEvent += FrameWrapper_FrameAvailableEvent;
        }

        public TCPNETCommunicator(Socket client, ILogger iloger = null, FrameWrapperBase<T> _frameWrapper = null) : base(iloger)
        {
            frameWrapper = _frameWrapper;
            // Do stuff
            tcpClientProvided = true;
            var IP = (client.RemoteEndPoint as IPEndPoint).Address.ToString();
            var Port = (client.RemoteEndPoint as IPEndPoint).Port;

            CommsUri = new ConnUri($"tcp://{IP}:{Port}");
            tcpEq = new CommEquipmentObject<Socket>("", CommsUri, client, false);

            WaitForAnswer = false;

            if (frameWrapper is not null)
                frameWrapper.FrameAvailableEvent += FrameWrapper_FrameAvailableEvent;
        }

        private void FrameWrapper_FrameAvailableEvent(string ID, T payload)
        {
            if (WaitForAnswer)
                sendAllowEvent.Set();
        }
        #region CommunicatorBase

        public override void Init(ConnUri uri, bool persistent, string ID, int inactivityMS, int _sendGap = 0)
        {
            if ((uri == null || !uri.IsValid) && !tcpClientProvided)
                return;

            this.ID = ID;
            messageQueu = new CircularByteBuffer4Comms(1024);
            MINIMUM_SEND_GAP = _sendGap;
            RECEIVE_TIMEOUT = inactivityMS;
            frameWrapper?.SetID(ID);
            State = STATE.STOP;

            CommsUri = uri ?? CommsUri;
            SetIPChunks(CommsUri.IP);

            if (!tcpClientProvided)
            {
                tcpEq = new CommEquipmentObject<Socket>(ID, uri, null, persistent);
                tcpEq.ID = ID;
            }
            else
            {
                tcpEq.ID = ID;
            }
        }

        public override void SendASync(byte[] serializedObject, int length)
        {
            if (State == STATE.RUNNING)
                messageQueu.Put(serializedObject, length);
        }

        /// <summary>
        /// Serialize and Send a message. Use only with CircularBuffer
        /// </summary>
        /// <param name="protoBufMessage"></param>
        public override void SendASync(T protoBufMessage)
        {
            lock (lockSerializer)
            {
                byte[] buff = frameWrapper.Data2BytesSync(protoBufMessage, out int count);
                SendASync(buff, count);
            }
        }

        public override bool SendSync(byte[] bytes, int offset, int length)
        {
            if (State != STATE.RUNNING)
                return false;
            else
                return Send2Equipment(bytes, offset, length, tcpEq);
        }

        public override void SendSync(T Message)
        {
            if (State != STATE.RUNNING)
                return;

            lock (lockSerializer)
            {
                byte[] buff = frameWrapper.Data2BytesSync(Message, out int count);
                if (count > 0)
                    SendSync(buff, 0, count);
            }
        }

        public override void Start()
        {
            if (State == STATE.RUNNING)
                return;

            _logger?.LogInformation("Start");
            exit = false;

            receiverTask = tcpClientProvided ? new Thread(ReceiveCallback) : new Thread(Connect2EquipmentCallback);
            senderTask = new Thread(DoSendStart);

            senderTask.Start();
            receiverTask.Start();

            dataRateTimer = new Timer(OnDataRate, null, 1000, 1000);

            State = STATE.RUNNING;
        }

        public override void Stop()
        {
            _logger?.LogInformation("Stop");
            exit = true;

            dataRateTimer.Dispose();

            messageQueu.Reset();
            tcpEq.ClientImpl?.Close();

            senderTask.Join();
            receiverTask.Join();

            State = STATE.STOP;
        }

        

        public override FrameWrapperBase<T> FrameWrapper { get => frameWrapper; }
        #endregion

        private void ClientDown()
        {
            if (tcpEq == null)
                return;

            if (WaitForAnswer)
            {
                sendAllowEvent = null;
            }

            _logger?.LogInformation("ClientDown - " + tcpEq.ID);

            bytesAccumulatorRX = 0;
            bytesAccumulatorTX = 0;

            try
            {
                tcpEq.ClientImpl?.Close();
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "ClientDown Exception");
            }
            finally
            {
                tcpEq.ClientImpl = null;
            }

            // Launch Event
            FireConnectionEvent(tcpEq.ID, tcpEq.ConnUri, false);
        }

        private void ClientUp(Socket o)
        {
            tcpEq.ClientImpl = o;

            bytesAccumulatorRX = 0;
            bytesAccumulatorTX = 0;

            if (WaitForAnswer)
            {
                sendAllowEvent = new ManualResetEvent(false);
                sendAllowEvent.Set();
            }


            // Launch Event
            FireConnectionEvent(tcpEq.ID, tcpEq.ConnUri, true);
        }

       
        private void DoSendStart()
        {
            long toWait = 0;
            LastTX = TimeTools.GetCoarseMillisNow();

            while (!exit)
            {
                try
                {
                    int read = messageQueu.Take(ref txBuffer, 0);

                    if (WaitForAnswer)
                    {
                        sendAllowEvent.WaitOne();
                        Thread.Sleep(MINIMUM_SEND_GAP);
                    }
                    else
                    {
                        long now = TimeTools.GetCoarseMillisNow();
                        if (now - LastTX < MINIMUM_SEND_GAP)
                        {
                            toWait = MINIMUM_SEND_GAP - (now - LastTX);
                            Thread.Sleep((int)toWait);
                        }
                    }

                    var sentOK = Send2Equipment(txBuffer, 0, read, tcpEq);
                    
                    if (WaitForAnswer && sentOK)
                        sendAllowEvent.Reset();

                    LastTX = TimeTools.GetCoarseMillisNow();
                }
                catch (Exception e)
                {
                    _logger?.LogWarning(e, "Exception in messageQueue");
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool Send2Equipment(byte[] data, int offset, int length, CommEquipmentObject<Socket> o)
        {
            if (o == null || o.ClientImpl == null)
                return false;

            string ID = o.ID;
            Socket t = o.ClientImpl;

            try
            {
                int nBytes = t.Send(data, offset, length, SocketFlags.None);

                bytesAccumulatorTX += nBytes;
                LastTX = TimeTools.GetCoarseMillisNow();
            }
            catch (Exception e)
            {
                _logger?.LogError(e, "Error while sending TCPNet");
                // Client Down
                ClientDown();

                return false;
            }

            return true;
        }

        private void Connect2EquipmentCallback()
        {
            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(tcpEq.ConnUri.IP), tcpEq.ConnUri.Port);
            var tcpFactory = new TcpSocketFactoryWithTimeout(ipep, CONNECTION_TIMEOUT);
            do
            {
                _logger?.LogInformation("Waiting for new connection");

                // Blocks here for timeout
                using (Socket t = tcpFactory.Connect())
                {
                    if (t != null)
                    {
                        t.SendTimeout = SEND_TIMEOUT;
                        t.ReceiveTimeout = RECEIVE_TIMEOUT;

                        // Launch event and Add to Dictionary of valid connections
                        ClientUp(t);
                        int rx;

                        try
                        {
                            while ((rx = tcpEq.ClientImpl.Receive(rxBuffer)) > 0)
                            {
                                // Update Accumulator
                                bytesAccumulatorRX += rx;
                                // Update RX Time
                                tcpEq.timeLastIncoming = TimeTools.GetCoarseMillisNow();

                                // RAW Data Event
                                FireDataEvent(CommsUri.IP,
                                                CommsUri.Port,
                                                TimeTools.GetLocalMicrosTime(),
                                                rxBuffer,
                                                0,
                                                rx,
                                                tcpEq.ID,
                                                IpChunks);

                                // Feed to FrameWrapper
                                frameWrapper?.AddBytes(rxBuffer, rx);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError(e, "Error while receiving TCPNet");
                        }
                        finally
                        {
                            ClientDown();
                        }
                    }
                }

            } while (!exit && tcpEq.IsPersistent);
        }

        private void ReceiveCallback()
        {
            if (tcpEq.ClientImpl != null)
            {
                _logger?.LogInformation("Receiving");

                tcpEq.ClientImpl.SendTimeout = SEND_TIMEOUT;
                tcpEq.ClientImpl.ReceiveTimeout = RECEIVE_TIMEOUT;

                // Launch event and Add to Dictionary of valid connections
                ClientUp(tcpEq.ClientImpl);
                int rx;

                try
                {
                    while ((rx = tcpEq.ClientImpl.Receive(rxBuffer)) > 0 && !exit)
                    {
                        // Update Accumulator
                        bytesAccumulatorRX += rx;
                        // Update RX Time
                        tcpEq.timeLastIncoming = TimeTools.GetCoarseMillisNow();

                        // RAW Data Event
                        FireDataEvent(CommsUri.IP,
                                        CommsUri.Port,
                                        TimeTools.GetLocalMicrosTime(),
                                        rxBuffer,
                                        0,
                                        rx,
                                        tcpEq.ID,
                                        IpChunks);

                        // Feed to FrameWrapper
                        frameWrapper?.AddBytes(rxBuffer, rx);
                    }
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Error while receiving TCPNet");
                }
                finally
                {
                    ClientDown();
                }
            }
        }

        private void OnDataRate(object state)
        {
            float dataRateMpbsRX = (bytesAccumulatorRX * 8f) / 1048576; // Mpbs
            float dataRateMpbsTX = (bytesAccumulatorTX * 8f) / 1048576; // Mpbs

            bytesAccumulatorRX = 0;
            bytesAccumulatorTX = 0;

            FireDataRateEvent(ID, dataRateMpbsRX, dataRateMpbsTX);
        }


        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Stop();

                    (messageQueu as IDisposable).Dispose();
                    dataRateTimer?.Dispose();

                    if (frameWrapper is not null)
                        frameWrapper.FrameAvailableEvent -= FrameWrapper_FrameAvailableEvent;
                }

                messageQueu = null;
                tcpEq.ClientImpl = null;
                dataRateTimer = null;

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        
    }

}
