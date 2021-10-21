using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

namespace Improved
{
    public enum PType
    {
        UDP,
        TCP
    }

    public enum ProgramStruct
    {
        Server,
        Client
    }

    public enum Message
    {
        Begin = (byte)1,
        End = (byte)0
    }

    public sealed class ImprovedSocket
    {
        private Socket self;
        private ProgramStruct p_struct;
        private bool bind_flag;
        private Thread TcpListenThread;
        private Thread TcpMessageListenThread;

        public delegate void MessageHadnler(byte[] message);
        public event MessageHadnler newMessage;

        public delegate void SocketHandler(ImprovedSocket improvedSocket);
        public event SocketHandler newConnect;


        public ImprovedSocket(PType pt, ProgramStruct pStruct)
        {
            InitValues();

            Protocol_Type = pt;
            p_struct = pStruct;


            if (Protocol_Type == PType.TCP)
            {
                self = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                self = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
        }

        public ImprovedSocket(Socket s, ProgramStruct pt)
        {
            InitValues();

            p_struct = pt;
            switch (s.ProtocolType)
            {
                case ProtocolType.Tcp:
                    Protocol_Type = PType.TCP;
                    break;

                case ProtocolType.Udp:
                    Protocol_Type = PType.UDP;
                    break;

                default:
                    throw new Exception("Данный сокет не поддерживается классом");
            }
            self = s;
        }

        private void InitValues()
        {
            Listen_Count = 10;
        }

        public void Bind(EndPoint endPoint)
        {
            if (p_struct == ProgramStruct.Server)
            {
                End_Point = endPoint;
                self.Bind(End_Point);
                bind_flag = true;
            }
            else throw new Exception("Данный метод не предназначен для Клиента");
        }

        public void Start()
        {
            if (p_struct == ProgramStruct.Client) throw new Exception("Клиент не может быть запущен для прослушивания");

            if (bind_flag)
            {
                if (p_struct == ProgramStruct.Server & Protocol_Type == PType.TCP)
                {
                    self.Listen(Listen_Count);
                    TcpListenThread = new Thread(TcpListen);
                    TcpListenThread.Start();
                }
                else throw new Exception("Нельзя прослушивать подключения для UDP-сокета");
            }
            else throw new Exception("Перед вызовом этого метода нужно вызвать метод Bind");
        }

        private void TcpListen()
        {
            while (true)
            {
                Socket client = self.Accept();
                newConnect?.Invoke(new ImprovedSocket(client, ProgramStruct.Client));
            }
        }

        public void Connect(EndPoint endPoint)
        {
            if (p_struct == ProgramStruct.Client)
            {
                if (Protocol_Type == PType.TCP) self.Connect(endPoint);
                else throw new Exception("Данный метод не предназначен для UDP");
            }
            else new Exception("Данный метод не предназначен для Сервера");
        }

        private byte[] MessageComplete(byte[] message)
        {
            byte[] result = new byte[2 + message.Length];
            int messageLength = message.Length;
            double messageZipLength = messageLength;
            int count = 0;

            while (messageZipLength > 255)
            {
                count += 1;
                messageZipLength /= 255;
            }

            //Первый байт сжатая длина сообщения
            //Второй байт кол-во сжатий длины сообщения
            result[0] = (byte)messageZipLength;
            result[1] = (byte)count;
            return result.Add(message);
        }

        public int Send(byte[] message)
        {
            return self.Send(MessageComplete(message));
        }

        public int Send(string message) => Send(message.GetBytes());

        public void Receive()
        {
            TcpMessageListenThread = new Thread(() =>
            {
                while (true)
                {
                    byte[] data = new byte[1024];
                    self.Receive(data);
                    newMessage?.Invoke(data);
                }
            }
            );
            TcpMessageListenThread.Start();
        }

        public int SendTo(EndPoint endPoint, byte[] message)
        {
            self.SendTo(message, endPoint);
            return 0;
        }

        public int SendTo(EndPoint endPoint, string message) => SendTo(endPoint, message.GetBytes());

        public int ReceiveFrom(EndPoint endPoint)
        {
            byte[] data = new byte[1024];
            self.ReceiveFrom(data, ref endPoint);
            return 0;
        }

        public int Listen_Count { get; set; }
        public PType Protocol_Type { get; set; }
        public ProgramStruct Program_Struct { get { return p_struct; } }
        public EndPoint End_Point { get; set; }
        public Socket Self_Socket { get { return self; } }
    }

    public static class Extensions
    {
        public static byte[] Add(this byte[] a, byte[] b)
        {
            int aLenght = a.Length;
            byte[] result = new byte[aLenght + b.Length];
            for (int i = 0; i < aLenght; i++) result[i] = a[i];
            for (int i = 0; i < b.Length; i++) result[i + aLenght] = b[i];
            return result;
        }

        public static byte[] AddBegin(this byte[] b,byte bb)
        {
            byte[] result = new byte[b.Length + 1];
            result[0] = bb;
            for (int i = 1; i < b.Length; i++) result[i] = bb;
            return result;
        }

        public static byte[] GetBytes(this string s) => UTF8Encoding.UTF8.GetBytes(s);
        public static string GetString(this byte[] b) => UTF8Encoding.UTF8.GetString(b);
    }
}
