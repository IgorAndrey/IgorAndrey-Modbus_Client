using System;
using System.Net.Sockets;
using System.Net;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using System.Collections.Generic;


public class LogDataArgs
{
  public LogDataArgs(string __data) { _data = __data; }
  public string _data { get; private set; }
}

namespace EasyModbus
{
  /// <summary>
  /// Implements a ModbusClient.
  /// </summary>
  public partial class ModbusClient
  {
    public enum RegisterOrder { LowHigh = 0, HighLow = 1 };
    private bool debug = true;
    public string StrLog = "";
    private TcpClient tcpClient;
    private string ipAddress = "127.0.0.1";
    private int port = 502;
    private uint transactionIdentifierInternal = 0;
    private byte[] transactionIdentifier = new byte[2];
    private byte[] protocolIdentifier = new byte[2];
    private byte[] crc = new byte[2];
    private byte[] length = new byte[2];
    private byte unitIdentifier = 0x01;
    private byte functionCode;
    private byte[] startingAddress = new byte[2];
    private byte[] quantity = new byte[2];
    private bool udpFlag = false;
    private int portOut;
    private int baudRate = 9600;
    private int connectTimeout = 1000;
    public byte[] receiveData;
    public byte[] sendData;
    private SerialPort serialport;
    private Parity parity = Parity.Even;
    private StopBits stopBits = StopBits.One;
    private bool connected = false;
    public int NumberOfRetries { get; set; } = 3;
    private int countRetries = 0;

    public delegate void ReceiveDataChanged(object sender);
    public event ReceiveDataChanged receiveDataChanged;

    public delegate void SendDataChanged(object sender);
    public event SendDataChanged sendDataChanged;

    public delegate void ConnectedChanged(object sender);
    public event ConnectedChanged connectedChanged;

    public delegate void LogChanged(object sender, LogDataArgs e);
    public event LogChanged LogDataChanged;

    NetworkStream stream;

    /// <summary>
    /// Constructor which determines the Master ip-address and the Master Port.
    /// </summary>
    /// <param name="ipAddress">IP-Address of the Master device</param>
    /// <param name="port">Listening port of the Master device (should be 502)</param>
    public ModbusClient(string ipAddress, int port)
    {
      this.ipAddress = ipAddress;
      this.port = port;
    }

    /// <summary>
    /// Constructor which determines the Serial-Port
    /// </summary>
    /// <param name="serialPort">Serial-Port Name e.G. "COM1"</param>
    public ModbusClient(string serialPort)
    {

      this.serialport = new SerialPort();
      serialport.PortName = serialPort;
      serialport.BaudRate = baudRate;
      serialport.Parity = parity;
      serialport.StopBits = stopBits;
      serialport.WriteTimeout = 10000;
      serialport.ReadTimeout = connectTimeout;

      serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
    }

    /// <summary>
    /// Parameterless constructor
    /// </summary>
    public ModbusClient()
    {
    }

    /// <summary>
    /// Establish connection to Master device in case of Modbus TCP. Opens COM-Port in case of Modbus RTU
    /// </summary>
    public void Connect()
    {
      if (serialport != null)
      {
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Open Serial port " + serialport.PortName+ System.Environment.NewLine));
          serialport.BaudRate = baudRate;
          serialport.Parity = parity;
          serialport.StopBits = stopBits;
          serialport.WriteTimeout = 10000;
          serialport.ReadTimeout = connectTimeout;
          serialport.Open();
          connected = true;


        }
        if (connectedChanged != null)
          try
          {
            connectedChanged(this);
          }
          catch
          {

          }
        return;
      }
      if (!udpFlag)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Open TCP-Socket, IP-Address: " + ipAddress + ", Port: " + port+ System.Environment.NewLine));
        tcpClient = new TcpClient();
        var result = tcpClient.BeginConnect(ipAddress, port, null, null);
        var success = result.AsyncWaitHandle.WaitOne(connectTimeout);
        if (!success)
        {
          throw new EasyModbus.Exceptions.ConnectionException("connection timed out");
        }
        tcpClient.EndConnect(result);

