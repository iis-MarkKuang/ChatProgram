using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Timers;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.IO;
using System.Data;
using System.Data.OleDb;

namespace ChatProgram
{
    public partial class ConnectUI : Form
    {
        private Comm comm;
        private static byte[] result = new byte[1024];
        private delegate void SetTextCallback(string text, bool receive);
        //TODO read from config
        private static int port, remotePort;
        private static IPAddress ip, remoteIp;
        private static Socket servSocket, clieSocket;
        private delegate void ReceiveMessageEventHandler(Object sender, string s);
        private static OleDbConnection conn;
        //定义秒针，分针，时针的长度
        private const int s_pinlen = 60;
        private const int m_pinlen = 45;
        private const int h_pinlen = 30;
        private static string tableName;
        private static string logFileName;
        private static string method;
        private static Thread serverThread;
        private static Thread clientThread;

        public ConnectUI()
        {
            InitializeComponent();
            //ReceiveMessageEvent += new ReceiveMessageEventHandler(this.UpdateMessageBoard);
            XmlDocument xmlDoc = new XmlDocument();
            string parentPath = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;

            xmlDoc.Load(parentPath + "//" + "Config.xml");
            XmlElement elementConnection = (XmlElement)xmlDoc.SelectSingleNode("configuration/connection");

            method = elementConnection.GetAttribute("method");
            
            //根据xml文件的配置，设置数据库
            XmlElement elementDatabase = (XmlElement)xmlDoc.SelectSingleNode("configuration/database");
            SetUpDatabase(elementDatabase);

            //根据xml文件的配置，设置日志文件
            XmlElement elementLog = (XmlElement)xmlDoc.SelectSingleNode("configuration/log");
            logFileName = elementLog.GetAttribute("path");
            
            //根据xml文件的配置决定用哪种通信方式
            if (method.ToLower() == "socket")
            {
                SetUpSocket(elementConnection);
            }
            else if (method.ToLower() == "serialport")
            {
                SetUpSerialPort(elementConnection);
            }
        }

        //设置数据库连接
        private void SetUpDatabase(XmlElement element)
        {
            string connStr = "Provider=Microsoft.ACE.OLEDB.12.0 ;Data Source=" + element.GetAttribute("dataSource");
            conn = new OleDbConnection(connStr);
            tableName = element.GetAttribute("tableName");
            conn.Open();
        }

        //设置套接字
        private void SetUpSocket(XmlElement ele)
        {
            string ipStr = ele.GetAttribute("localIp");
            string remoteIpStr = ele.GetAttribute("remoteIp");

            ip = IPAddress.Parse(ipStr);
            remoteIp = IPAddress.Parse(remoteIpStr);
            port = Convert.ToInt32(ele.GetAttribute("localPort"));
            remotePort = Convert.ToInt32(ele.GetAttribute("remotePort"));
            servSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SetUpSocketServer();

            clientThread = new Thread(MaintainClientConnect);
            clientThread.IsBackground = true;
            clientThread.Start();
        }

        private void SetUpSerialPort(XmlElement ele)
        {
            comm = new Comm();
            comm.serialPort.PortName = ele.GetAttribute("portName").ToUpper();
            //波特率
            comm.serialPort.BaudRate = Convert.ToInt32(ele.GetAttribute("baudRate"));
            //数据位
            comm.serialPort.DataBits = Convert.ToInt32(ele.GetAttribute("dataBits"));
            //两个停止位
            comm.serialPort.StopBits = System.IO.Ports.StopBits.One;
            //无奇偶校验位
            comm.serialPort.Parity = System.IO.Ports.Parity.None;
            comm.serialPort.ReadTimeout = Convert.ToInt32(ele.GetAttribute("readTimeout"));
            comm.serialPort.WriteTimeout = Convert.ToInt32(ele.GetAttribute("writeTimeout"));

            comm.Open();

            if (comm.IsOpen)
            {
                // Comm类用线程去轮询Read串口，每0.1秒读一次。
                comm.DataReceived += new Comm.EventHandle(comm_DataReceived);
            }
        }

