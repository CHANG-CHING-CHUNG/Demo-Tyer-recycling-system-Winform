using AForge.Video;
using AForge.Video.DirectShow;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Forms;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;

namespace TyerRecycle
{
	public partial class tyerRecycle : Form
	{
		//攝像鏡頭設定值
		private FilterInfoCollection videoDevices;
		private VideoCaptureDevice videoDevice;
		private VideoCapabilities[] snapshotCapabilities;
		private ArrayList listCamera = new ArrayList();
		public string pathFolder = Application.StartupPath+@"\RecycleImage\";
		private string imageFileNamePath = "";
		string nameCapture = "";
		private Stopwatch stopWatch = null;
		private static bool needSnapshot = false;
		List<string> pic = new List<string>();

		//藍芽磅秤設定值
		bool receiving = false;

		//設定 Api 網址
		//private string apiUrl = "https://tyer.idwteam.tk/api/";
		private string apiUrl = "http://192.168.1.201:81/api/";

		public tyerRecycle()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			//設定密碼顯示*號
			textBoxLoginPassword.PasswordChar = '*';

			//顯示登入畫面
			panelLogin.Visible = true;

			//取得視訊設備裝置
			comboBoxWebcamDevice.Items.Clear();
			FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
			if(videoDevices.Count != 0)
			{
				foreach(FilterInfo device in videoDevices)
				{
					comboBoxWebcamDevice.Items.Add(device.Name);
				}
			}
			else
			{
				comboBoxWebcamDevice.Items.Add("尚未找到視訊設備");
				comboBoxWebcamDevice.SelectedIndex = 0;
			}

			comboBoxWebcamDevice.SelectedIndex = 0;

			//取得藍芽設備裝置
			comboBoxBlueToothDevice.Items.Clear();

			string[] BlueToothDevicePorts = SerialPort.GetPortNames();

			if(BlueToothDevicePorts.Length!=0)
			{
				foreach(string item in BlueToothDevicePorts)
				{
					comboBoxBlueToothDevice.Items.Add(item);
				}

				comboBoxBlueToothDevice.SelectedIndex = 1;
			}
			else
			{
				comboBoxBlueToothDevice.Items.Add("尚未找到藍芽設備");
				comboBoxBlueToothDevice.SelectedIndex = 0;
			}

			if(BlueToothDevicePorts.Length > 0)
			{
				buttonAddDevice.Visible = false;
			}

			//寫入兌換清單標頭
			dataGridViewExchangeHistory.Columns.Add("item1", "回收項目");
			dataGridViewExchangeHistory.Columns.Add("item2", "重量(數量)");
			dataGridViewExchangeHistory.Columns.Add("item3", "已獲得金額");

