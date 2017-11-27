using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.IO.Ports;
using System.Drawing;
using System.Threading;
using System.Net;

namespace modbus_client
{

  public partial class Form1 : Form
  {
    ExModbusClient modbusClient;
    public int requestcounter = 0;
    public UInt16[] hr;
    public bool[] ic;
    public IPAddress iPAddress;
    public byte UnitId;
    public UInt16 ScanR;
    public string[] bd = { "300", "600", "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200", "230400", "460800", "921600" };
    public string[] pcb = { "Even", "Mark", "None", "Odd", "Space" };
    public string[] stopbcb = { "One", "OnePointFive", "Two" };
    public string[] customviewenum = { "HEX", "UInt16", "Int16", "UInt32", "Int32", "Float" };
    public string[] orderByte = { "ABCD", "ABDC", "ACBD", "ACDB", "ADBC", "ADCB", "BACD", "BADC", "BCAD", "BCDA", "BDAC", "BDCA", "CABD", "CADB", "CBAD", "CBDA", "CDAB", "CDBA", "DABC", "DACB", "DBAC", "DBCA", "DCAB", "DCBA" };
    public string[] selectfuncstrcb = { "01 Read Coils", "02 Read Discrete Inputs", "03 Read Holding Registers", "04 Read Input Registers" };
    public string customview;
    public string orderbyte;
    public int RowCount;
    string LogRec;
    string LogSend;
    string LogText;

    public Form1()
    {
      InitializeComponent();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
      string[] portnames = SerialPort.GetPortNames();
      customview = "HEX";
      orderbyte = "ABCD";
      SerialPort sp;

      foreach (string portname in portnames)
      {
        sp = new SerialPort(portname);
        if (!sp.IsOpen)
        {
          comboBox1.Items.Add(portname);
        }
      }
      
      comboBox2.Items.AddRange(bd);
      comboBox3.Items.AddRange(pcb);
      comboBox4.Items.AddRange(stopbcb);
      comboBox5.Items.AddRange(customviewenum);
      comboBox6.Items.AddRange(orderByte);
      comboBox7.Items.AddRange(selectfuncstrcb);

      dataGridView1.ScrollBars = ScrollBars.Vertical | ScrollBars.Horizontal;
      dataGridView1.Font = new Font("Consolas", 14F, FontStyle.Regular, GraphicsUnit.Point, 204);
      maskedTextBox1.ValidatingType = typeof(UInt16);
      maskedTextBox2.ValidatingType = typeof(byte);
      maskedTextBox3.ValidatingType = typeof(UInt16);
      maskedTextBox4.ValidatingType = typeof(byte);
      maskedTextBox5.ValidatingType = typeof(IPAddress);

      dataGridView1.Columns.Add(new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "Time",
        CellTemplate = new DataGridViewTextBoxCell(),
        Width = (TextRenderer.MeasureText("00:00:00:000", dataGridView1.Font)).Width + 10
      });