        private void ConnectUI_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            string textToBeSent = messageBox.Text;
            messageBox.Text = "";
            if (method.ToLower() == "socket")
            {
                ClientSendMessage(textToBeSent);
            }
            else if (method.ToLower() == "serialport")
            {
                PortSendMessage(textToBeSent);
            }
        }

        //判断是否还连接，因为Socket.connected有一个known bug
        public static bool IsSocketConnected(Socket s)
        {
            #region remarks
            /* As zendar wrote, it is nice to use the Socket.Poll and Socket.Available, but you need to take into consideration 
             * that the socket might not have been initialized in the first place. 
             * This is the last (I believe) piece of information and it is supplied by the Socket.Connected property. 
             * The revised version of the method would looks something like this: 
             * from：http://stackoverflow.com/questions/2661764/how-to-check-if-a-socket-is-connected-disconnected-in-c */
            #endregion

            #region 过程

            if (s == null)
                return false;
            return !((s.Poll(1000, SelectMode.SelectRead) && (s.Available == 0)) || !s.Connected);

            /* The long, but simpler-to-understand version:

                    bool part1 = s.Poll(1000, SelectMode.SelectRead);
                    bool part2 = (s.Available == 0);
                    if ((part1 && part2 ) || !s.Connected)
                        return false;
                    else
                        return true;

            */
            #endregion
        }

        private void MaintainClientConnect()
        {
            while (true)
            {
                try
                {
                    if (clieSocket == null)
                    {
                        clieSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        clieSocket.Connect(new IPEndPoint(remoteIp, remotePort));
                        WriteLogFile("Initialize client socket and make first connection to server " + DateTime.Now.ToString() + "\r\n");
                    }
                    else if (!clieSocket.Connected && !IsSocketConnected(clieSocket))
                    {
                        //注释这两行是因为 只需释放clieSocket，就不会重连时被要求BeginConnect并到另一端口
                        //在connected为false的时候再去shutdown和disconnect，会报错“提供了一个无效的参数”
                        //clieSocket.Shutdown(SocketShutdown.Both);
                        //clieSocket.Disconnect(true);
                        
                        //重连的时候不允许在界面上进行操作
                        lock(this)
                        {
                            clieSocket.Close();
                            clieSocket = null;
                            clieSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                            clieSocket.Connect(new IPEndPoint(remoteIp, remotePort));
                            WriteLogFile("Reconnected to server success " + DateTime.Now.ToString() + "\r\n");
                        }   
                    }
                    else
                    {
                        WriteLogFile("Heartbeat detected the connection is live " + DateTime.Now.ToString() + "\r\n");
                    }
                     
                    //5秒重连一次
                    Thread.Sleep(5000);
                }
                catch (Exception e)
                {
                    WriteLogFile(e.Message);
                }
            }
            
        }

        private void ClientSendMessage(string text)
        {
            try
            {
                clieSocket.Send(Encoding.UTF8.GetBytes(text));
                WriteLogFile(String.Format("Send to server message: {0}", text));
                UpdateMessageBoard(text, false);
                UpdateAccess(text, 1, 1);
            }
            catch (Exception e)
            {
                clieSocket.Shutdown(SocketShutdown.Both);
                WriteLogFile(e.Message);
            }
        }

        private void SetUpSocketServer()
        {
            servSocket.Bind(new IPEndPoint(ip, port));
            WriteLogFile(String.Format("Listen {0} successful", servSocket.LocalEndPoint.ToString()));
            serverThread = new Thread(new ThreadStart(this.ListenClientConnect));
            serverThread.IsBackground = true;
            serverThread.Start();
        }