        //tcpClient = new TcpClient(ipAddress, port);
        stream = tcpClient.GetStream();
        stream.ReadTimeout = connectTimeout;
        connected = true;
      }
      else
      {
        tcpClient = new TcpClient();
        connected = true;
      }
      if (connectedChanged != null)
        try
        {
          connectedChanged(this);
        }
        catch
        {

        }
    }

    /// <summary>
    /// Establish connection to Master device in case of Modbus TCP.
    /// </summary>
    public void Connect(string ipAddress, int port)
    {
      if (!udpFlag)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Open TCP-Socket, IP-Address: " + ipAddress + ", Port: " + port+ System.Environment.NewLine));
        tcpClient = new TcpClient();
        var result = tcpClient.BeginConnect(ipAddress, port, null, null);
        var success = result.AsyncWaitHandle.WaitOne(connectTimeout);
        if (!success)
        {
          throw new EasyModbus.Exceptions.ConnectionException("connection timed out");
        }
        tcpClient.EndConnect(result);

        //tcpClient = new TcpClient(ipAddress, port);
        stream = tcpClient.GetStream();
        stream.ReadTimeout = connectTimeout;
        connected = true;
      }
      else
      {
        tcpClient = new TcpClient();
        connected = true;
      }

      if (connectedChanged != null)
        connectedChanged(this);
    }

    public static UInt16 calculateCRC(byte[] data, UInt16 numberOfBytes, int startByte)
    {
      byte[] auchCRCHi = {
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
            0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
            0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81,
            0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0,
            0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
            0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
            0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01,
            0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0,
            0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01,
            0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81,
            0x40
            };

      byte[] auchCRCLo = {
            0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7, 0x05, 0xC5, 0xC4,
            0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E, 0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09,
            0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9, 0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD,
            0x1D, 0x1C, 0xDC, 0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
            0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32, 0x36, 0xF6, 0xF7,
            0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D, 0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A,
            0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38, 0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE,
            0x2E, 0x2F, 0xEF, 0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
            0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1, 0x63, 0xA3, 0xA2,
            0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4, 0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F,
            0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB, 0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB,
            0x7B, 0x7A, 0xBA, 0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
            0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0, 0x50, 0x90, 0x91,
            0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97, 0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C,
            0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E, 0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88,
            0x48, 0x49, 0x89, 0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
            0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83, 0x41, 0x81, 0x80,
            0x40
            };
      UInt16 usDataLen = numberOfBytes;
      byte uchCRCHi = 0xFF;
      byte uchCRCLo = 0xFF;
      int i = 0;
      int uIndex;
      while (usDataLen > 0)
      {
        usDataLen--;
        if ((i + startByte) < data.Length)
        {
          uIndex = uchCRCLo ^ data[i + startByte];
          uchCRCLo = (byte)(uchCRCHi ^ auchCRCHi[uIndex]);
          uchCRCHi = auchCRCLo[uIndex];
        }
        i++;
      }
      return (UInt16)((UInt16)uchCRCHi << 8 | uchCRCLo);
    }

    private bool dataReceived = false;
    private bool receiveActive = false;
    private byte[] readBuffer = new byte[256];
    private int bytesToRead = 0;

    private void DataReceivedHandler(object sender,
                    SerialDataReceivedEventArgs e)
    {
      serialport.DataReceived -= DataReceivedHandler;
      receiveActive = true;

      const long ticksWait = TimeSpan.TicksPerMillisecond * 2000;//((40*10000000) / this.baudRate);


      SerialPort sp = (SerialPort)sender;
      if (bytesToRead == 0)
      {
        sp.DiscardInBuffer();
        receiveActive = false;
        serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
        return;
      }
      readBuffer = new byte[256];
      int numbytes = 0;
      int actualPositionToRead = 0;
      DateTime dateTimeLastRead = DateTime.Now;
      do
      {
        try
        {
          dateTimeLastRead = DateTime.Now;
          while ((sp.BytesToRead) == 0)
          {
            //System.Threading.Thread.Sleep(10);
            if ((DateTime.Now.Ticks - dateTimeLastRead.Ticks) > ticksWait)
              break;
          }
          numbytes = sp.BytesToRead;
          byte[] rxbytearray = new byte[numbytes];
          sp.Read(rxbytearray, 0, numbytes);
          Array.Copy(rxbytearray, 0, readBuffer, actualPositionToRead, (actualPositionToRead + rxbytearray.Length) <= bytesToRead ? rxbytearray.Length : bytesToRead - actualPositionToRead);
          actualPositionToRead = actualPositionToRead + rxbytearray.Length;
        }
        catch (Exception)
        {
        }
        if (bytesToRead <= actualPositionToRead)
          break;
        if (DetectValidModbusFrame(readBuffer, (actualPositionToRead < readBuffer.Length) ? actualPositionToRead : readBuffer.Length) | bytesToRead <= actualPositionToRead)
          break;
      }
      while ((DateTime.Now.Ticks - dateTimeLastRead.Ticks) < ticksWait);

      //10.000 Ticks in 1 ms

      receiveData = new byte[actualPositionToRead];
      Array.Copy(readBuffer, 0, receiveData, 0, (actualPositionToRead < readBuffer.Length) ? actualPositionToRead : readBuffer.Length);
      StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Received Serial-Data: " + BitConverter.ToString(receiveData) + System.Environment.NewLine;

      bytesToRead = 0;
      dataReceived = true;
      receiveActive = false;
      serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
      if (receiveDataChanged != null)
      {
        receiveDataChanged(this);

      }

      //sp.DiscardInBuffer();
    }

    public static bool DetectValidModbusFrame(byte[] readBuffer, int length)
    {
      // minimum length 6 bytes
      if (length < 6)
        return false;
      //SlaveID correct
      if (readBuffer[0] > 247)
        return false;
      //CRC correct?
      byte[] crc = new byte[2];
      crc = BitConverter.GetBytes(calculateCRC(readBuffer, (ushort)(length - 2), 0));
      if (crc[0] != readBuffer[length - 2] | crc[1] != readBuffer[length - 1])
        return false;
      return true;
    }

    public bool[] ReadDiscreteInputs(int startingAddress, int quantity)
    {
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC2 (Read Discrete Inputs from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      if (startingAddress > 65535 | quantity > 2000)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ArgumentException Throwed"+ System.Environment.NewLine));
        throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
      }
      bool[] response;
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)0x0006);
      this.functionCode = 0x02;
      this.startingAddress = BitConverter.GetBytes(startingAddress);
      this.quantity = BitConverter.GetBytes(quantity);
      Byte[] data = new byte[]
                      {
                            this.transactionIdentifier[1],
              this.transactionIdentifier[0],
              this.protocolIdentifier[1],
              this.protocolIdentifier[0],
              this.length[1],
              this.length[0],
              this.unitIdentifier,
              this.functionCode,
              this.startingAddress[1],
              this.startingAddress[0],
              this.quantity[1],
              this.quantity[0],
                            this.crc[0],
                            this.crc[1]
                      };
      crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
      data[12] = crc[0];
      data[13] = crc[1];

      if (serialport != null)
      {
        dataReceived = false;
        if (quantity % 8 == 0)
          bytesToRead = 5 + quantity / 8;
        else
          bytesToRead = 6 + quantity / 8;
        //               serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, 8);
        if (debug)
        {
          byte[] debugData = new byte[8];
          Array.Copy(data, 6, debugData, 0, 8);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[8];
          Array.Copy(data, 6, sendData, 0, 8);
          sendDataChanged(this);

        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;


        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
        }
        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);
          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x82 & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x82 & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x82 & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x82 & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
        if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
          }
          else
          {
            countRetries++;
            return ReadDiscreteInputs(startingAddress, quantity);
          }
        }
        else if (!dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new TimeoutException("No Response from Modbus Slave");
          }
          else
          {
            countRetries++;
            return ReadDiscreteInputs(startingAddress, quantity);
          }
        }
      }
      response = new bool[quantity];
      for (int i = 0; i < quantity; i++)
      {
        int intData = data[9 + i / 8];
        int mask = Convert.ToInt32(Math.Pow(2, (i % 8)));
        response[i] = Convert.ToBoolean((intData & mask) / mask);
      }
      return (response);
    }

    public bool[] ReadCoils(int startingAddress, int quantity)
    {
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC1 (Read Coils from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      if (startingAddress > 65535 | quantity > 2000)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ArgumentException Throwed"+ System.Environment.NewLine));
        throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
      }
      bool[] response;
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)0x0006);
      this.functionCode = 0x01;
      this.startingAddress = BitConverter.GetBytes(startingAddress);
      this.quantity = BitConverter.GetBytes(quantity);
      Byte[] data = new byte[]{
                            this.transactionIdentifier[1],
              this.transactionIdentifier[0],
              this.protocolIdentifier[1],
              this.protocolIdentifier[0],
              this.length[1],
              this.length[0],
              this.unitIdentifier,
              this.functionCode,
              this.startingAddress[1],
              this.startingAddress[0],
              this.quantity[1],
              this.quantity[0],
                            this.crc[0],
                            this.crc[1]
            };

      crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
      data[12] = crc[0];
      data[13] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        if (quantity % 8 == 0)
          bytesToRead = 5 + quantity / 8;
        else
          bytesToRead = 6 + quantity / 8;
        //               serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, 8);
        if (debug)
        {
          byte[] debugData = new byte[8];
          Array.Copy(data, 6, debugData, 0, 8);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[8];
          Array.Copy(data, 6, sendData, 0, 8);
          sendDataChanged(this);

        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;
        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];

          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
        }
        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send MocbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);

          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x81 & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x81 & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x81 & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x81 & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
        if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
          }
          else
          {
            countRetries++;
            return ReadCoils(startingAddress, quantity);
          }
        }
        else if (!dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new TimeoutException("No Response from Modbus Slave");
          }
          else
          {
            countRetries++;
            return ReadCoils(startingAddress, quantity);
          }
        }
      }
      response = new bool[quantity];
      for (int i = 0; i < quantity; i++)
      {
        int intData = data[9 + i / 8];
        int mask = Convert.ToInt32(Math.Pow(2, (i % 8)));
        response[i] = Convert.ToBoolean((intData & mask) / mask);
      }
      return (response);
    }

    public UInt16[] ReadHoldingRegisters(int startingAddress, int quantity)
    {
      StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC3 (Read Holding Registers from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity+ System.Environment.NewLine;
      transactionIdentifierInternal++;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine;
          try { throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened"); }
          catch { StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "serial port not opened" + System.Environment.NewLine; }
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine;
        try { throw new EasyModbus.Exceptions.ConnectionException("connection error"); }
        catch { StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "connection error" + System.Environment.NewLine; }
      }
      if (startingAddress > 65535 | quantity > 125)
      {
        StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ArgumentException Throwed"+ System.Environment.NewLine;
        throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 125");
      }
      UInt16[] response;
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)0x0006);
      this.functionCode = 0x03;
      this.startingAddress = BitConverter.GetBytes(startingAddress);
      this.quantity = BitConverter.GetBytes(quantity);
      Byte[] data = new byte[]{ this.transactionIdentifier[1],
              this.transactionIdentifier[0],
              this.protocolIdentifier[1],
              this.protocolIdentifier[0],
              this.length[1],
              this.length[0],
              this.unitIdentifier,
              this.functionCode,
              this.startingAddress[1],
              this.startingAddress[0],
              this.quantity[1],
              this.quantity[0],
                            this.crc[0],
                            this.crc[1]
            };
      crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
      data[12] = crc[0];
      data[13] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        bytesToRead = 5 + 2 * quantity;
        //                serialport.ReceivedBytesThreshold = bytesToRead;
        try
        {
          serialport.Write(data, 6, 8);
        }
        catch (Exception e)
        {
          StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + e.ToString()+ System.Environment.NewLine;
        }

        if (debug)
        {
          byte[] debugData = new byte[8];
          Array.Copy(data, 6, debugData, 0, 8);
          StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine;
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[8];
          Array.Copy(data, 6, sendData, 0, 8);
          sendDataChanged(this);
        }
        data = new byte[2100];
        readBuffer = new byte[256];

        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;
        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);

          receivedUnitIdentifier = data[6];
        }
        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine;
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);
          }
          data = new Byte[256];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine;
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x83 & data[8] == 0x01)
      {
        StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine;
        try { throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master"); }
        catch { StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Function code not supported by master" + System.Environment.NewLine; }
      }
      if (data[7] == 0x83 & data[8] == 0x02)
      {
        StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine;
        try { throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid"); }
        catch { StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Starting address invalid or starting address + quantity invalid" + System.Environment.NewLine; }
      }
      if (data[7] == 0x83 & data[8] == 0x03)
      {
        StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine;
        try { throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid"); }
        catch { StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "quantity invalid" + System.Environment.NewLine; }

      }
      if (data[7] == 0x83 & data[8] == 0x04)
      {
        StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine;
        try { throw new EasyModbus.Exceptions.ModbusException("error reading"); }
        catch { StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "error reading"; }
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
        if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
        {
          StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine;
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            try { throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed"); }
            catch { StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Response CRC check failed" + System.Environment.NewLine; }
          }
          else
          {
            countRetries++;
            LogDataChanged(this, new LogDataArgs(StrLog));
            StrLog = "";
            return ReadHoldingRegisters(startingAddress, quantity);
          }
        }
        else if (!dataReceived)
        {
          StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine;
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            try
            {
              throw new TimeoutException("No Response from Modbus Slave");
            }
            catch
            {
              StrLog += DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "No Response from Modbus Slave" + System.Environment.NewLine;
            }
          }
          else
          {
            countRetries++;
            LogDataChanged(this, new LogDataArgs(StrLog));
            StrLog = "";
            return ReadHoldingRegisters(startingAddress, quantity);
          }
        }
      }
      response = new UInt16[quantity];
      for (int i = 0; i < quantity; i++)
      {
        byte lowByte;
        byte highByte;
        highByte = data[9 + i * 2];
        lowByte = data[9 + i * 2 + 1];

        data[9 + i * 2] = lowByte;
        data[9 + i * 2 + 1] = highByte;

        response[i] = BitConverter.ToUInt16(data, (9 + i * 2));
      }
      LogDataChanged(this, new LogDataArgs(StrLog));
      StrLog = "";
      return (response);
    }

    public UInt16[] ReadInputRegisters(int startingAddress, int quantity)
    {

      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC4 (Read Input Registers from Master device), StartingAddress: " + startingAddress + ", Quantity: " + quantity+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      if (startingAddress > 65535 | quantity > 125)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ArgumentException Throwed"+ System.Environment.NewLine));
        throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 125");
      }
      UInt16[] response;
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)0x0006);
      this.functionCode = 0x04;
      this.startingAddress = BitConverter.GetBytes(startingAddress);
      this.quantity = BitConverter.GetBytes(quantity);
      Byte[] data = new byte[]{ this.transactionIdentifier[1],
              this.transactionIdentifier[0],
              this.protocolIdentifier[1],
              this.protocolIdentifier[0],
              this.length[1],
              this.length[0],
              this.unitIdentifier,
              this.functionCode,
              this.startingAddress[1],
              this.startingAddress[0],
              this.quantity[1],
              this.quantity[0],
                            this.crc[0],
                            this.crc[1]
            };
      crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
      data[12] = crc[0];
      data[13] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        bytesToRead = 5 + 2 * quantity;


        //               serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, 8);
        if (debug)
        {
          byte[] debugData = new byte[8];
          Array.Copy(data, 6, debugData, 0, 8);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[8];
          Array.Copy(data, 6, sendData, 0, 8);
          sendDataChanged(this);

        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;

        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
        }

        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);
          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x84 & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x84 & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x84 & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x84 & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data[8] + 3), 6));
        if ((crc[0] != data[data[8] + 9] | crc[1] != data[data[8] + 10]) & dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
          }
          else
          {
            countRetries++;
            return ReadInputRegisters(startingAddress, quantity);
          }
        }
        else if (!dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new TimeoutException("No Response from Modbus Slave");

          }
          else
          {
            countRetries++;
            return ReadInputRegisters(startingAddress, quantity);
          }

        }
      }
      response = new UInt16[quantity];
      for (int i = 0; i < quantity; i++)
      {
        byte lowByte;
        byte highByte;
        highByte = data[9 + i * 2];
        lowByte = data[9 + i * 2 + 1];

        data[9 + i * 2] = lowByte;
        data[9 + i * 2 + 1] = highByte;

        response[i] = BitConverter.ToUInt16(data, (9 + i * 2));
      }
      return (response);
    }


    /// <summary>
    /// Write single Coil to Master device (FC5).
    /// </summary>
    /// <param name="startingAddress">Coil to be written</param>
    /// <param name="value">Coil Value to be written</param>
    public void WriteSingleCoil(int startingAddress, bool value)
    {

      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC5 (Write single coil to Master device), StartingAddress: " + startingAddress + ", Value: " + value+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      byte[] coilValue = new byte[2];
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)0x0006);
      this.functionCode = 0x05;
      this.startingAddress = BitConverter.GetBytes(startingAddress);
      if (value == true)
      {
        coilValue = BitConverter.GetBytes((int)0xFF00);
      }
      else
      {
        coilValue = BitConverter.GetBytes((int)0x0000);
      }
      Byte[] data = new byte[]{ this.transactionIdentifier[1],
              this.transactionIdentifier[0],
              this.protocolIdentifier[1],
              this.protocolIdentifier[0],
              this.length[1],
              this.length[0],
              this.unitIdentifier,
              this.functionCode,
              this.startingAddress[1],
              this.startingAddress[0],
              coilValue[1],
              coilValue[0],
                            this.crc[0],
                            this.crc[1]
                            };
      crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
      data[12] = crc[0];
      data[13] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        bytesToRead = 8;
        //               serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, 8);
        if (debug)
        {
          byte[] debugData = new byte[8];
          Array.Copy(data, 6, debugData, 0, 8);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[8];
          Array.Copy(data, 6, sendData, 0, 8);
          sendDataChanged(this);

        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;
        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
          countRetries = 0;
        }

      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);

          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x85 & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x85 & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x85 & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x85 & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
        if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
          }
          else
          {
            countRetries++;
            WriteSingleCoil(startingAddress, value);
          }
        }
        else if (!dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new TimeoutException("No Response from Modbus Slave");

          }
          else
          {
            countRetries++;
            WriteSingleCoil(startingAddress, value);
          }
        }
      }
    }


    /// <summary>
    /// Write single Register to Master device (FC6).
    /// </summary>
    /// <param name="startingAddress">Register to be written</param>
    /// <param name="value">Register Value to be written</param>
    public void WriteSingleRegister(int startingAddress, int value)
    {
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC6 (Write single register to Master device), StartingAddress: " + startingAddress + ", Value: " + value+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      byte[] registerValue = new byte[2];
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)0x0006);
      this.functionCode = 0x06;
      this.startingAddress = BitConverter.GetBytes(startingAddress);
      registerValue = BitConverter.GetBytes((int)value);

      Byte[] data = new byte[]{ this.transactionIdentifier[1],
              this.transactionIdentifier[0],
              this.protocolIdentifier[1],
              this.protocolIdentifier[0],
              this.length[1],
              this.length[0],
              this.unitIdentifier,
              this.functionCode,
              this.startingAddress[1],
              this.startingAddress[0],
              registerValue[1],
              registerValue[0],
                            this.crc[0],
                            this.crc[1]
                            };
      crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
      data[12] = crc[0];
      data[13] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        bytesToRead = 8;
        //                serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, 8);
        if (debug)
        {
          byte[] debugData = new byte[8];
          Array.Copy(data, 6, debugData, 0, 8);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[8];
          Array.Copy(data, 6, sendData, 0, 8);
          sendDataChanged(this);

        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;
        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
        }
        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);

          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x86 & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x86 & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x86 & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x86 & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
        if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
          }
          else
          {
            countRetries++;
            WriteSingleRegister(startingAddress, value);
          }
        }
        else if (!dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new TimeoutException("No Response from Modbus Slave");

          }
          else
          {
            countRetries++;
            WriteSingleRegister(startingAddress, value);
          }
        }
      }
    }

    /// <summary>
    /// Write multiple coils to Master device (FC15).
    /// </summary>
    /// <param name="startingAddress">First coil to be written</param>
    /// <param name="values">Coil Values to be written</param>
    public void WriteMultipleCoils(int startingAddress, bool[] values)
    {
      string debugString = "";
      for (int i = 0; i < values.Length; i++)
        debugString = debugString + values[i] + " ";
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC15 (Write multiple coils to Master device), StartingAddress: " + startingAddress + ", Values: " + debugString+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      byte byteCount = (byte)((values.Length % 8 != 0 ? values.Length / 8 + 1 : (values.Length / 8)));
      byte[] quantityOfOutputs = BitConverter.GetBytes((int)values.Length);
      byte singleCoilValue = 0;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)(7 + (byteCount)));
      this.functionCode = 0x0F;
      this.startingAddress = BitConverter.GetBytes(startingAddress);



      Byte[] data = new byte[14 + 2 + (values.Length % 8 != 0 ? values.Length / 8 : (values.Length / 8) - 1)];
      data[0] = this.transactionIdentifier[1];
      data[1] = this.transactionIdentifier[0];
      data[2] = this.protocolIdentifier[1];
      data[3] = this.protocolIdentifier[0];
      data[4] = this.length[1];
      data[5] = this.length[0];
      data[6] = this.unitIdentifier;
      data[7] = this.functionCode;
      data[8] = this.startingAddress[1];
      data[9] = this.startingAddress[0];
      data[10] = quantityOfOutputs[1];
      data[11] = quantityOfOutputs[0];
      data[12] = byteCount;
      for (int i = 0; i < values.Length; i++)
      {
        if ((i % 8) == 0)
          singleCoilValue = 0;
        byte CoilValue;
        if (values[i] == true)
          CoilValue = 1;
        else
          CoilValue = 0;


        singleCoilValue = (byte)((int)CoilValue << (i % 8) | (int)singleCoilValue);

        data[13 + (i / 8)] = singleCoilValue;
      }
      crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data.Length - 8), 6));
      data[data.Length - 2] = crc[0];
      data[data.Length - 1] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        bytesToRead = 8;
        //               serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, data.Length - 6);
        if (debug)
        {
          byte[] debugData = new byte[data.Length - 6];
          Array.Copy(data, 6, debugData, 0, data.Length - 6);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[data.Length - 6];
          Array.Copy(data, 6, sendData, 0, data.Length - 6);
          sendDataChanged(this);

        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;
        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
        }
        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);

          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x8F & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x8F & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x8F & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x8F & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
        if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
          }
          else
          {
            countRetries++;
            WriteMultipleCoils(startingAddress, values);
          }
        }
        else if (!dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new TimeoutException("No Response from Modbus Slave");

          }
          else
          {
            countRetries++;
            WriteMultipleCoils(startingAddress, values);
          }
        }
      }
    }

    /// <summary>
    /// Write multiple registers to Master device (FC16).
    /// </summary>
    /// <param name="startingAddress">First register to be written</param>
    /// <param name="values">register Values to be written</param>
    public void WriteMultipleRegisters(int startingAddress, int[] values)
    {
      string debugString = "";
      for (int i = 0; i < values.Length; i++)
        debugString = debugString + values[i] + " ";
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC16 (Write multiple Registers to Server device), StartingAddress: " + startingAddress + ", Values: " + debugString+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      byte byteCount = (byte)(values.Length * 2);
      byte[] quantityOfOutputs = BitConverter.GetBytes((int)values.Length);
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)(7 + values.Length * 2));
      this.functionCode = 0x10;
      this.startingAddress = BitConverter.GetBytes(startingAddress);

      Byte[] data = new byte[13 + 2 + values.Length * 2];
      data[0] = this.transactionIdentifier[1];
      data[1] = this.transactionIdentifier[0];
      data[2] = this.protocolIdentifier[1];
      data[3] = this.protocolIdentifier[0];
      data[4] = this.length[1];
      data[5] = this.length[0];
      data[6] = this.unitIdentifier;
      data[7] = this.functionCode;
      data[8] = this.startingAddress[1];
      data[9] = this.startingAddress[0];
      data[10] = quantityOfOutputs[1];
      data[11] = quantityOfOutputs[0];
      data[12] = byteCount;
      for (int i = 0; i < values.Length; i++)
      {
        byte[] singleRegisterValue = BitConverter.GetBytes((int)values[i]);
        data[13 + i * 2] = singleRegisterValue[1];
        data[14 + i * 2] = singleRegisterValue[0];
      }
      crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data.Length - 8), 6));
      data[data.Length - 2] = crc[0];
      data[data.Length - 1] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        bytesToRead = 8;
        //                serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, data.Length - 6);

        if (debug)
        {
          byte[] debugData = new byte[data.Length - 6];
          Array.Copy(data, 6, debugData, 0, data.Length - 6);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[data.Length - 6];
          Array.Copy(data, 6, sendData, 0, data.Length - 6);
          sendDataChanged(this);

        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;
        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
        }
        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);
          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x90 & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x90 & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x90 & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x90 & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      if (serialport != null)
      {
        crc = BitConverter.GetBytes(calculateCRC(data, 6, 6));
        if ((crc[0] != data[12] | crc[1] != data[13]) & dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "CRCCheckFailedException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new EasyModbus.Exceptions.CRCCheckFailedException("Response CRC check failed");
          }
          else
          {
            countRetries++;
            WriteMultipleRegisters(startingAddress, values);
          }
        }
        else if (!dataReceived)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "TimeoutException Throwed"+ System.Environment.NewLine));
          if (NumberOfRetries <= countRetries)
          {
            countRetries = 0;
            throw new TimeoutException("No Response from Modbus Slave");

          }
          else
          {
            countRetries++;
            WriteMultipleRegisters(startingAddress, values);
          }
        }
      }
    }

    /// <summary>
    /// Read/Write Multiple Registers (FC23).
    /// </summary>
    /// <param name="startingAddressRead">First input register to read</param>
    /// <param name="quantityRead">Number of input registers to read</param>
    /// <param name="startingAddressWrite">First input register to write</param>
    /// <param name="values">Values to write</param>
    /// <returns>Int Array which contains the Holding registers</returns>
    public int[] ReadWriteMultipleRegisters(int startingAddressRead, int quantityRead, int startingAddressWrite, int[] values)
    {

      string debugString = "";
      for (int i = 0; i < values.Length; i++)
        debugString = debugString + values[i] + " ";
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FC23 (Read and Write multiple Registers to Server device), StartingAddress Read: " + startingAddressRead + ", Quantity Read: " + quantityRead + ", startingAddressWrite: " + startingAddressWrite + ", Values: " + debugString+ System.Environment.NewLine));
      transactionIdentifierInternal++;
      byte[] startingAddressReadLocal = new byte[2];
      byte[] quantityReadLocal = new byte[2];
      byte[] startingAddressWriteLocal = new byte[2];
      byte[] quantityWriteLocal = new byte[2];
      byte writeByteCountLocal = 0;
      if (serialport != null)
        if (!serialport.IsOpen)
        {
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "SerialPortNotOpenedException Throwed"+ System.Environment.NewLine));
          throw new EasyModbus.Exceptions.SerialPortNotOpenedException("serial port not opened");
        }
      if (tcpClient == null & !udpFlag & serialport == null)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ConnectionException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ConnectionException("connection error");
      }
      if (startingAddressRead > 65535 | quantityRead > 125 | startingAddressWrite > 65535 | values.Length > 121)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ArgumentException Throwed"+ System.Environment.NewLine));
        throw new ArgumentException("Starting address must be 0 - 65535; quantity must be 0 - 2000");
      }
      int[] response;
      this.transactionIdentifier = BitConverter.GetBytes((uint)transactionIdentifierInternal);
      this.protocolIdentifier = BitConverter.GetBytes((int)0x0000);
      this.length = BitConverter.GetBytes((int)0x0006);
      this.functionCode = 0x17;
      startingAddressReadLocal = BitConverter.GetBytes(startingAddressRead);
      quantityReadLocal = BitConverter.GetBytes(quantityRead);
      startingAddressWriteLocal = BitConverter.GetBytes(startingAddressWrite);
      quantityWriteLocal = BitConverter.GetBytes(values.Length);
      writeByteCountLocal = Convert.ToByte(values.Length * 2);
      Byte[] data = new byte[17 + 2 + values.Length * 2];
      data[0] = this.transactionIdentifier[1];
      data[1] = this.transactionIdentifier[0];
      data[2] = this.protocolIdentifier[1];
      data[3] = this.protocolIdentifier[0];
      data[4] = this.length[1];
      data[5] = this.length[0];
      data[6] = this.unitIdentifier;
      data[7] = this.functionCode;
      data[8] = startingAddressReadLocal[1];
      data[9] = startingAddressReadLocal[0];
      data[10] = quantityReadLocal[1];
      data[11] = quantityReadLocal[0];
      data[12] = startingAddressWriteLocal[1];
      data[13] = startingAddressWriteLocal[0];
      data[14] = quantityWriteLocal[1];
      data[15] = quantityWriteLocal[0];
      data[16] = writeByteCountLocal;

      for (int i = 0; i < values.Length; i++)
      {
        byte[] singleRegisterValue = BitConverter.GetBytes((int)values[i]);
        data[17 + i * 2] = singleRegisterValue[1];
        data[18 + i * 2] = singleRegisterValue[0];
      }
      crc = BitConverter.GetBytes(calculateCRC(data, (ushort)(data.Length - 8), 6));
      data[data.Length - 2] = crc[0];
      data[data.Length - 1] = crc[1];
      if (serialport != null)
      {
        dataReceived = false;
        bytesToRead = 5 + 2 * quantityRead;
        //               serialport.ReceivedBytesThreshold = bytesToRead;
        serialport.Write(data, 6, data.Length - 6);
        if (debug)
        {
          byte[] debugData = new byte[data.Length - 6];
          Array.Copy(data, 6, debugData, 0, data.Length - 6);
          LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send Serial-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
        }
        if (sendDataChanged != null)
        {
          sendData = new byte[data.Length - 6];
          Array.Copy(data, 6, sendData, 0, data.Length - 6);
          sendDataChanged(this);
        }
        data = new byte[2100];
        readBuffer = new byte[256];
        DateTime dateTimeSend = DateTime.Now;
        byte receivedUnitIdentifier = 0xFF;
        while (receivedUnitIdentifier != this.unitIdentifier & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
        {
          while (dataReceived == false & !((DateTime.Now.Ticks - dateTimeSend.Ticks) > TimeSpan.TicksPerMillisecond * this.connectTimeout))
            System.Threading.Thread.Sleep(1);
          data = new byte[2100];
          Array.Copy(readBuffer, 0, data, 6, readBuffer.Length);
          receivedUnitIdentifier = data[6];
        }
        if (receivedUnitIdentifier != this.unitIdentifier)
          data = new byte[2100];
        else
          countRetries = 0;
      }
      else if (tcpClient.Client.Connected | udpFlag)
      {
        if (udpFlag)
        {
          UdpClient udpClient = new UdpClient();
          IPEndPoint endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), port);
          udpClient.Send(data, data.Length - 2, endPoint);
          portOut = ((IPEndPoint)udpClient.Client.LocalEndPoint).Port;
          udpClient.Client.ReceiveTimeout = 5000;
          endPoint = new IPEndPoint(System.Net.IPAddress.Parse(ipAddress), portOut);
          data = udpClient.Receive(ref endPoint);
        }
        else
        {
          stream.Write(data, 0, data.Length - 2);
          if (debug)
          {
            byte[] debugData = new byte[data.Length - 2];
            Array.Copy(data, 0, debugData, 0, data.Length - 2);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Send ModbusTCP-Data: " + BitConverter.ToString(debugData)+ System.Environment.NewLine));
          }
          if (sendDataChanged != null)
          {
            sendData = new byte[data.Length - 2];
            Array.Copy(data, 0, sendData, 0, data.Length - 2);
            sendDataChanged(this);

          }
          data = new Byte[2100];
          int NumberOfBytes = stream.Read(data, 0, data.Length);
          if (receiveDataChanged != null)
          {
            receiveData = new byte[NumberOfBytes];
            Array.Copy(data, 0, receiveData, 0, NumberOfBytes);
            LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Receive ModbusTCP-Data: " + BitConverter.ToString(receiveData)+ System.Environment.NewLine));
            receiveDataChanged(this);
          }
        }
      }
      if (data[7] == 0x97 & data[8] == 0x01)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "FunctionCodeNotSupportedException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.FunctionCodeNotSupportedException("Function code not supported by master");
      }
      if (data[7] == 0x97 & data[8] == 0x02)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "StartingAddressInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.StartingAddressInvalidException("Starting address invalid or starting address + quantity invalid");
      }
      if (data[7] == 0x97 & data[8] == 0x03)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "QuantityInvalidException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.QuantityInvalidException("quantity invalid");
      }
      if (data[7] == 0x97 & data[8] == 0x04)
      {
        LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "ModbusException Throwed"+ System.Environment.NewLine));
        throw new EasyModbus.Exceptions.ModbusException("error reading");
      }
      response = new int[quantityRead];
      for (int i = 0; i < quantityRead; i++)
      {
        byte lowByte;
        byte highByte;
        highByte = data[9 + i * 2];
        lowByte = data[9 + i * 2 + 1];

        data[9 + i * 2] = lowByte;
        data[9 + i * 2 + 1] = highByte;

        response[i] = BitConverter.ToInt16(data, (9 + i * 2));
      }
      return (response);
    }

    /// <summary>
    /// Close connection to Master Device.
    /// </summary>
    public void Disconnect()
    {
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Disconnect"+ System.Environment.NewLine));
      if (serialport != null)
      {
        if (serialport.IsOpen & !this.receiveActive)
          serialport.Close();
        if (connectedChanged != null)
          connectedChanged(this);
        return;
      }
      if (stream != null)
        stream.Close();
      if (tcpClient != null)
        tcpClient.Close();
      connected = false;
      if (connectedChanged != null)
        connectedChanged(this);

    }

    /// <summary>
    /// Destructor - Close connection to Master Device.
    /// </summary>
    ~ModbusClient()
    {
      LogDataChanged(this, new LogDataArgs(DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Destructor called - automatically disconnect"+ System.Environment.NewLine));
      if (serialport != null)
      {
        if (serialport.IsOpen)
          serialport.Close();
        return;
      }
      if (tcpClient != null & !udpFlag)
      {
        if (stream != null)
          stream.Close();
        tcpClient.Close();
      }
    }

    /// <summary>
    /// Returns "TRUE" if Client is connected to Server and "FALSE" if not. In case of Modbus RTU returns if COM-Port is opened
    /// </summary>
    public bool Connected
    {
      get
      {
        if (serialport != null)
        {
          return (serialport.IsOpen);
        }

        if (udpFlag & tcpClient != null)
          return true;
        if (tcpClient == null)
          return false;
        else
        {
          return connected;

        }

      }
    }

    public bool Available(int timeout)
    {
      // Ping's the local machine.
      System.Net.NetworkInformation.Ping pingSender = new System.Net.NetworkInformation.Ping();
      IPAddress address = System.Net.IPAddress.Parse(ipAddress);

      // Create a buffer of 32 bytes of data to be transmitted.
      string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
      byte[] buffer = System.Text.Encoding.ASCII.GetBytes(data);

      // Wait 10 seconds for a reply.
      System.Net.NetworkInformation.PingReply reply = pingSender.Send(address, timeout, buffer);

      if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
        return true;
      else
        return false;
    }

    /// <summary>
    /// Gets or Sets the IP-Address of the Server.
    /// </summary>
    public string IPAddress
    {
      get
      {
        return ipAddress;
      }
      set
      {
        ipAddress = value;
      }
    }

    /// <summary>
    /// Gets or Sets the Port were the Modbus-TCP Server is reachable (Standard is 502).
    /// </summary>
    public int Port
    {
      get
      {
        return port;
      }
      set
      {
        port = value;
      }
    }

    /// <summary>
    /// Gets or Sets the UDP-Flag to activate Modbus UDP.
    /// </summary>
    public bool UDPFlag
    {
      get
      {
        return udpFlag;
      }
      set
      {
        udpFlag = value;
      }
    }

    /// <summary>
    /// Gets or Sets the Unit identifier in case of serial connection (Default = 0)
    /// </summary>
    public byte UnitIdentifier
    {
      get
      {
        return unitIdentifier;
      }
      set
      {
        unitIdentifier = value;
      }
    }


    /// <summary>
    /// Gets or Sets the Baudrate for serial connection (Default = 9600)
    /// </summary>
    public int Baudrate
    {
      get
      {
        return baudRate;
      }
      set
      {
        baudRate = value;
      }
    }

    /// <summary>
    /// Gets or Sets the of Parity in case of serial connection
    /// </summary>
    public Parity Parity
    {
      get
      {
        if (serialport != null)
          return parity;
        else
          return Parity.None;
      }
      set
      {
        if (serialport != null)
          parity = value;
      }
    }


    /// <summary>
    /// Gets or Sets the number of stopbits in case of serial connection
    /// </summary>
    public StopBits StopBits
    {
      get
      {
        if (serialport != null)
          return stopBits;
        else
          return StopBits.One;
      }
      set
      {
        if (serialport != null)
          stopBits = value;
      }
    }

    /// <summary>
    /// Gets or Sets the connection Timeout in case of ModbusTCP connection
    /// </summary>
    public int ConnectionTimeout
    {
      get
      {
        return connectTimeout;
      }
      set
      {
        connectTimeout = value;
      }
    }

    /// <summary>
    /// Gets or Sets the serial Port
    /// </summary>
    public string SerialPort
    {
      get
      {

        return serialport.PortName;
      }
      set
      {
        if (value == null)
        {
          serialport = null;
          return;
        }
        if (serialport != null)
          serialport.Close();
        this.serialport = new SerialPort();
        this.serialport.PortName = value;
        serialport.BaudRate = baudRate;
        serialport.Parity = parity;
        serialport.StopBits = stopBits;
        serialport.WriteTimeout = 10000;
        serialport.ReadTimeout = connectTimeout;
        serialport.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
      }
    }
  }
}
