using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// TcpSocketFactoryWithTimeout is used to open a TcpClient connection, with a 
/// user definable connection timeout in milliseconds (1000=1second)
/// Use it like this:
/// TcpClient connection = new TcpClientWithTimeout('127.0.0.1',80,1000).Connect();
/// </summary>
internal class TcpSocketFactoryWithTimeout
{
    IPEndPoint _endPoint;
    int _timeout_milliseconds;
    Socket _connection;
    bool _connected;
    Exception _exception;

    internal TcpSocketFactoryWithTimeout(IPEndPoint endPoint, int timeout_milliseconds)
    {
        _endPoint = endPoint;
        _timeout_milliseconds = timeout_milliseconds;
    }
    internal Socket Connect()
    {
        // kick off the thread that tries to connect
        _connected = false;
        _exception = null;
        Thread thread = new Thread(BeginConnect);
        //thread.IsBackground = true; // So that a failed connection attempt 
                                    // wont prevent the process from terminating while it does the long timeout
        thread.Start();

        // wait for either the timeout or the thread to finish
        thread.Join(_timeout_milliseconds);

        if (_connected == true)
        {
            // it succeeded, so return the connection
            thread.Abort();
            return _connection;
        }
        if (_exception != null)
        {
            // it crashed, so return the exception to the caller
            thread.Abort();
            throw _exception;
        }
        else
        {
            // if it gets here, it timed out, so abort the thread and throw an exception
            thread.Abort();
            throw new TimeoutException("TcpClient connection to timed out");
        }
    }
    private void BeginConnect()
    {
        try
        {
            _connection = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _connection.Connect(_endPoint);
            // record that it succeeded, for the main thread to return to the caller
            _connected = true;
        }
        catch (Exception ex)
        {
            // record the exception for the main thread to re-throw back to the calling code
            _exception = ex;
        }
    }
}