        private void ListenClientConnect()
        {
            while (true)
            {
                try
                {
                    if (servSocket.IsBound)
                    {
                        servSocket.Listen(10);
                        servSocket.ReceiveTimeout = -1;
                        Socket clientSocket = servSocket.Accept();
                        clientSocket.Send(Encoding.UTF8.GetBytes("Hello from server"));

                        //另外两种构思 thread中套thread保证非阻塞，事实证明不必要
                        //Thread receiveThread = new Thread(this.ReceiveMessage);
                        //receiveThread.Start(clientSocket);
                        //ReceiveMessage(clientSocket);

                        //new Thread(() =>
                        //    {
                        //        UpdateMessageBoard<Socket> u = new UpdateMessageBoard<Socket>(ReceiveMessage);
                        //        Invoke(u, clientSocket);
                        //    }).Start();
                        ReceiveMessage(clientSocket);
                    }
                }
                catch (Exception e)
                {
                    WriteLogFile(e.Message);
                }
                
            }
        }

        
        private void ReceiveMessage(object clientSocket)
        {
            Socket myClientSocket = (Socket)clientSocket;

            while (true)
            {
                try
                {
                    myClientSocket.ReceiveTimeout = 5000;
                    int receiveNumber = myClientSocket.Receive(result);
                    string resultStr = Encoding.UTF8.GetString(result, 0, receiveNumber);
                    WriteLogFile(String.Format("Got {0} messages from address {1}", resultStr, myClientSocket.RemoteEndPoint.ToString()));
                    UpdateMessageBoard(resultStr, true);
                    UpdateAccess(resultStr, 2, 1);
                    Application.DoEvents();
                } catch (Exception ex) {
                    WriteLogFile(ex.Message);
                    myClientSocket.Close();
                    Thread.Sleep(5000);
                    break;
                }
            }
        }

        private void PortSendMessage(string message)
        {
            try
            {
                byte[] content = Encoding.UTF8.GetBytes(message);
                if (comm.IsOpen)
                {
                    comm.WritePort(content, 0, content.Length);
                    WriteLogFile(String.Format("Sent {0} messages to other serial port", message));
                    UpdateAccess(message, 1, 2);
                    UpdateMessageBoard(message, false);
                }
            }
            catch (Exception e)
            {
                WriteLogFile(e.Message);
            }
        }

