using System;

namespace NanoComms.FrameWrappers
{
    public abstract class FrameWrapperBase<T>
    {
        #region logger
        //protected readonly ILogger<FrameWrapperBase<T>> logger = null;
        #endregion

        // Delegate and event
        public delegate void FrameAvailableDelegate(string ID, T payload);
        public event FrameAvailableDelegate FrameAvailableEvent;
        public string ID { get; private set; }


        public FrameWrapperBase()
        {
            //logger = this.GetLogger();               
        }

        public void SetID(string _id)
        {
            ID = _id;
        }

        public abstract void AddBytes(byte[] bytes, int length);

        public abstract void Start();

        public abstract void Stop();

        public void FireEvent(T toFire)
        {
            FrameAvailableEvent?.Invoke(ID, toFire);
        }

        public void UnsubscribeEventHandlers()
        {
            if (FrameAvailableEvent != null)
                foreach (var d in FrameAvailableEvent.GetInvocationList())
                    FrameAvailableEvent -= (d as FrameAvailableDelegate);
        }

        public virtual byte[] Data2BytesSync(T data, out int count)
        {
            throw new NotImplementedException();
        }
    }
}