			//設定 datagridview 樣式
			DataGridViewColumn item1 = dataGridViewExchangeHistory.Columns[0];
			item1.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
			item1.HeaderCell.Style.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);
			item1.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);

			DataGridViewColumn item2 = dataGridViewExchangeHistory.Columns[1];
			item2.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
			item2.HeaderCell.Style.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);
			item2.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);

			DataGridViewColumn item3 = dataGridViewExchangeHistory.Columns[2];
			item3.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
			item3.HeaderCell.Style.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);
			item3.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);

			//panelSelectionExchange.Visible = true;
		}

		private async void buttonLogin_Click(object sender, EventArgs e)
		{
			ResetMsg();

			labelLoginMsg.Text = "登入驗證中...";
			labelLoginMsg.BackColor = Color.FromArgb(52, 73, 94);
			labelLoginMsg.ForeColor = Color.White;
			buttonLogin.Visible = false;

			Log(textBoxLoginAccount.Text.Trim()+" 登入中");

			string url = apiUrl+"login_station_process";

			//設定要傳送的值
			var values = new Dictionary<string, string>
			{
				{ "action", "login" },
				{ "account", textBoxLoginAccount.Text.Trim() },
				{ "password", textBoxLoginPassword.Text.Trim() }
			};

			//將要傳送的值轉換成字串
			HttpContent content = new FormUrlEncodedContent(values);

			try
			{
				//宣告使用 http 傳送資料
				HttpClient client = new HttpClient();
				HttpResponseMessage response = await client.PostAsync(url, content);

				string responseStr = await response.Content.ReadAsStringAsync();
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStr);
				if(responseJsonObj.success==true)
				{
					labelLoginMsg.Text = Convert.ToString("登入成功！畫面跳轉中！");
					labelLoginMsg.BackColor = Color.Green;
					labelLoginMsg.ForeColor = Color.White;

					Log(textBoxLoginAccount.Text.Trim() + " 登入成功！");

					textBoxSquadronId.Text = responseJsonObj.squadron_id;
					textBoxStationId.Text = responseJsonObj.station_id;

					//隱藏登入畫面
					panelLogin.Visible = false;
				}
				else
				{
					labelLoginMsg.Text = Convert.ToString(responseJsonObj.msg);
					labelLoginMsg.BackColor = Color.Red;
					labelLoginMsg.ForeColor = Color.White;

					Log(textBoxLoginAccount.Text.Trim() + " 登入失敗！原因："+responseJsonObj.msg);
				}

				buttonLogin.Visible = true;
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());

				labelLoginMsg.Text = "";
				labelLoginMsg.BackColor = Color.FromArgb(52, 73, 94);
				labelLoginMsg.ForeColor = Color.White;

				buttonLogin.Visible = true;
			}
		}

		private SerialPort blueToothPort;

		private void comboBoxBlueToothDevice_DropDown(object sender, EventArgs e)
		{
			comboBoxBlueToothDevice.Items.Clear();

			string[] BlueToothDevicePorts = SerialPort.GetPortNames();
			foreach(string item in BlueToothDevicePorts)
			{
				comboBoxBlueToothDevice.Items.Add(item);
			}
		}

		//打開 Windows 新增裝置程式
		private void buttonAddDevice_Click(object sender, EventArgs e)
		{
			try
			{
				Process p = Process.Start(@"C:\Windows\System32\DevicePairingWizard.exe");

				while(true)
				{
					if(p.HasExited)
					{
						break;
					}
				}

				comboBoxBlueToothDevice.Items.Clear();

				string[] myPorts = SerialPort.GetPortNames();
				foreach(string item in myPorts)
				{
					comboBoxBlueToothDevice.Items.Add(item);
				}
			}
			catch(Exception)
			{
			}
		}

		private void buttonOpenPort_Click(object sender, EventArgs e)
		{
			try
			{
				//設定要開啟的連接埠資料(port Name, speed, protity, stopbit)
				if(comboBoxBlueToothDevice.Text.IndexOf("COM")==0)
				{
					blueToothPort = new SerialPort();
					blueToothPort.BaudRate = 9600;
					blueToothPort.Parity = Parity.None;
					blueToothPort.PortName = comboBoxBlueToothDevice.Text;
					blueToothPort.StopBits = StopBits.One;
					blueToothPort.DataBits = 8;
					blueToothPort.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(DataReceived);
					blueToothPort.Open();
					blueToothPort.ReadTimeout = 3000;

					ShowSuccessMsg("藍芽磅秤連接埠已開啟");

					receiving = true;

					buttonOpenPort.Visible = false;
					buttonClosePort.Visible = true;
					comboBoxBlueToothDevice.Enabled = false;

					confirmDevices();

					Log("藍芽磅秤 "+comboBoxBlueToothDevice.Text+" 連接埠已開啟");
				}
				else
				{
					ShowErrorMsg("尚未選擇藍芽磅秤連接埠");
				}
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());

				Log("藍芽磅秤連接失敗。原因："+ex.ToString());
			}
		}

		private void buttonClosePort_Click(object sender, EventArgs e)
		{
			//關閉連接埠
			CloseComport(blueToothPort);

			buttonOpenPort.Visible = true;
			buttonClosePort.Visible = false;
			comboBoxBlueToothDevice.Enabled = true;
			buttonSelectionStation.Visible = false;
			buttonSaveData.Visible = false;
			buttonExchange.Visible = false;

			Log("藍芽磅秤 "+comboBoxBlueToothDevice.Text+" 連接埠已關閉");
		}

		//關閉連接埠
		private void CloseComport(SerialPort port)
		{
			try
			{
				if((port != null) &&(port.IsOpen))
				{
					port.Close();

					//提示錯誤訊息
					ShowErrorMsg("藍芽磅秤連接埠已關閉");

					receiving = false;
				}
			}
			catch(Exception ex)
			{
				//這邊你可以自訂發生例外的處理程序
				MessageBox.Show(String.Format("出問題啦:{0}", ex.ToString()));

				Log("藍芽磅秤關閉連接埠發生問題。原因："+ex.ToString());
			}
		}

		//接收傳回來的資料
		string ReceivedValue = string.Empty;
		public void DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				if(receiving)
				{
					ReceivedValue += blueToothPort.ReadExisting();
					if(ReceivedValue.IndexOf("ST,GS,+") >= 0 && ReceivedValue.IndexOf("kg\r\n") > 0 && ReceivedValue.Length >= 10)
					{
						Thread.Sleep(500);

						int index = ReceivedValue.IndexOf("kg\r\n");
						string receivedData = ReceivedValue.Substring(9, index - 9).Trim();
						UpdateUI(receivedData, textBoxScaleTotal);
					}
					else
					{
						blueToothPort.DiscardInBuffer();
					}
				}
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());

				Log("藍芽磅秤 "+comboBoxBlueToothDevice.Text+" 接收資料發生錯誤。原因："+ex.ToString());
			}
		}

		private static string _usbcamera;
		public string usbcamera
		{
			get { return _usbcamera; }
			set { _usbcamera = value; }
		}

		private void comboBoxWebcamDevice_DropDown(object sender, EventArgs e)
		{
			//取得視訊設備裝置
			comboBoxWebcamDevice.Items.Clear();
			FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
			if (videoDevices.Count != 0)
			{
				foreach (FilterInfo device in videoDevices)
				{
					comboBoxWebcamDevice.Items.Add(device.Name);
				}
			}
			else
			{
				comboBoxWebcamDevice.Items.Add("尚未找到視訊設備");
				comboBoxWebcamDevice.SelectedIndex = 0;
			}

			comboBoxWebcamDevice.SelectedIndex = 0;
		}

		private void buttonOpenCamera_Click(object sender, EventArgs e)
		{
			try
			{
				usbcamera = comboBoxWebcamDevice.SelectedIndex.ToString();
				videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
				videoDevice = new VideoCaptureDevice(videoDevices[Convert.ToInt32(usbcamera)].MonikerString);
				//snapshotCapabilities = videoDevice.SnapshotCapabilities;

				//if(snapshotCapabilities.Length==0)
				//{
				//	MessageBox.Show("視訊鏡頭不支援截圖！");
				//}

				OpenVideoSource(videoDevice);

				buttonOpenCamera.Visible = false;
				buttonCloseCamera.Visible = true;
				comboBoxWebcamDevice.Enabled = false;

				ShowSuccessMsg("攝像鏡頭連接埠已開啟");

				Log("攝像鏡頭 "+comboBoxWebcamDevice.Text+" 已開啟連接埠");

				confirmDevices();
			}
			catch(Exception ex)
			{
				//這邊你可以自訂發生例外的處理程序
				MessageBox.Show(String.Format("出問題啦:{0}", ex.ToString()));
				
				Log("攝像鏡頭 "+comboBoxWebcamDevice.Text+" 開啟連接埠發生錯誤。原因："+ex.ToString());
			}
		}

		public void OpenVideoSource(IVideoSource source)
		{
			try
			{
				//將視訊鏡頭設為忙碌狀態
				this.Cursor = Cursors.WaitCursor;

				//停止視訊串流
				CloseCurrentVideoSource();

				//開始新的視訊來源
				videoSourcePlayer1.VideoSource = source;
				videoSourcePlayer1.Start();

				//重置監看狀態
				stopWatch = null;

				this.Cursor = Cursors.Default;
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		private void buttonCloseCamera_Click(object sender, EventArgs e)
		{
			CloseCurrentVideoSource();
		}

		public void CloseCurrentVideoSource()
		{
			try
			{
				if(videoSourcePlayer1.VideoSource != null)
				{
					videoSourcePlayer1.SignalToStop();

					for(int i = 0; i < 30; i++)
					{
						if(!videoSourcePlayer1.IsRunning)
						{
							break;
							Thread.Sleep(100);
						}
					}

					if(videoSourcePlayer1.IsRunning)
					{
						videoSourcePlayer1.Stop();
					}

					videoSourcePlayer1.VideoSource = null;
				}

				buttonOpenCamera.Visible = true;
				buttonCloseCamera.Visible = false;
				comboBoxWebcamDevice.Enabled = true;
				buttonSelectionStation.Visible = false;
				buttonSaveData.Visible = false;
				buttonExchange.Visible = false;

				Log("攝像鏡頭 "+comboBoxWebcamDevice.Text+" 已關閉連接埠");
			}
			catch(Exception ex)
			{
				//這邊你可以自訂發生例外的處理程序
				MessageBox.Show(String.Format("出問題啦:{0}", ex.ToString()));

				Log("攝像鏡頭 "+comboBoxWebcamDevice.Text+" 關閉連接埠發生錯誤。原因：" + ex.ToString());
			}
		}

		private void videoSourcePlayer1_NewFrame_1(object sender, ref Bitmap image)
		{
			try
			{
				DateTime now = DateTime.Now;
				Graphics g = Graphics.FromImage(image);
				SolidBrush brush = new SolidBrush(Color.Red);
				g.DrawString(now.ToString(), this.Font, brush, new PointF(5, 5));
				brush.Dispose();

				if(needSnapshot)
				{
					this.Invoke(new CaptureSnapshotManifast(UpdateCaptureSnapshotManifast), image);
				}

				g.Dispose();
			}
			catch(Exception ex)
			{
				//這邊你可以自訂發生例外的處理程序
				MessageBox.Show(String.Format("出問題啦:{0}", ex.ToString()));
			}
		}

		//Delegate Untuk Capture, insert database, update ke grid 
		public delegate void CaptureSnapshotManifast(Bitmap image);
		public void UpdateCaptureSnapshotManifast(Bitmap image)
		{
			try
			{
				pictureBox1.Image = image;
				pictureBox1.Update();

				nameCapture = "recycleImage_"+DateTime.Now.ToString("yyyyMMddHHmmss")+".png";
				imageFileNamePath = pathFolder+nameCapture;
				if(Directory.Exists(pathFolder))
				{
					pictureBox1.Image.Save(imageFileNamePath, ImageFormat.Png);
				}
				else
				{
					Directory.CreateDirectory(pathFolder);
					pictureBox1.Image.Save(imageFileNamePath, ImageFormat.Png);
				}

				//將擷取的圖片存入暫存中
				pic.Add(imageFileNamePath);
				

				//計算回收品金額
				decimal cash = 0;
				if(textBoxScaleTotal.Text.Trim() != "" && textBoxKeyinQty.Text.Trim()=="")
				{
					cash = calcCash(Convert.ToInt32(textBoxRecycleSelectionItem.Text), Convert.ToDecimal(textBoxScaleTotal.Text.Trim()));
				}

				if(textBoxKeyinQty.Text.Trim() != "" && textBoxScaleTotal.Text.Trim()=="")
				{
					cash = calcCash(Convert.ToInt32(textBoxRecycleSelectionItem.Text), Convert.ToInt32(textBoxKeyinQty.Text.Trim()));
				}
				
				//寫入 datagridview 顯示
				string[] exchangeHistoryData = {
					textBoxSelectionName.Text,
					textBoxKeyinQty.Text.Trim()!="" ? textBoxKeyinQty.Text.Trim() : textBoxScaleTotal.Text.Trim(),
					cash.ToString()
				};
				dataGridViewExchangeHistory.Rows.Add(exchangeHistoryData);

				needSnapshot = false;
				//pictureBox1.Image = null;

				//儲存資料到資料庫
				//saveDB();
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		//確認藍芽磅秤與視訊設備已連接
		private void confirmDevices()
		{
			buttonSelectionStation.Visible = true;
			//if (blueToothPort != null && blueToothPort.IsOpen && videoSourcePlayer1.VideoSource != null)
			//{
			//	buttonSelectionStation.Visible = true;
			//}
		}

		private void buttonSelectionStation_Click(object sender, EventArgs e)
		{
			buttonSelectionStation.Visible = false;
			panelSelection.Enabled = true;
			buttonSelectRecycleItem.Visible = true;
			buttonExchange.Visible = false;
			buttonSaveData.Visible = false;
			buttonPrint.Visible = false;

			Log("點擊下一步開啟秤重");
		}

		private void buttonSelectRecycleItem_Click(object sender, EventArgs e)
		{
			panelRecycleItems.Visible = true;
		}

		private void buttonCloseRecycleItem_Click(object sender, EventArgs e)
		{
			panelRecycleItems.Visible = false;
		}

		private void buttonRecycleItem1_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("1", "紙類");
		}

		private void buttonRecycleItem2_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("2", "廢紙容器");
		}

		private void buttonRecycleItem3_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("3", "光碟片");
		}

		private void buttonRecycleItem4_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("4", "塑膠容器");
		}

		private void buttonRecycleItem5_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("5", "廢乾電池");
		}

		private void buttonRecycleItem6_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("6", "廢電風扇", false);
		}

		private void buttonRecycleItem7_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("7", "照明光源", false);
		}

		private void buttonRecycleItem8_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("8", "農藥容器", false);
		}

		private void buttonRecycleItem9_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("9", "玻璃容器");
		}

		private void buttonRecycleItem10_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("10", "廢筆記型電腦", false);
		}

		private void buttonRecycleItem11_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("11", "廢平板電腦", false);
		}

		private void buttonRecycleItem12_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("12", "鍵盤", false);
		}

		private void buttonRecycleItem13_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("13", "廢電視機", false);
		}

		private void buttonRecycleItem14_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("14", "廢冷暖氣機", false);
		}

		private void showRecycleItemInfo(string id, string name, bool scale=true)
		{
			textBoxRecycleSelectionItem.Text = id;
			textBoxSelectionName.Text = name;

			textBoxScaleTotal.Text = null;
			textBoxKeyinQty.Text = null;

			if(scale)
			{
				labelScale.Visible = true;
				textBoxScaleTotal.Visible = true;
				labelKeyinQty.Visible = false;
				textBoxKeyinQty.Visible = false;
				labelKeyinQtyNotice.Visible = false;
			}
			else
			{
				labelScale.Visible = false;
				textBoxScaleTotal.Visible = false;
				labelKeyinQty.Visible = true;
				textBoxKeyinQty.Visible = true;
				labelKeyinQtyNotice.Visible = true;
			}

			buttonSelectionStation.Visible = false;
			panelRecycleItems.Visible = false;
			buttonSaveData.Visible = true;

			ResetMsg();

			Log("點擊回收品："+name);
		}

		private void buttonSaveData_Click(object sender, EventArgs e)
		{
			if(textBoxCityCardNo.Text.Trim()=="")
			{
				ShowErrorMsg("請輸入市民卡號！");
				return;
			}

			if(CheckSpecialString(textBoxCityCardNo.Text, "市民卡號")==1)
			{
				return;
			}

			if(textBoxRecycleSelectionItem.Text.Trim()=="")
			{
				ShowErrorMsg("請選擇回收品項！");
				return;
			}

			//顯示存檔中狀態
			labelError.Text = "存檔中...請稍候";
			labelError.ForeColor = Color.Black;
			labelError.BackColor = Color.Bisque;

			Log("資料開始存檔");

			needSnapshot = true;
		}

		private void saveDB()
		{
			//設定要送出的網址
			string url = apiUrl+"saveExchange";

			var request = WebRequest.Create(url);
			request.Method = "POST";
			var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			boundary = "--" + boundary;

			var requestStream = request.GetRequestStream();

			//計算回收品金額
			decimal cash = 0;
			if(textBoxScaleTotal.Text.Trim() != "" && textBoxKeyinQty.Text.Trim()=="")
			{
				cash = calcCash(Convert.ToInt32(textBoxRecycleSelectionItem.Text), Convert.ToDecimal(textBoxScaleTotal.Text.Trim()));
			}
			if(textBoxKeyinQty.Text.Trim() != "" && textBoxScaleTotal.Text.Trim()=="")
			{
				cash = calcCash(Convert.ToInt32(textBoxRecycleSelectionItem.Text), Convert.ToInt32(textBoxKeyinQty.Text.Trim()));
			}

			//設定要傳送的值
			var values = new Dictionary<string, string>
			{
				{ "action", "insert" },
				{ "exchange_method_id", textBoxRecycleSelectionItem.Text.Trim() },
				{ "city_card_no", textBoxCityCardNo.Text.Trim() },
				{ "squadron_id", textBoxSquadronId.Text.Trim() },
				{ "station_id", textBoxStationId.Text.Trim() },
				{ "weight", textBoxScaleTotal.Text.Trim() },
				{ "qty", textBoxKeyinQty.Text.Trim() },
				{ "cash", cash.ToString() }
			};

			//寫入表單資料
			foreach(string name in values.Keys)
			{
				var buffers = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.UTF8.GetBytes(values[name] + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
			}

			try
			{
				//寫入檔案資料
				var stream1 = File.Open(imageFileNamePath, FileMode.Open);
				var buffer = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
				requestStream.Write(buffer, 0, buffer.Length);
				buffer = Encoding.UTF8.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"{2}", "pic", imageFileNamePath, Environment.NewLine));
				requestStream.Write(buffer, 0, buffer.Length);
				buffer = Encoding.ASCII.GetBytes(string.Format("Content-Type: {0}{1}{1}", "image/png", Environment.NewLine));
				requestStream.Write(buffer, 0, buffer.Length);
				stream1.CopyTo(requestStream);
				buffer = Encoding.ASCII.GetBytes(Environment.NewLine);
				requestStream.Write(buffer, 0, buffer.Length);

				var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
				requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

				var response = request.GetResponse();
				var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
				if(responseJsonObj.success==true)
				{
					ShowSuccessMsg("資料存檔完成！");

					//寫入 datagridview 顯示
					string[] exchangeHistoryData = {
						textBoxSelectionName.Text,
						textBoxKeyinQty.Text.Trim()!="" ? textBoxKeyinQty.Text.Trim() : textBoxScaleTotal.Text.Trim(),
						cash.ToString()
					};
					dataGridViewExchangeHistory.Rows.Add(exchangeHistoryData);

					Log("資料存檔完成");
				}
				else
				{
					ShowErrorMsg(Convert.ToString(responseJsonObj.msg));

					Log("資料存檔發生錯誤。原因："+responseJsonObj.msg);
				}

				request = null;

				buttonSelectionStation.Visible = false;
				buttonPrint.Visible = true;
				buttonExchange.Visible = true;
				buttonSaveData.Visible = false;

				UpdateUI(null, textBoxSelectionName);
				UpdateUI(null, textBoxScaleTotal);
				UpdateUI(null, textBoxKeyinQty);
			}
			catch(Exception ex)
			{
				ResetMsg();
				MessageBox.Show(ex.ToString());

				Log("資料存檔發生錯誤。原因："+ex.ToString());
			}
		}

		private void buttonExchange_Click(object sender, EventArgs e)
		{
			try
			{
				//Process.Start(@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", "http://tyer.idwteam.tk/manage/exchange");
				//Process.Start(@"C:\Users\bill\AppData\Local\Google\Chrome SxS\Application\chrome.exe", "http://tyer.idwteam.tk/manage/exchange");

				panelSelectionExchange.Visible = true;

				buttonSelectionStation.Visible = false;
				buttonExchange.Visible = true;
				buttonSaveData.Visible = false;
				buttonPrint.Visible = true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		//關閉兌換 panel
		private void buttonCloseSelectionExchange_Click(object sender, EventArgs e)
		{
			panelSelectionExchange.Visible = false;
		}

		//選擇加值卡片
		private void buttonSelectionEasyCard_Click(object sender, EventArgs e)
		{
			//計算可加值的兌換金額
			int totalMoney = 0;
			int usedExchangePoint = Convert.ToInt32(textBoxUsedExchangePoint.Text.Trim());
			int usedStoredValueCash = Convert.ToInt32(textBoxUsedStoredValueCash.Text.Trim());
			foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
			{
				totalMoney += Convert.ToInt32(item.Cells[2].Value);
			}

			textBoxShowTotalPrice.Text = (totalMoney - usedExchangePoint - usedStoredValueCash).ToString();

			if(Convert.ToInt32(textBoxShowTotalPrice.Text.Trim())==0)
			{
				buttonSaveStoredValue.Visible = false;
			}
			else
			{
				buttonSaveStoredValue.Visible = true;
			}

			panelStoredValueCash.Visible = true;
			panelExchangePoint.Visible = false;
		}

		//選擇兌換點數
		private void buttonSelectionPoint_Click(object sender, EventArgs e)
		{
			//計算可加值的兌換金額
			int totalMoney = 0;
			int usedExchangePoint = Convert.ToInt32(textBoxUsedExchangePoint.Text.Trim());
			int usedStoredValueCash = Convert.ToInt32(textBoxUsedStoredValueCash.Text.Trim());
			foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
			{
				totalMoney += Convert.ToInt32(item.Cells[2].Value);
			}

			textBoxShowExchangePoint.Text = (totalMoney - usedExchangePoint - usedStoredValueCash).ToString();
			
			if(Convert.ToInt32(textBoxShowExchangePoint.Text.Trim())==0)
			{
				buttonSaveExchangePoint.Visible = false;
			}
			else
			{
				buttonSaveExchangePoint.Visible = true;
			}

			panelStoredValueCash.Visible = false;
			panelExchangePoint.Visible = true;
		}

		//加值悠遊卡/一卡通
		private void buttonSaveStoredValue_Click(object sender, EventArgs e)
		{
			//驗證輸入錯誤
			if(textBoxStoredValueCash.Text.Trim()=="" || Convert.ToInt32(textBoxStoredValueCash.Text.Trim())==0 || Convert.ToInt32(textBoxStoredValueCash.Text.Trim())>500)
			{
				ShowErrorMsg("加值金額最低金額為1元\r\n且當天不可超過500元");
				return;
			}

			if(Convert.ToInt32(textBoxUsedStoredValueCash.Text.Trim())>=500)
			{
				ShowErrorMsg("已超過加值金額\r\n當天不可超過500元");
				return;
			}

			//設定要送出的網址
			string url = apiUrl+"saveEasyCard";

			var request = WebRequest.Create(url);
			request.Method = "POST";
			var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			boundary = "--" + boundary;

			var requestStream = request.GetRequestStream();

			//設定要傳送的值
			var values = new Dictionary<string, string>
			{
				{ "action", "insert" },
				{ "city_card_no", textBoxCityCardNo.Text.Trim() },
				{ "squadron_id", textBoxSquadronId.Text.Trim() },
				{ "station_id", textBoxStationId.Text.Trim() },
				{ "cash", textBoxStoredValueCash.Text.Trim() }
			};

			//寫入表單資料
			foreach(string name in values.Keys)
			{
				var buffers = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.UTF8.GetBytes(values[name] + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
			}

			try
			{
				var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
				requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

				var response = request.GetResponse();
				var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
				if(responseJsonObj.success==true)
				{
					panelStoredValueCash.Visible = false;

					ShowSuccessMsg("加值金額："+textBoxStoredValueCash.Text.Trim()+"。已加值完成！");

					Log("加值金額："+textBoxStoredValueCash.Text.Trim()+"。已加值完成");

					textBoxUsedStoredValueCash.Text = textBoxStoredValueCash.Text.Trim();
				}
				else
				{
					ShowErrorMsg(Convert.ToString(responseJsonObj.msg));

					Log("加值資料存檔發生錯誤。原因："+responseJsonObj.msg);
				}

				request = null;

				buttonSelectionStation.Visible = false;
				buttonPrint.Visible = true;
				buttonExchange.Visible = false;
				buttonSaveData.Visible = false;
				textBoxShowTotalPrice.Text = null;
				textBoxStoredValueCash.Text = null;
				panelStoredValueCash.Visible = false;
			}
			catch (Exception ex)
			{
				ResetMsg();
				MessageBox.Show(ex.ToString());

				Log("加值資料存檔發生錯誤。原因："+ex.ToString());
			}
		}

		//關閉加值悠遊卡/一卡通 panel
		private void buttonCloseStoredValue_Click(object sender, EventArgs e)
		{
			textBoxShowTotalPrice.Text = null;
			textBoxStoredValueCash.Text = null;
			panelStoredValueCash.Visible = false;
		}

		//儲存兌換點數
		private void buttonSaveExchangePoint_Click(object sender, EventArgs e)
		{
			//設定要送出的網址
			string url = apiUrl+"saveExchangePoint";

			var request = WebRequest.Create(url);
			request.Method = "POST";
			var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			boundary = "--" + boundary;

			var requestStream = request.GetRequestStream();

			//設定要傳送的值
			var values = new Dictionary<string, string>
			{
				{ "action", "insert" },
				{ "city_card_no", textBoxCityCardNo.Text.Trim() },
				{ "squadron_id", textBoxSquadronId.Text.Trim() },
				{ "station_id", textBoxStationId.Text.Trim() },
				{ "point", textBoxExchangePoint.Text.Trim() }
			};

			//寫入表單資料
			foreach(string name in values.Keys)
			{
				var buffers = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.UTF8.GetBytes(values[name] + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
			}

			try
			{
				var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
				requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

				var response = request.GetResponse();
				var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
				if(responseJsonObj.success==true)
				{
					panelStoredValueCash.Visible = false;

					ShowSuccessMsg("兌換點數："+textBoxExchangePoint.Text.Trim()+"。已兌換完成！");

					Log("兌換點數："+textBoxStoredValueCash.Text.Trim()+"。已兌換完成");

					textBoxUsedExchangePoint.Text = textBoxExchangePoint.Text.Trim();
				}
				else
				{
					ShowErrorMsg(Convert.ToString(responseJsonObj.msg));

					Log("兌換點數資料存檔發生錯誤。原因："+responseJsonObj.msg);
				}

				request = null;

				buttonSelectionStation.Visible = false;
				buttonPrint.Visible = true;
				buttonExchange.Visible = true;
				buttonSaveData.Visible = false;
				textBoxShowExchangePoint.Text = null;
				textBoxExchangePoint.Text = null;
				panelExchangePoint.Visible = false;
			}
			catch (Exception ex)
			{
				ResetMsg();
				MessageBox.Show(ex.ToString());

				Log("加值資料存檔發生錯誤。原因："+ex.ToString());
			}
		}

		//關閉兌換點數 panel
		private void buttonCloseExchangePoint_Click(object sender, EventArgs e)
		{
			textBoxShowExchangePoint.Text = null;
			textBoxExchangePoint.Text = null;
			panelExchangePoint.Visible = false;
		}

		private void buttonPrint_Click(object sender, EventArgs e)
		{
			//列印簽收單
			printDialog1.Document = printDocument1;

			if (printDialog1.ShowDialog()==DialogResult.OK)
			{
				printDocument1.Print();

				Log("開始列印簽收單");
			}

			Log("開始列印簽收單");

			buttonSelectionStation.Visible = false;
			buttonPrint.Visible = false;
			buttonExchange.Visible = false;
		}

		//簽收單內容
		private void printDocument1_PrintPage(object sender, System.Drawing.Printing.PrintPageEventArgs e)
		{
			//設定要送出的網址
			string url = apiUrl+"updateExchangeNo";

			var request = WebRequest.Create(url);
			request.Method = "POST";
			var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			boundary = "--" + boundary;

			var requestStream = request.GetRequestStream();

			//設定要傳送的值
			var values = new Dictionary<string, string>
			{
				{ "action", "update" },
				{ "city_card_no", textBoxCityCardNo.Text.Trim() },
				{ "squadron_id", textBoxSquadronId.Text.Trim() },
				{ "station_id", textBoxStationId.Text.Trim() }
			};

			//寫入表單資料
			foreach(string name in values.Keys)
			{
				var buffers = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.UTF8.GetBytes(values[name] + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
			}

			try
			{
				var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
				requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

				var response = request.GetResponse();
				var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
				if(responseJsonObj.success==true)
				{
					e.Graphics.DrawString("單號："+responseJsonObj.datas.exchangeNo, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 20);
					e.Graphics.DrawString("簽收人："+responseJsonObj.datas.user, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 40);
					e.Graphics.DrawString("站別："+responseJsonObj.datas.stationName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 60);
					e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 80);
					e.Graphics.DrawString("品項", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 100);
					e.Graphics.DrawString("重(數)量", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, 100);
					e.Graphics.DrawString("金額", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, 100);

					int pointLocationY = 120;
					foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
					{
						string itemName = "";
						if(item.Cells[0].Value.ToString().Length>4)
						{
							itemName = item.Cells[0].Value.ToString().Substring(0, 4)+"\r\n"+item.Cells[0].Value.ToString().Substring(4, item.Cells[0].Value.ToString().Length-4);
						}
						else
						{
							itemName = item.Cells[0].Value.ToString();
						}

						e.Graphics.DrawString(itemName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY);
						e.Graphics.DrawString(item.Cells[1].Value.ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, pointLocationY);
						e.Graphics.DrawString(item.Cells[2].Value.ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, pointLocationY);

						if(item.Cells[0].Value.ToString().Length>4)
						{
							pointLocationY += 40;
						}
						else
						{
							pointLocationY += 20;
						}
					}
					e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+20);
					e.Graphics.DrawString("已加值金額："+textBoxUsedStoredValueCash.Text.Trim(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+40);
					e.Graphics.DrawString("已兌換點數："+textBoxUsedExchangePoint.Text.Trim(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+60);
					e.Graphics.DrawString("簽收：________________", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+80);
					e.Graphics.DrawString(" ", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+100);

					buttonExchange.Visible = false;
					buttonSaveData.Visible = false;
					buttonPrint.Visible = false;
					panelSelectionExchange.Visible = false;

					textBoxCityCardNo.Text = null;
					textBoxSelectionName.Text = null;
					textBoxScaleTotal.Text = null;
					textBoxKeyinQty.Text = null;
					textBoxUsedExchangePoint.Text = "0";
					textBoxUsedStoredValueCash.Text = "0";
					dataGridViewExchangeHistory.Rows.Clear();
					ResetMsg();

					Log("簽收單列印完成");
				}
				else
				{
					ShowErrorMsg(Convert.ToString(responseJsonObj.msg));

					Log("簽收單列印失敗。原因："+responseJsonObj.msg);
				}

				request = null;
			}
			catch(Exception ex)
			{
				ResetMsg();
				MessageBox.Show(ex.ToString());

				Log("簽收單列印失敗。原因："+ex.ToString());
			}
		}

		//計算兌換的金額
		private int calcCash(int selectionItemId, decimal qty)
		{
			int cash = 0;

			//依不同回收項目計算
			switch(selectionItemId)
			{
				//紙類
				case 1:
				//農藥容器
				case 8:
				//鍵盤
				case 12:
					cash = 3*Convert.ToInt32(qty);
				break;

				//廢紙容器
				case 2:
					cash = 2* Convert.ToInt32(qty);
				break;

				//光碟片
				case 3:
					cash = 15*Convert.ToInt32(qty);
				break;

				//塑膠容器
				case 4:
					cash = 5*Convert.ToInt32(qty);
				break;

				//廢乾電池(含鈕扣型電池)
				case 5:
					cash = 10*Convert.ToInt32(qty);
				break;

				//廢電風扇
				case 6:
					cash = 20*Convert.ToInt32(qty);
				break;

				//照明光源
				case 7:
				//玻璃容器
				case 9:
					cash = Convert.ToInt32(qty);
				break;

				//廢筆記型電腦
				case 10:
				//廢電視機
				case 13:
				//廢冷暖氣機
				case 14:
					cash = 50*Convert.ToInt32(qty);
				break;

				//廢平板電腦
				case 11:
					cash = 30*Convert.ToInt32(qty);
				break;

				default:
					cash = 0;
				break;
			}

			if(cash<1)
			{
				cash = 1;
			}

			return cash;
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			CloseComport(blueToothPort);
			CloseCurrentVideoSource();
		}

		//針對文字處理跨執行緒
		private delegate void UpdateUICallBack(string value, Control ctl);
		private void UpdateUI(string value, Control ctl)
		{
			if(value!="")
			{
				if(this.InvokeRequired)
				{
					UpdateUICallBack uu = new UpdateUICallBack(UpdateUI);
					this.Invoke(uu, value, ctl);
				}
				else
				{
					ctl.Text = value;
				}
			}
		}

		//驗證文字特殊字元
		private int CheckSpecialString(String Data, String ColumnDescribe)
		{
			if(Data.IndexOf("--") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf("@") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf("'") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf("%") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf("*") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf("!") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf("1=1") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf(";") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.IndexOf("'") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.ToUpper().IndexOf("DELETE") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.ToUpper().IndexOf("TRUNCATE") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.ToUpper().IndexOf("UPDATE") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			if(Data.ToUpper().IndexOf("INSERT") > 0)
			{
				ShowErrorMsg(ColumnDescribe + " 格式不合法");
				return 1;
			}
			return 0;
		}

		//顯示成功訊息在提示框
		private void ShowSuccessMsg(String Msg)
		{
			labelError.Text = Msg;
			labelError.BackColor = Color.Green;
			labelError.ForeColor = Color.White;
		}

		//顯示錯誤訊息在提示框
		private void ShowErrorMsg(String Msg)
		{
			labelError.Text = Msg;
			labelError.BackColor = Color.Red;
			labelError.ForeColor = Color.White;
		}

		//重置提示框
		private void ResetMsg()
		{
			labelError.Text = "";
			labelError.BackColor = Color.Bisque;
			labelError.ForeColor = Color.White;
		}

		//寫入操作Log
		private void Log(string describe)
		{
			//設定要送出的網址
			string url = apiUrl+"saveLog";

			var request = WebRequest.Create(url);
			request.Method = "POST";
			var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			boundary = "--" + boundary;

			var requestStream = request.GetRequestStream();

			//設定要傳送的值
			var values = new Dictionary<string, string>
			{
				{ "action", "insert" },
				{ "describe", describe },
				{ "squadron_id", textBoxSquadronId.Text.Trim() },
				{ "station_id", textBoxStationId.Text.Trim() }
			};

			//寫入表單資料
			foreach(string name in values.Keys)
			{
				var buffers = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
				requestStream.Write(buffers, 0, buffers.Length);
				buffers = Encoding.UTF8.GetBytes(values[name] + Environment.NewLine);
				requestStream.Write(buffers, 0, buffers.Length);
			}

			try
			{
				var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
				requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

				var response = request.GetResponse();
				var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}
	}
}