        private void comm_DataReceived(byte[] readBuffer1)
        {
            try
            {
                string receive = Encoding.UTF8.GetString(readBuffer1);
                WriteLogFile(String.Format("Got {0} messages from other serial port", receive));
                UpdateAccess(receive, 2, 2);
                UpdateMessageBoard(receive, true);
            }
            catch (Exception e)
            {
                WriteLogFile(e.Message);
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void infoBox_SelectedIndexChanged(object sender, EventArgs e)
        {
        
        }

        //更新信息历史记录文本框
        private void UpdateMessageBoard(String text, bool receive)
        {
            if (this.history.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(UpdateMessageBoard);
                this.Invoke(d, new Object[] { text, receive });
            }
            else
            {
                if (receive)
                {
                    this.history.Text += DateTime.Now.ToString() + " I received " + text + "\r\n";
                }
                else
                {
                    this.history.Text += DateTime.Now.ToString() + " I sent " + text + "\r\n";
                }
                this.history.Focus();
                this.history.Select(this.history.TextLength, 0);
                this.history.ScrollToCaret();
            }
        }

        private void ConnectUI_KeyDown(Object sender, KeyEventArgs e)
        {
            
        }

        protected override void OnClosed(EventArgs e)
        {
            if(clieSocket != null)
                clieSocket.Close();
            if(servSocket != null)
                servSocket.Close();
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            //得到当前的时、分、秒
            int h = DateTime.Now.Hour;
            int m = DateTime.Now.Minute;
            int s = DateTime.Now.Second;
            //调用MyDrawClock绘制图形表盘
            MyDrawClock(h, m, s);
            //在窗体标题上显示数字时钟
            this.Text = String.Format("{0}:{1}:{2}", h, m, s);
        }

        private void MyDrawClock(int h, int m, int s)
        {
            Graphics g = this.CreateGraphics();
            //清除所有
            g.Clear(Color.White);
            //创建Pen
            Pen myPen = new Pen(Color.Black, 1);
            //标识圆的大小
            Rectangle r = new Rectangle(450, 50, 150, 150);
            //绘制表盘
            g.DrawEllipse(myPen, r);
            //表中心点
            Point centerPoint = new Point(525, 125);
            //计算出秒针，分针，时针的另外端点
            Point secPoint = new Point((int)(centerPoint.X + (Math.Sin(6 * s * Math.PI / 180)) * s_pinlen),
                    (int)(centerPoint.Y - (Math.Cos(6 * s * Math.PI / 180)) * s_pinlen));
            Point minPoint = new Point((int)(centerPoint.X + (Math.Sin(6 * m * Math.PI / 180)) * m_pinlen),
                       (int)(centerPoint.Y - (Math.Cos(6 * m * Math.PI / 180)) * m_pinlen));
            Point hourPoint = new Point((int)(centerPoint.X + (Math.Sin(((30 * h) + (m / 2)) * Math.PI / 180)) * h_pinlen),
                       (int)(centerPoint.Y - (Math.Cos(((30 * h) + (m / 2)) * Math.PI / 180)) * h_pinlen));
            //以不同的颜色绘制
            g.DrawLine(myPen, centerPoint, secPoint);
            myPen = new Pen(Color.Blue, 2);
            g.DrawLine(myPen, centerPoint, minPoint);
            myPen = new Pen(Color.Red, 2);
            g.DrawLine(myPen, centerPoint, hourPoint);

            Font myFont = new Font("Arial", 12, FontStyle.Bold);
            SolidBrush whiteBrush = new SolidBrush(Color.Blue);
            g.DrawString("12", myFont, whiteBrush, 513, 53);
            g.DrawString("6", myFont, whiteBrush, 520, 183);
            g.DrawString("3", myFont, whiteBrush, 583, 118);
            g.DrawString("9", myFont, whiteBrush, 453, 118);
        }

        //更新Access数据库
        private void UpdateAccess(string text, int sendOrReceive, int socketOrComm)
        {
            try
            {
                string sender = "", recipient = "";
                if (socketOrComm == 1)
                {
                    if (sendOrReceive == 1)
                    {
                        sender = ip.ToString() + ":" + port;
                        recipient = remoteIp.ToString() + ":" + remotePort;
                    }
                    else if (sendOrReceive == 2)
                    {
                        sender = remoteIp.ToString() + ":" + remotePort;
                        recipient = ip.ToString() + ":" + port;
                    }
                }
                else if (socketOrComm == 2)
                {
                    if (sendOrReceive == 1)
                    {
                        sender = comm.serialPort.PortName;
                        recipient = "";
                    }
                    else if (sendOrReceive == 2)
                    {
                        sender = "";
                        recipient = comm.serialPort.PortName;
                    }
                }
               
                string insertSQL = String.Format("Insert into {0} ([content], [timestamp], [sender], [recipient], [sendOrReceive], [transmission]) values('{1}', '{2}', '{3}', '{4}', {5}, {6})",
                    tableName,
                    text,
                    DateTime.Now.ToString(),
                    sender,
                    recipient,
                    sendOrReceive,
                    socketOrComm);
                OleDbCommand command = new OleDbCommand(insertSQL, conn);
                command.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                WriteLogFile(e.Message);
            }
        }

        //释放资源
        private void ConnectUI_FormClosed(object sender, FormClosedEventArgs e)
        {
            conn.Close();
            if (method.ToLower() == "socket")
            {
                clieSocket.Close();
                servSocket.Close();
                serverThread.Abort();
                clientThread.Abort();
                clieSocket = null;
                servSocket = null;
                serverThread = null;
                clientThread = null;
            }
        }

        // 写入日志文件
        private void WriteLogFile(string content)
        {
            byte[] contentByte = System.Text.Encoding.UTF8.GetBytes(DateTime.Now.ToString() + "  " + content + "\r\n");
            try
            {
                using (FileStream fWrite = new FileStream(@logFileName, FileMode.Append))
                {
                    fWrite.Write(contentByte, 0, contentByte.Length);
                    fWrite.Dispose();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return;
        }
    }
}