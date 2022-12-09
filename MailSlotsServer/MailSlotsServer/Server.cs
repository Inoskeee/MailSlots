using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;

namespace MailSlots
{
    public partial class frmMain : Form
    {
        private int ClientHandleMailSlot;       // дескриптор мэйлслота
        private string MailSlotName = "\\\\" + Dns.GetHostName() + "\\mailslot\\ServerMailslot";    // имя мэйлслота, Dns.GetHostName() - метод, возвращающий имя машины, на которой запущено приложение
        private Thread t;                       // поток для обслуживания мэйлслота
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом

        private List<string> _clients = new List<string>();

        // конструктор формы
        public frmMain()
        {
            InitializeComponent();

            // создание мэйлслота
            ClientHandleMailSlot = DIS.Import.CreateMailslot("\\\\.\\mailslot\\ServerMailslot", 0, DIS.Types.MAILSLOT_WAIT_FOREVER, 0);

            // вывод имени мэйлслота в заголовок формы, чтобы можно было его использовать для ввода имени в форме клиента, запущенного на другом вычислительном узле
            this.Text += "     " + MailSlotName;

            // создание потока, отвечающего за работу с мэйлслотом
            Thread t = new Thread(ReceiveMessage);
            t.Start();
        }

        private void ReceiveMessage()
        {
            string msg = "";            // прочитанное сообщение
            int MailslotSize = 0;       // максимальный размер сообщения
            int lpNextSize = 0;         // размер следующего сообщения
            int MessageCount = 0;       // количество сообщений в мэйлслоте
            uint realBytesReaded = 0;   // количество реально прочитанных из мэйлслота байтов

            // входим в бесконечный цикл работы с мэйлслотом
            while (_continue)
            {
                // получаем информацию о состоянии мэйлслота
                DIS.Import.GetMailslotInfo(ClientHandleMailSlot, MailslotSize, ref lpNextSize, ref MessageCount, 0);

                // если есть сообщения в мэйлслоте, то обрабатываем каждое из них
                if (MessageCount > 0)
                {
                    for (int i = 0; i < MessageCount; i++)
                    {
                        byte[] buff = new byte[1024];                           // буфер прочитанных из мэйлслота байтов
                        DIS.Import.FlushFileBuffers(ClientHandleMailSlot);      // "принудительная" запись данных, расположенные в буфере операционной системы, в файл мэйлслота
                        DIS.Import.ReadFile(ClientHandleMailSlot, buff, 1024, ref realBytesReaded, 0);      // считываем последовательность байтов из мэйлслота в буфер buff
                        msg = Encoding.Unicode.GetString(buff);                 // выполняем преобразование байтов в последовательность символов

                        string[] clientInfo = msg.Split(':');

                        if(clientInfo.Length > 1 && clientInfo[1].TrimEnd('\0') == "connected" )
                        {
                            rtbMessages.Invoke((MethodInvoker)delegate
                            {
                                if (clientInfo[0] != "")
                                {
                                    uint BytesWritten = 0;  // количество реально записанных в мэйлслот байт
                                    Int32 HandleMailSlot = DIS.Import.CreateFile($"\\\\.\\mailslot\\clients\\{clientInfo[0]}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
                                    byte[] clientBuff = Encoding.Unicode.GetBytes(rtbMessages.Text);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
                                    DIS.Import.WriteFile(HandleMailSlot, clientBuff, Convert.ToUInt32(clientBuff.Length), ref BytesWritten, 0);     // выполняем запись последовательности байт в мэйлслот
                                    
                                    rtbMessages.Text += $"\n >> {clientInfo[0]} подключился к серверу!";     // выводим полученное сообщение на форму
                                    msg = $"\n >> {clientInfo[0]} подключился к серверу!";
                                    _clients.Add(clientInfo[0]);
                                }
                            });
                        }
                        else if(clientInfo.Length > 1 && clientInfo[1].TrimEnd('\0') == "disconnected")
                        {
                            rtbMessages.Invoke((MethodInvoker)delegate
                            {
                                if (clientInfo[0] != "")
                                {
                                    rtbMessages.Text += $"\n >> {clientInfo[0]} отключился от сервера!";     // выводим полученное сообщение на форму
                                    msg = $"\n >> {clientInfo[0]} отключился от сервера!";
                                    _clients.Remove(clientInfo[0]);
                                }
                            });
                        }
                        else
                        {
                            rtbMessages.Invoke((MethodInvoker)delegate
                            {
                                if (msg != "")
                                {
                                    rtbMessages.Text += "\n >> " + msg + " \n";     // выводим полученное сообщение на форму
                                }
                                msg = msg.Insert(0, "\n >> ");
                            });
                        }


                        Thread.Sleep(500);                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                    }

                    foreach(var client in _clients)
                    {
                        msg = msg.TrimEnd('\0');
                        uint BytesWritten = 0;  // количество реально записанных в мэйлслот байт
                        Int32 HandleMailSlot = DIS.Import.CreateFile($"\\\\.\\mailslot\\clients\\{client}", DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
                        byte[] clientBuff = Encoding.Unicode.GetBytes(msg);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт
                        DIS.Import.WriteFile(HandleMailSlot, clientBuff, Convert.ToUInt32(clientBuff.Length), ref BytesWritten, 0);     // выполняем запись последовательности байт в мэйлслот

                    }

                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _continue = false;      // сообщаем, что работа с мэйлслотом завершена

            if (t != null)
                t.Abort();          // завершаем поток

            if (ClientHandleMailSlot != -1)
                DIS.Import.CloseHandle(ClientHandleMailSlot);            // закрываем дескриптор мэйлслота
        }
    }
}