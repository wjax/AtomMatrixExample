using Nano.Base;
using NanoComms.Base;
using System;
using System.Text;

namespace NanoComms.Helper
{
    public class DataRateCalc
    {
        CircularBuffer<int> data;
        CircularBuffer<long> times;

        public DataRateCalc(int size)
        {
            data = new CircularBuffer<int>(size);
            times = new CircularBuffer<long>(size);
        }

        /// <summary>
        /// Add new chunk of bytes and its time of reception
        /// </summary>
        /// <param name="bytes">bytes received/processed</param>
        /// <param name="time">time in microseconds for this chunk</param>
        /// <returns></returns>
        public void Add(int bytes, long time)
        {
            data.Add(bytes);
            times.Add(time);
        }

        public float Average { get {
                int sum = 0;
                for (int i = 0; i< data.Length; i++)
                    sum += data[i];

                long diff = times.Newest - times.Oldest;
                return ((sum * 1000_000f) / diff)/ 1048576;
            } }
    }
}
