using Microsoft.VisualBasic;
using static System.Net.Mime.MediaTypeNames;
using System.Drawing;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Dynamic;

namespace CinsAptServer
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>


        class CinsAptServer : Form
        {
            private TextBox conStatus;
            private ListBox results;
            private byte[] data = new byte[1024];
            private int size = 1024;
            private Socket server;
            public static List<Socket> socketList;
            public String weatherinfo = "";
            Thread curthread;
            Thread cardthread;
            Thread weatherThread;
            public CinsAptServer()
            {
                Text = "Asynchronous TCP Server";
                Size = new Size(400, 450);
                results = new ListBox();
                results.Parent = this;
                results.Location = new Point(10, 65);
                results.Size = new Size(350, 20 * Font.Height);
                Label label1 = new Label();
                label1.Parent = this;
                label1.Text = "Text received from client:";
                label1.AutoSize = true;
                label1.Location = new Point(10, 45);
                Label label2 = new Label();
                label2.Parent = this;
                label2.Text = "Connection Status:";
                label2.AutoSize = true;
                label2.Location = new Point(10, 330);
                conStatus = new TextBox();
                conStatus.Parent = this;
                conStatus.Text = "Waiting for client...";
                conStatus.Size = new Size(200, 2 * Font.Height);
                conStatus.Location = new Point(110, 325);
                Button stopServer = new Button();
                stopServer.Parent = this;
                stopServer.Text = "Stop Server";
                stopServer.Location = new Point(260, 32);
                stopServer.Size = new Size(7 * Font.Height, 2 * Font.Height);
                stopServer.Click += new EventHandler(ButtonStopOnClick);
                server = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint iep = new IPEndPoint(IPAddress.Any, 9050);
                socketList = new List<Socket>();
                curthread = new Thread(currency);
                curthread.IsBackground = true;
                cardthread = new Thread(cardReader);
                cardthread.IsBackground = true;
                weatherThread = new Thread(weather);
                weatherThread.IsBackground = true;
                server.Bind(iep);
                server.Listen(8);
                try
                {
                    server.BeginAccept(new AsyncCallback(AcceptConn), server);
                }
                catch (Exception e)
                {

                    MessageBox.Show(e.Message);
                }

                curthread.Start();
                cardthread.Start();
                weatherThread.Start();

            }
            void ButtonStopOnClick(object obj, EventArgs ea)
            {
                Close();
            }
            void AcceptConn(IAsyncResult iar)
            {
                try
                {
                    Socket oldserver = (Socket)iar.AsyncState;
                    Socket client = oldserver.EndAccept(iar);
                    socketList.Add(client);
                    conStatus.Text = "Connected to: " + client.RemoteEndPoint.ToString();
                    string stringData = weatherinfo;
                    byte[] message1 = Encoding.Unicode.GetBytes(stringData);
                    client.BeginSend(message1, 0, message1.Length, SocketFlags.None,
                    new AsyncCallback(SendData), client);
                    server.BeginAccept(new AsyncCallback(AcceptConn), server);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }

            }
            void sendWithoutWait(IAsyncResult iar)
            {
                try
                {
                    Socket client = (Socket)iar.AsyncState;
                    int sent = client.EndSend(iar);
                }
                catch (Exception e)
                {
                    //MessageBox.Show(e.ToString());
                }
            }
            void SendData(IAsyncResult iar)
            {

                try
                {
                    Socket client = (Socket)iar.AsyncState;
                    int sent = client.EndSend(iar);
                    client.BeginReceive(data, 0, size, SocketFlags.None,
                    new AsyncCallback(ReceiveData), client);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }

            }
            void ReceiveData(IAsyncResult iar)
            {
                Socket client = (Socket)iar.AsyncState;
                try
                {

                    int recv = client.EndReceive(iar);
                    if (recv == 0)
                    {
                        socketList.Remove(client);
                        client.Close();
                        return;
                    }
                    string receivedData = Encoding.Unicode.GetString(data, 0, recv);
                    results.Items.Add(receivedData);
                    results.Items.Add(socketList.Count);
                    byte[] message2 = Encoding.Unicode.GetBytes(receivedData);

                    foreach (Socket socket in socketList)
                    {
                        if (socket.Connected)
                        {
                            socket.BeginSend(message2, 0, message2.Length, SocketFlags.None,
                            new AsyncCallback(sendWithoutWait), socket);
                        }

                    }
                    client.BeginReceive(data, 0, size, SocketFlags.None,
                    new AsyncCallback(ReceiveData), client);
                    //client.BeginSend(message2, 0, message2.Length, SocketFlags.None,
                    //new AsyncCallback(SendData), client);
                }
                catch (Exception e)
                {
                    //MessageBox.Show(e.ToString());
                    socketList.Remove(client);
                    client.Close();
                }

            }

            async void weather()
            {
                while (true)
                {
                    try
                    {
                        string endpoint = "http://api.openweathermap.org/data/2.5/weather?q=Izmir,tr&APPID=970d2cf93919afdcfca4f8e35464379c";
                        HttpClient client = new HttpClient();
                        HttpResponseMessage response = await client.GetAsync(endpoint);
                        string responseContent = await response.Content.ReadAsStringAsync();
                        dynamic data = JsonConvert.DeserializeObject(responseContent);
                        double current_temp = data.main.temp;//returns kelvin
                        current_temp = current_temp - 273;//cast to celcius
                        int temp = (int)current_temp;
                        String current_weather = data.weather[0].description;
                        weatherinfo = ("+!#hava#!+ " + temp + " celcius. " + current_weather);
                        byte[] weathermessage = Encoding.Unicode.GetBytes(weatherinfo);
                        foreach (Socket socket in socketList)
                        {
                            socket.BeginSend(weathermessage, 0, weathermessage.Length, SocketFlags.None,
                                new AsyncCallback(sendWithoutWait), socket);
                        }
                        Thread.Sleep(60000);
                    }
                    catch (Exception e)
                    {

                        MessageBox.Show(e.ToString());
                    }
                }

            }
            void cardReader()
            {
                try
                {

                    string[] lines;
                    lines = System.IO.File.ReadAllLines("cardReader.txt");
                    for (int i = 0; i < lines.Length; i++)
                    {
                        DateTime currentTime = DateTime.Now;
                        int hour = currentTime.Hour;
                        int minute = currentTime.Minute;
                        int second = currentTime.Second;
                        String curtime = (hour + ":" + minute + ":" + second);
                        String cardinfo = "+!#kart#!+ " + curtime + ":" + lines[i];
                        byte[] cardmessage = Encoding.Unicode.GetBytes(cardinfo);
                        foreach (Socket socket in socketList)
                        {
                            socket.BeginSend(cardmessage, 0, cardmessage.Length, SocketFlags.None,
                                new AsyncCallback(sendWithoutWait), socket);
                        }
                        Random rand = new Random();
                        int randomNumber = rand.Next(5000);
                        Thread.Sleep(10000 + randomNumber);
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }

            }
            async void currency()
            {
                while (true)
                {
                    try
                    {
                        string endpoint = "https://api.exchangerate-api.com/v4/latest/TRY";
                        HttpClient client = new HttpClient();
                        HttpResponseMessage response = await client.GetAsync(endpoint);
                        string responseContent = await response.Content.ReadAsStringAsync();
                        dynamic data = JsonConvert.DeserializeObject(responseContent);
                        double dolar = data.rates.USD;
                        dolar = 1 / dolar;
                        double gbp = data.rates.GBP;
                        gbp = 1 / gbp;
                        double eur = data.rates.EUR;
                        eur = 1 / eur;
                        //our api doesnt provide a momentary info about dollar/tl, it updates in minutes so we faked like we have momentary info about currencies.
                        for (int i = 0; i <= 10; i++)
                        {
                            Random rand = new Random();
                            double randomNumber = rand.NextDouble() * 20 - 10;
                            randomNumber = randomNumber / 1000;
                            dolar += randomNumber;

                            rand = new Random();
                            randomNumber = rand.NextDouble() * 20 - 10;
                            randomNumber = randomNumber / 1000;
                            gbp += randomNumber;

                            rand = new Random();
                            randomNumber = rand.NextDouble() * 20 - 10;
                            randomNumber = randomNumber / 1000;
                            eur += randomNumber;

                            String curdata = ("+!#para#!+" + " " + dolar.ToString("F5") + " " + gbp.ToString("F5") + " " + eur.ToString("F5"));
                            byte[] currencymessage = Encoding.Unicode.GetBytes(curdata);


                            if (socketList.Count > 0)
                            {
                                foreach (Socket socket in socketList)
                                {
                                    socket.BeginSend(currencymessage, 0, currencymessage.Length, SocketFlags.None,
                                           new AsyncCallback(sendWithoutWait), socket);
                                }
                                Thread.Sleep(4000);
                            }




                        }
                    }
                    catch (Exception e)
                    {

                        MessageBox.Show(e.ToString());
                    }

                }
            }
            public static void Main()
            {
                try
                {
                    System.Windows.Forms.Application.Run(new CinsAptServer());
                }
                catch (Exception e)
                {

                    MessageBox.Show(e.ToString());
                }

            }
        }
    }
}