      dataGridView1.Columns.Add(new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "Address",
        CellTemplate = new DataGridViewTextBoxCell(),
      });
      dataGridView1.Columns.Add(dataGridViewColumn: new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "HEX",
        CellTemplate = new DataGridViewTextBoxCell(),
        Width = (TextRenderer.MeasureText("FFFF", dataGridView1.Font)).Width + 10
      });
      dataGridView1.Columns.Add(dataGridViewColumn: new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "UInt16",
        CellTemplate = new DataGridViewTextBoxCell(),
        Width = (TextRenderer.MeasureText("65535", dataGridView1.Font)).Width + 10
      });
      dataGridView1.Columns.Add(dataGridViewColumn: new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "Int16",
        CellTemplate = new DataGridViewTextBoxCell(),
        Width = (TextRenderer.MeasureText("-32768", dataGridView1.Font)).Width + 10
      });

      dataGridView1.Columns.Add(new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "Bin",
        CellTemplate = new DataGridViewTextBoxCell(),
        Width = (TextRenderer.MeasureText("0000000000000000", dataGridView1.Font)).Width + 10
      });

      dataGridView1.Columns.Add(new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "State",
        CellTemplate = new DataGridViewTextBoxCell(),
        Width = (TextRenderer.MeasureText("[Off]", dataGridView1.Font)).Width + 10
      });

      dataGridView1.Columns.Add(dataGridViewColumn: new DataGridViewColumn()
      {
        ReadOnly = false,
        Name = "Custom",
        CellTemplate = new DataGridViewTextBoxCell(),
        Width = (TextRenderer.MeasureText("0000000000000000", dataGridView1.Font)).Width + 10,
      });

      dataGridView1.Columns["Bin"].Visible = false;
      dataGridView1.Columns["HEX"].Visible = false;
      dataGridView1.Columns["UInt16"].Visible = false;
      dataGridView1.Columns["Int16"].Visible = false;
      dataGridView1.Columns["State"].Visible = false;
      dataGridView1.Columns["Custom"].Visible = false;

      System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer
      {
        Interval = 250,
        Enabled = true
      };

      timer.Tick += formUpdate;
      //dataGridView1.Width = wdgw + 45;
    }

    private void formUpdate(Object myObject, EventArgs myEventArgs)
    {
      dataGridView1.Update();
    }

    void SaveLogRec(object sender, RecDataArgs e)
    {
      LogRec = e._data;
      Thread thread = new Thread(updateReceiveTextBox);
      thread.Start();
    }

    void SaveLogSend(object sender, SendDataArgs e)
    {
      LogSend = e._data;
      Thread thread = new Thread(updateSendTextBox);
      thread.Start();
    }

    void LogDataTextBox(object sender, LogDataTextArgs e)
    {
      LogText = e._data;
      Thread thread = new Thread(updateLogTextBox);
      thread.Start();
    }

    delegate void UpdateLogTextBoxCallback();
    void updateLogTextBox()
    {
      if (richTextBox2.InvokeRequired)
      {
        UpdateLogTextBoxCallback d = new UpdateLogTextBoxCallback(updateLogTextBox);
        this.Invoke(d, new object[] { });
      }
      else
      {
        richTextBox2.AppendText(LogText);
      }
    }

    delegate void UpdateReceiveDataCallback();
    void updateReceiveTextBox()
    {
      if (richTextBox1.InvokeRequired)
      {
        UpdateReceiveDataCallback d = new UpdateReceiveDataCallback(updateReceiveTextBox);
        this.Invoke(d, new object[] { });
      }
      else
      {
        richTextBox1.SelectionStart = richTextBox1.TextLength;
        richTextBox1.SelectionLength = 0;
        richTextBox1.SelectionColor  = Color.DarkGreen;
        richTextBox1.AppendText(LogRec);
        richTextBox1.SelectionColor = richTextBox1.ForeColor;
      }
    }

    delegate void UpdateSendDataCallback();
    void updateSendTextBox()
    {
      if (richTextBox1.InvokeRequired)
      {
        UpdateSendDataCallback d = new UpdateSendDataCallback(updateSendTextBox);
        this.Invoke(d, new object[] { });
      }
      else
      {
        richTextBox1.SelectionStart = richTextBox1.TextLength;
        richTextBox1.SelectionLength = 0;
        richTextBox1.SelectionColor = Color.DarkBlue;
        richTextBox1.AppendText(LogSend);
        richTextBox1.SelectionColor = richTextBox1.ForeColor;
      }
    }

    delegate void UpdateRowCountCallback();
    void updateRowCount()
    {
      if (dataGridView1.InvokeRequired)
      {
        UpdateRowCountCallback d = new UpdateRowCountCallback(updateSendTextBox);
        this.Invoke(d, new object[] { });
      }
      else
      {
        dataGridView1.RowCount = RowCount;
      }
    }


    private void RecieveDataB(object sender, ReadDataBArgs e)
    {
      if (modbusClient.LockQuantity) return;
      int s = modbusClient.startAddress;
      byte selectfunc = modbusClient.Selectfunc;
      ic = e._data;
      requestcounter++;

      for (int i = 0; i < RowCount; i++)
      {
        try
        {
          if (!(i > ic.Length - 1))
          { 
            dataGridView1["Time", i].Value = DateTime.Now.ToString("HH:mm:ss:fff").ToString();
            dataGridView1["Address", i].Value = (s + i).ToString();
            dataGridView1["State", i].Value = (ic[i]) ? "[On]" : "[Off]";
            dataGridView1["State", i].Style.BackColor = (ic[i]) ? Color.Red : Color.Green;
          }
        }
        catch
        {
          updateRowCount();
        }
      }
    }


    private void RecieveData(object sender, ReadDataArgs e)
    {
      if (modbusClient.LockQuantity) return;
      int s = modbusClient.startAddress;
      byte selectfunc = modbusClient.Selectfunc;
      hr = e._data;
      requestcounter++;

      for (int i = 0; i < RowCount; i++)
      {

        Dictionary<byte, byte> hexstrABCD = new Dictionary<byte, byte>();

        try
        {
          if (!(i > hr.Length - 1))
          {
            dataGridView1["Time", i].Value = DateTime.Now.ToString("HH:mm:ss:fff").ToString();
            dataGridView1["Address", i].Value = (s + i).ToString();
            dataGridView1["HEX", i].Value = hr[i].ToString("X4");
            dataGridView1["UInt16", i].Value = hr[i].ToString();
            dataGridView1["Int16", i].Value = ((Int16)hr[i]).ToString();
            dataGridView1["Bin", i].Value = Convert.ToString(hr[i], 2).PadLeft(16, paddingChar: '0');
          }
        }
        catch
        {
          updateRowCount();
        }

        if (checkBox1.Checked)
        {
          if (i % 2 == 0 && !(i > hr.Length - 1))
          {

            hexstrABCD.Add(66, (byte)((hr[i] & 0xFF00) >> 8));//A
            hexstrABCD.Add(65, (byte)(hr[i] & 0x00FF));//B

            if ((i + 1) < RowCount)
            {
              hexstrABCD.Add(68, (byte)((hr[i + 1] & 0xFF00) >> 8));//C
              hexstrABCD.Add(67, (byte)(hr[i + 1] & 0x00FF));//D
            }
            else
            {
              hexstrABCD.Add(68, 0);//C
              hexstrABCD.Add(67, 0);//D
            }

            byte[] customviewValue = new byte[4];

            int indexCustom = 0;
            if (orderbyte == "") orderbyte = "ABCD";
            foreach (byte ob in orderbyte.ToCharArray())
            {
              if ((ob == 'A' || ob == 'B' || ob == 'C' || ob == 'D'))
              {
                customviewValue[indexCustom] = hexstrABCD[ob];
                indexCustom++;
              }
            }
            //Array.Reverse(customviewValue, 0, 4);

            string customValue = "";
            switch (customview)
            {
              case "HEX":
                UInt32 vvcHEX = 0;
                if (customviewValue.Length > 3)
                {
                  try { vvcHEX = BitConverter.ToUInt32(customviewValue, 0); } catch { vvcHEX = 0; }
                }
                else
                {
                  try { vvcHEX = BitConverter.ToUInt16(customviewValue, 0); } catch { vvcHEX = 0; }
                }
                customValue = vvcHEX.ToString("X8");
                break;

              case "UInt16":
                UInt16 vvcUInt16 = 0;
                try { vvcUInt16 = BitConverter.ToUInt16(customviewValue, 0); } catch { vvcUInt16 = 0; }
                customValue = vvcUInt16.ToString();
                break;

              case "Int16":
                Int16 vvcInt16 = 0;
                try { vvcInt16 = BitConverter.ToInt16(customviewValue, 0); } catch { vvcInt16 = 0; }
                customValue = vvcInt16.ToString();
                break;

              case "Int32":
                Int32 vvcInt32 = 0;
                try { vvcInt32 = BitConverter.ToInt32(customviewValue, 0); } catch { vvcInt32 = 0; }
                customValue = vvcInt32.ToString();
                break;

              case "UInt32":
                UInt32 vvcUInt32 = 0;
                try { vvcUInt32 = BitConverter.ToUInt32(customviewValue, 0); } catch { vvcUInt32 = 0; }
                customValue = vvcUInt32.ToString();
                break;

              case "Float":
                float vvcFloat = 0.0F;
                try { vvcFloat = BitConverter.ToSingle(customviewValue, 0); } catch { vvcFloat = 0.0F; }
                customValue = vvcFloat.ToString();
                break;

              default:
                UInt32 vvcD = 0;
                try { vvcD = BitConverter.ToUInt32(customviewValue, 0); } catch { vvcD = 0; }
                customValue = vvcD.ToString("X8");
                break;
            }
            dataGridView1["Custom", i].Value = customValue;
          }
        }
        else
        {
          dataGridView1.Columns["Custom"].Visible = false;
        }
      }
    }

    private void button1_Click(object sender, EventArgs e)
    {
      string comname = comboBox1.Text;
      string paritycb = comboBox3.Text;
      string stopbcb = comboBox4.Text;

			panel1.Enabled = false;
			panel2.Enabled = false;
			panel3.Enabled = false;

			if (modbusClient == null)
      {
        modbusClient = new ExModbusClient(comname);

        int combd = 9600;
        try { combd = int.Parse(comboBox2.Text); }
        catch { combd = 9600; comboBox2.Text = "9600"; };
        modbusClient.Baudrate = combd;

        try
        {
          modbusClient.Parity = (Parity)Enum.Parse(typeof(Parity), paritycb);
        }
        catch
        {
          paritycb = "None";
          modbusClient.Parity = (Parity)Enum.Parse(typeof(Parity), paritycb);
          comboBox3.Text = paritycb;
        }

        try
        {
          modbusClient.StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopbcb);
        }
        catch
        {
          stopbcb = "One";
          modbusClient.StopBits = (StopBits)Enum.Parse(typeof(StopBits), stopbcb);
          comboBox4.Text = stopbcb;
        }
        modbusClient.LogDataText += LogDataTextBox;
        modbusClient.Connect();
      }
    }

    private void button2_Click(object sender, EventArgs e)
    {
      if (modbusClient != null && modbusClient.Connected)
      {
        modbusClient.Disconnect();
        modbusClient = null;
      }
			panel1.Enabled = true;
			if (radioButton1.Checked)
			{
				panel2.Enabled = false;
				panel3.Enabled = true;
			}
			else
			{
				panel2.Enabled = true;
				panel3.Enabled = false;
			}
		}

		private void button4_Click(object sender, EventArgs e)
    {
      if (modbusClient != null)
      {
        if (modbusClient.Connected)
        {
          modbusClient.ChangeData += RecieveData;
          modbusClient.ChangeDataB += RecieveDataB;

          modbusClient.RecData += SaveLogRec;
          modbusClient.SendData += SaveLogSend;

          string UnitIdstr = maskedTextBox4.Text;
          string selectfuncstr = comboBox7.Text;
          byte selectfunc = 3;
          string ScanRstr = maskedTextBox3.Text;

          try
          {
            selectfunc = byte.Parse(selectfuncstr.Substring(0, 2));
            modbusClient.Selectfunc = selectfunc;
          }
          catch
          {
            selectfunc = 3;
            modbusClient.Selectfunc = selectfunc;
          }

          try
          {
            UnitId = byte.Parse(UnitIdstr);
            modbusClient.UnitIdentifier = UnitId;
          }
          catch
          {
            UnitId = 1;
            modbusClient.UnitIdentifier = UnitId;
            maskedTextBox4.Text = UnitId.ToString();
          }

          UInt16 s;
          try
          {
            s = UInt16.Parse(maskedTextBox1.Text);
          }
          catch
          {
            s = 0;
            maskedTextBox1.Text = s.ToString();
          }
          modbusClient.startAddress = s;

          byte q;

          try
          {
            q = byte.Parse(maskedTextBox2.Text);
            if (q > 124) { q = 124; }
          }
          catch
          {
            q = 1;
            maskedTextBox2.Text = q.ToString();
          }

          try
          {
            ScanR = UInt16.Parse(ScanRstr);
            if (ScanR < 250)
            {
              ScanR = 250;
            }
            else if (ScanR > 2500)
            {
              ScanR = 2500;
            }
          }
          catch
          {
            ScanR = 1000;
          }
          maskedTextBox3.Text = ScanR.ToString();
          modbusClient.ScanRate = ScanR;


          modbusClient.Quantity = q;

          dataGridView1.RowCount = q;
          modbusClient.Start();
        }
      }
    }

    private void button3_Click(object sender, EventArgs e)
    {
      if (modbusClient != null)
      {
        if (modbusClient.Connected)
        {
          modbusClient.Stop();
          modbusClient.ChangeData -= RecieveData;
          modbusClient.ChangeDataB -= RecieveDataB;

          modbusClient.RecData -= SaveLogRec;
          modbusClient.SendData -= SaveLogSend;
        }
      }
    }

    private void checkBox1_CheckedChanged(object sender, EventArgs e)
    {
      customview = comboBox5.Text;
      orderbyte = comboBox6.Text;
      dataGridView1.Columns["Custom"].Visible = ((CheckBox)sender).Checked;
    }

    private void comboBox5_TextChanged(object sender, EventArgs e)
    {
      customview = comboBox5.Text;
    }

    private void comboBox6_TextChanged(object sender, EventArgs e)
    {
      orderbyte = comboBox6.Text;
    }

    private void maskedTextBox4_TextChanged(object sender, EventArgs e)
    {
      string UnitIdstr = maskedTextBox4.Text;
      try
      {
        UnitId = byte.Parse(UnitIdstr);
      }
      catch
      {
        UnitId = 1;
        maskedTextBox4.Text = UnitId.ToString();
      }
      modbusClient.UnitIdentifier = UnitId;
    }


    private void maskedTextBox3_TextChanged(object sender, EventArgs e)
    {
      string ScanRstr = maskedTextBox3.Text;
      try
      {
        ScanR = UInt16.Parse(ScanRstr);
      }
      catch
      {
        ScanRstr = "1000";
        
      }
      if (modbusClient != null) modbusClient.ScanRate = UInt16.Parse(ScanRstr);
      maskedTextBox3.Text = ScanRstr;
    }



    private void comboBox7_TextChanged(object sender, EventArgs e)
    {
      string selectfuncstr = comboBox7.Text;
      byte selectfunc = 3;
      try
      {
        selectfunc = byte.Parse(selectfuncstr.Substring(0, 2));
        if (modbusClient != null) modbusClient.Selectfunc = selectfunc;
      }
      catch
      {
        selectfunc = 3;
        if (modbusClient != null) modbusClient.Selectfunc = selectfunc;
      }
      if (selectfunc == 1 || selectfunc == 2)
      {
        dataGridView1.Columns["Bin"].Visible = false;
        dataGridView1.Columns["HEX"].Visible = false;
        dataGridView1.Columns["UInt16"].Visible = false;
        dataGridView1.Columns["Int16"].Visible = false;
        dataGridView1.Columns["State"].Visible = true;

      }
      else
      {
        dataGridView1.Columns["Bin"].Visible = true;
        dataGridView1.Columns["HEX"].Visible = true;
        dataGridView1.Columns["UInt16"].Visible = true;
        dataGridView1.Columns["Int16"].Visible = true;
        dataGridView1.Columns["State"].Visible = false;

      }
    }

    private void maskedTextBox1_TextChanged(object sender, EventArgs e)
    {
      UInt16 s;
      try
      {
        s = UInt16.Parse(maskedTextBox1.Text);
      }
      catch
      {
        s = 0;
        maskedTextBox1.Text = s.ToString();
      }
      if (modbusClient != null) modbusClient.startAddress = s;
    }

    private void maskedTextBox2_TextChanged(object sender, EventArgs e)
    {
      if (modbusClient != null) modbusClient.LockQuantity = true;

      try
      {
        RowCount = byte.Parse(maskedTextBox2.Text);
        if (RowCount > 124) { RowCount = 124; }
      }
      catch
      {
        RowCount = 1;
        maskedTextBox2.Text = RowCount.ToString();
      }
      if (modbusClient != null)
      {
        modbusClient.Quantity = RowCount;
        dataGridView1.RowCount = RowCount;
        modbusClient.LockQuantity = false;
      }
      else { dataGridView1.RowCount = RowCount; }
    }

		private void radioButton1_CheckedChanged(object sender, EventArgs e)
		{
			if (radioButton1.Checked)
			{
				panel2.Enabled = false;
				panel3.Enabled = true;
			}
			else
			{
				panel2.Enabled = true;
				panel3.Enabled = false;
			}
		}
	}
}
