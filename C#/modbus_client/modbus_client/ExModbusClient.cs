using System;
using System.Collections.Generic;
using System.CodeDom;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyModbus;

public class ReadDataArgs
{
	public ReadDataArgs(UInt16[] __data) { _data = __data; }
	public UInt16[] _data { get; private set; }
}

public class ReadDataBArgs
{
	public ReadDataBArgs(bool[] __data) { _data = __data; }
	public bool[] _data { get; private set; }
}

public class RecDataArgs
{
	public RecDataArgs(string __data) { _data = __data; }
	public string _data { get; private set; }
}

public class SendDataArgs
{
	public SendDataArgs(string __data) { _data = __data; }
	public string _data { get; private set; }
}

public class LogDataTextArgs
{
  public LogDataTextArgs(string __data) { _data = __data; }
  public string _data { get; private set; }
}

namespace modbus_client
{
	public class ExModbusClient
	{
		ModbusClient modbusClient;

		private byte unitIdentifier;
		private byte selectfunc;
		public string serport;
		public int Baudrate;
		public bool Connected;
		public int startAddress;
		public int Quantity;
    public UInt16 ScanRate=1000;
		public bool LockQuantity;
		//readData ReadData;
		System.Timers.Timer timer;
		public System.IO.Ports.Parity Parity;
		public System.IO.Ports.StopBits StopBits;

		public byte UnitIdentifier { get => unitIdentifier; set => unitIdentifier = value; }
		public byte Selectfunc { get => selectfunc; set => selectfunc = value; }

		public delegate void EventChangeData(object sender, ReadDataArgs e);
		public event EventChangeData ChangeData;

		public delegate void EventChangeDataB(object sender, ReadDataBArgs e);
		public event EventChangeDataB ChangeDataB;

		public delegate void EventRecData(object sender, RecDataArgs e);
		public event EventRecData RecData;

		public delegate void EventSendData(object sender, SendDataArgs e);
		public event EventSendData SendData;

    public delegate void EventLogData(object sender, LogDataTextArgs e);
    public event EventLogData LogDataText;

    public ExModbusClient(string _serport)
		{
			serport = _serport;
			timer = new System.Timers.Timer {
				Enabled = false,
				Interval = ScanRate,
			};
			timer.Elapsed += OnTimedEvent;
		}

		string receiveData = "";
		void UpdateReceiveData(object sender)
		{
      receiveData = DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Rx: " + BitConverter.ToString(modbusClient.receiveData).Replace("-", " ") + System.Environment.NewLine;
			RecData(this, new RecDataArgs(receiveData));
		}

		string sendData = "";
		void UpdateSendData(object sender)
		{
      sendData = DateTime.Now.ToString("HH:mm:ss:fff") + " : " + "Tx: " + BitConverter.ToString(modbusClient.sendData).Replace("-", " ") + System.Environment.NewLine;
			SendData(this, new SendDataArgs(sendData));
		}

    string LogData = "";

    void UpdateLogData(object sender, LogDataArgs e)
    {
      LogData = e._data;
      LogDataText(this, new LogDataTextArgs(LogData));
    }


    //Поверочный комментарий
    public void Connect()
		{
			if (serport.Contains("COM"))
			{
				if (modbusClient == null) modbusClient = new ModbusClient(serport);
				modbusClient.receiveDataChanged += new ModbusClient.ReceiveDataChanged(UpdateReceiveData);
				modbusClient.sendDataChanged += new ModbusClient.SendDataChanged(UpdateSendData);
        modbusClient.LogDataChanged += new ModbusClient.LogChanged(UpdateLogData);
        modbusClient.Baudrate = Baudrate;
				modbusClient.Parity = Parity;
				modbusClient.StopBits = StopBits;
				modbusClient.NumberOfRetries = 3;
				modbusClient.Connect();
				Connected = modbusClient.Connected;
			}
		}

		public void Disconnect()
		{
			if (modbusClient != null)
			{
				modbusClient.Disconnect();
        Connected = modbusClient.Connected;
				modbusClient=null;
			}
		}

		public void Start()
		{
      modbusClient.UnitIdentifier = UnitIdentifier;
      timer.Interval = ScanRate;
      timer.Enabled = true;
		}

		public void Stop()
		{
			timer.Enabled = false;
		}

		private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
		{
			if (LockQuantity) return;
			switch (selectfunc)
			{
				case 1:
          modbusClient.UnitIdentifier = UnitIdentifier;
          ChangeDataB(this, new ReadDataBArgs(modbusClient.ReadCoils(startAddress, Quantity)));
					break;
				case 2:
          modbusClient.UnitIdentifier = UnitIdentifier;
          ChangeDataB(this, new ReadDataBArgs(modbusClient.ReadDiscreteInputs(startAddress, Quantity)));
					break;
				case 3:
          modbusClient.UnitIdentifier = UnitIdentifier;
          ChangeData(this, new ReadDataArgs(modbusClient.ReadHoldingRegisters(startAddress, Quantity)));
					break;
				case 4:
          modbusClient.UnitIdentifier = UnitIdentifier;
          ChangeData(this, new ReadDataArgs(modbusClient.ReadInputRegisters(startAddress, Quantity)));
					break;
			}
			
		}
	}
}
