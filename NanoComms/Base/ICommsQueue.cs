using System;
using System.Text;

namespace NanoComms.Base
{
    interface ICommsQueue
    {
        int Put(byte[] buff, int length);
        int Take(ref byte[] buff, int offset);
        void Reset();
    }
}
