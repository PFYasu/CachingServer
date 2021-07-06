using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Linq;

namespace CachingServer
{
    class Server
    {
        public string Address { get; private set; }
        public int Port { get; private set; }
        public int ConnectedUsers { get; private set; }
        public int ValueBytes
        {
            get
            {
                return bd.ValueBytes;
            }
        }

        private readonly TcpListener listener;
        private readonly ByteData bd;

        public Server(string address, int port, int dataLimiter)
        {
            Address = address;
            Port = port;
            ConnectedUsers = 0;

            listener = new TcpListener(IPAddress.Parse(Address), Port);
            bd = new ByteData(dataLimiter);
        }

        public void Start()
        {
            Console.WriteLine("Server started! Waiting for first connection..");
            listener.Start();

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Thread clientTh = new Thread(new ParameterizedThreadStart(clientManagement));
                clientTh.Start(client);
                Console.WriteLine("New client connected! " + ++ConnectedUsers + " connected users");
            }
        }

        private void clientManagement(Object tcpClient)
        {
            TcpClient client = (TcpClient)tcpClient;
            NetworkStream ns = client.GetStream();

            sendMessageToClient(ns, "Escape character is '^]'\r\n");
            while (true)
            {
                List<byte> receivedBytes;
                try
                {
                    receivedBytes = getMessageFromClient(ns);
                }
                catch(Exception)
                {
                    break;
                }

                var foundCommand = findCommand(receivedBytes);

                if (foundCommand.Item1 != -1 && foundCommand.Item2 == "get")
                {
                    var executeGetInfo = executeGet(receivedBytes, foundCommand.Item1);
                    if (executeGetInfo.Item1 == -1)
                    {
                        sendMessageToClient(ns, "MISSING\r\n");
                    }
                    else
                    {
                        sendMessageToClient(ns, "OK " + executeGetInfo.Item1 + "\r\n");
                        sendMessageToClient(ns, Encoding.UTF8.GetString(executeGetInfo.Item2.ToArray()) + "\r\n");
                    }
                }
                else if (foundCommand.Item1 != -1 && foundCommand.Item2 == "set")
                {
                    List<byte> valueBytes;
                    try
                    {
                        valueBytes = getMessageFromClient(ns);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                    executeSet(receivedBytes, foundCommand.Item1, valueBytes);
                    sendMessageToClient(ns, "OK\r\n");
                }
            }
            Console.WriteLine("Client was disconnected! " +  --ConnectedUsers + " connected users");
            client.Close();
        }

        private void sendMessageToClient(NetworkStream ns, string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            ns.Write(bytes, 0, bytes.Length);
        }

        private List<byte> getMessageFromClient(NetworkStream ns)
        {
            List<byte> bytes = new List<byte>();
            byte[] singleByte = new byte[1];

            while (true)
            {
                ns.Read(singleByte, 0, singleByte.Length);

                if(singleByte[0] == '\b')
                {
                    if(bytes.Count >= 1)
                    {
                        bytes.RemoveAt(bytes.Count - 1);
                    }
                }
                else if(singleByte[0] == 0)
                {
                    throw new Exception("Client disconnected!");
                }
                else
                {
                    bytes.Add(singleByte[0]);
                    if (bytes.Count >= 2 && bytes[bytes.Count - 1] == '\n' && bytes[bytes.Count - 2] == '\r')
                    {
                        bytes.RemoveRange(bytes.Count - 2, 2);
                        return bytes;
                    }
                }
            }
        }

        private (int, List<byte>) executeGet(List<byte> bytes, int position)
        {
            bytes.RemoveRange(0, position);

            int spacePos = getPatternPosition(bytes, Encoding.UTF8.GetBytes(" "));
            List<byte> key = new List<byte>();

            bytes.RemoveRange(0, spacePos + 1);
            for (int i = 0; i < bytes.Count; i++)
            {
                key.Add(bytes[i]);
            }

            try
            {
                List<byte> value = bd.GetValue(key);
                return (value.Count, value);
            }
            catch(Exception)
            {
                return (-1, null);
            }
        }

        private void executeSet(List<byte> bytes, int position, List<byte> value)
        {
            bytes.RemoveRange(0, position);

            int spacePos = getPatternPosition(bytes, Encoding.UTF8.GetBytes(" "));
            bytes.RemoveRange(0, spacePos + 1);

            spacePos = getPatternPosition(bytes, Encoding.UTF8.GetBytes(" "));
            List<byte> key = new List<byte>();
            for(int i = 0; i < spacePos; i++)
            {
                key.Add(bytes[i]);
            }
            bytes.RemoveRange(0, spacePos + 1);

            int length = int.Parse(Encoding.UTF8.GetString(bytes.ToArray()));

            List<byte> val = new List<byte>();
            for(int i = 0; i < value.Count && i < length; i++)
            {
                val.Add(value[i]);
            }

            bd.SetValue(key, val); 
        }

        private int getPatternPosition(List<byte> bytes, byte[] pattern)
        {
            for (int i = 0; i < bytes.Count; i++)
            {
                if (bytes.Skip(i).Take(pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }
            return -1;
        }

        private (int, string) findCommand(List<byte> receivedBytes)
        {
            byte[] setPattern = Encoding.UTF8.GetBytes("set ");
            byte[] getPattern = Encoding.UTF8.GetBytes("get ");

            int setPos = getPatternPosition(receivedBytes, setPattern);
            int getPos = getPatternPosition(receivedBytes, getPattern);

            if(setPos == -1 && getPos >= 0)
                return (getPos, "get");
            if(getPos == -1 && setPos >= 0)
                return (setPos, "set");
            if(getPos != -1 && setPos != -1)
                return getPos > setPos ? (setPos, "set") : (getPos, "get");
            return (-1, "");
        }
    }
}
