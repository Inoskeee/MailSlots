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
using System.IO;
using System.Threading;

namespace MailSlots
{
    public partial class frmMain : Form
    {
        private Int32 HandleMailSlot;   // дескриптор мэйлслота
        private int ClientHandleMailSlot;       // дескриптор мэйлслота
        private Thread t;                       // поток для обслуживания мэйлслота
        private bool _continue = true;          // флаг, указывающий продолжается ли работа с мэйлслотом
        private string mailSlotAddres;
        // конструктор формы
        public frmMain()
        {
            InitializeComponent();
            this.Text += "     " + Dns.GetHostName();   // выводим имя текущей машины в заголовок формы
            tbMailSlot.Text = "ServerMailslot";
            mailSlotAddres = $"\\\\*\\mailslot\\{tbMailSlot.Text}";
        }

        // присоединение к мэйлслоту
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(loginText.Text))
                {
                    // открываем мэйлслот, имя которого указано в поле tbMailSlot
                    HandleMailSlot = DIS.Import.CreateFile(mailSlotAddres, DIS.Types.EFileAccess.GenericWrite, DIS.Types.EFileShare.Read, 0, DIS.Types.ECreationDisposition.OpenExisting, 0, 0);
                    if (HandleMailSlot != -1)
                    {
                        btnConnect.Enabled = false;
                        btnSend.Enabled = true;
                        loginText.Enabled = false;
                        // создание мэйлслота
                        ClientHandleMailSlot = DIS.Import.CreateMailslot($"\\\\.\\mailslot\\clients\\{loginText.Text}", 0, DIS.Types.MAILSLOT_WAIT_FOREVER, 0);

                        // создание потока, отвечающего за работу с мэйлслотом
                        Thread t = new Thread(ReceiveMessage);
                        t.Start();

                        uint BytesWritten = 0;  // количество реально записанных в мэйлслот байт
                        byte[] buff = Encoding.Unicode.GetBytes($"{loginText.Text}:connected");    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

                        DIS.Import.WriteFile(HandleMailSlot, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);     // выполняем запись последовательности байт в мэйлслот
                    }
                    else
                    {
                        MessageBox.Show("Не удалось подключиться к мейлслоту");
                    }
                }
                else
                {
                    MessageBox.Show("Введите логин");
                }
                    
            }
            catch
            {
                MessageBox.Show("Не удалось подключиться к мейлслоту");
            }
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
                    for (int i = 0; i < MessageCount; i++)
                    {
                        byte[] buff = new byte[1024];                           // буфер прочитанных из мэйлслота байтов
                        DIS.Import.FlushFileBuffers(ClientHandleMailSlot);      // "принудительная" запись данных, расположенные в буфере операционной системы, в файл мэйлслота
                        DIS.Import.ReadFile(ClientHandleMailSlot, buff, 1024, ref realBytesReaded, 0);      // считываем последовательность байтов из мэйлслота в буфер buff
                        msg = Encoding.Unicode.GetString(buff);                 // выполняем преобразование байтов в последовательность символов

                        messageText.Invoke((MethodInvoker)delegate
                        {
                            if (msg != "")
                                messageText.Text += $"{msg}";     // выводим полученное сообщение на форму
                        });
                        Thread.Sleep(500);                                      // приостанавливаем работу потока перед тем, как приcтупить к обслуживанию очередного клиента
                    }
            }
        }

        // отправка сообщения
        private void btnSend_Click(object sender, EventArgs e)
        {
            uint BytesWritten = 0;  // количество реально записанных в мэйлслот байт
            byte[] buff = Encoding.Unicode.GetBytes(loginText.Text.ToString() + " >> " + tbMessage.Text);    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            DIS.Import.WriteFile(HandleMailSlot, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);     // выполняем запись последовательности байт в мэйлслот
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            uint BytesWritten = 0;  // количество реально записанных в мэйлслот байт
            byte[] buff = Encoding.Unicode.GetBytes($"{loginText.Text}:disconnected");    // выполняем преобразование сообщения (вместе с идентификатором машины) в последовательность байт

            DIS.Import.WriteFile(HandleMailSlot, buff, Convert.ToUInt32(buff.Length), ref BytesWritten, 0);     // выполняем запись последовательности байт в мэйлслот
            
            DIS.Import.CloseHandle(HandleMailSlot);     // закрываем дескриптор мэйлслота

            _continue = false;      // сообщаем, что работа с мэйлслотом завершена

            if (t != null)
                t.Abort();          // завершаем поток

            if (ClientHandleMailSlot != -1)
                DIS.Import.CloseHandle(ClientHandleMailSlot);            // закрываем дескриптор мэйлслота
        }
    }
}