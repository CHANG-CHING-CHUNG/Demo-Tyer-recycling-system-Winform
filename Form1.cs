using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web;
using AForge.Video;
using AForge.Video.DirectShow;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Drawing.Printing;

namespace TyerRecycle
{
	public partial class Form1 : Form
	{
		//攝像鏡頭設定值
		FilterInfoCollection videoDevices;
		VideoCaptureDevice videoDevice;
		string pathFolder = @"C:\RecycleImage\";
		string imageFileNamePath = "";
		string nameCapture = "";
		List<string> pic = new List<string>();
		Bitmap image_from_cam;
		static string _usbcamera;
		IVideoSource webcamSource;

		//藍芽磅秤設定值
		SerialPort blueToothPort;
		bool receiving = true;

		//接收傳回來的資料
		string ReceivedValue = string.Empty;

		//設定 Api 網址
		//private string apiUrl = "https://tyer.idwteam.tk/api/";
		//private string apiUrl = "http://testing.tyer.hying.com.tw/api/";
		private string apiUrl = "http://114.34.229.69:81/api/";

		//針對文字處理跨執行緒
		private delegate void UpdateUICallBack(string value, Control ctl);

		//列印物件初始化
		PrintDialog printDialog1 = new PrintDialog();
		PrintDialog printDialog2 = new PrintDialog();
		PrintDialog printDialog3 = new PrintDialog();

		//初始化暫存列印項目
		DataTable printDt = new DataTable();

		//已兌換金額
		int leftCash = 0;

		//已加值過標記
		bool hasExchangeEasyCard = false;

		public Form1()
		{
			InitializeComponent();
		}

		private void Form1_Load(object sender, EventArgs e)
		{
			//顯示系統資訊
			this.Text = "桃樂資源回收系統 系統版本："+Application.ProductVersion;

			//確認是否已有圖片資料夾，如無則建立
			if(Directory.Exists(pathFolder)==false)
			{
				Directory.CreateDirectory(pathFolder);
			}

			//設定密碼顯示*號
			textBoxLoginPassword.PasswordChar = '*';

			//設定輸入法預設為英數
			textBoxLoginAccount.ImeMode = ImeMode.Off;
			textBoxLoginPassword.ImeMode = ImeMode.Off;
			textBoxCityCardIdNumber.ImeMode = ImeMode.Off;
			textBoxCityCardName.ImeMode = ImeMode.Off;
			textBoxCityCardNo.ImeMode = ImeMode.Off;
			textBoxKeyinQty.ImeMode = ImeMode.Off;
			textBoxRegisterMemberCityCardNo.ImeMode = ImeMode.Off;
			textBoxRegisterMemberIdnumber.ImeMode = ImeMode.Off;
			textBoxRegisterMemberName.ImeMode = ImeMode.Off;

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
			}
			comboBoxWebcamDevice.SelectedIndex = 0;

			//取得藍芽設備裝置
			comboBoxBlueToothDevice.Items.Clear();

			string[] BlueToothDevicePorts = SerialPort.GetPortNames();

			if(BlueToothDevicePorts.Length>1)
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

			//寫入兌換清單標頭
			dataGridViewExchangeHistory.Columns.Add("item1", "回收項目");
			dataGridViewExchangeHistory.Columns.Add("item2", "重量(數量)");
			dataGridViewExchangeHistory.Columns.Add("item3", "已獲得金額");
			dataGridViewExchangeHistory.Columns.Add("item4", "圖片");

			//設定 datagridview 樣式
			DataGridViewColumn item1 = dataGridViewExchangeHistory.Columns[0];
			item1.Name = "name";
			item1.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
			item1.HeaderCell.Style.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);
			item1.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);

			DataGridViewColumn weight = dataGridViewExchangeHistory.Columns[1];
			weight.Name = "weight";
			weight.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
			weight.HeaderCell.Style.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);
			weight.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);

			DataGridViewColumn cash = dataGridViewExchangeHistory.Columns[2];
			cash.Name = "cash";
			cash.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
			cash.HeaderCell.Style.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);
			cash.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 18F, System.Drawing.FontStyle.Bold);

			DataGridViewColumn pic = dataGridViewExchangeHistory.Columns[3];
			pic.Visible = false;

			//暫存列印項目 DataTable 加入標頭
			printDt.Columns.Add("item_name", typeof(string));
			printDt.Columns.Add("qty", typeof(string));
			printDt.Columns.Add("cash", typeof(string));
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			//關閉視訊鏡頭
			closeWebcamDevice();

			//關閉藍芽磅秤連接埠
			CloseComport(blueToothPort);
		}

		private void comboBoxWebcamDevice_DropDown(object sender, EventArgs e)
		{
			//取得視訊設備裝置
			comboBoxWebcamDevice.Items.Clear();
			FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
			if(videoDevices.Count!=0)
			{
				foreach(FilterInfo device in videoDevices)
				{
					comboBoxWebcamDevice.Items.Add(device.Name);
				}
			}
			else
			{
				comboBoxWebcamDevice.Items.Add("尚未找到視訊設備");
				comboBoxWebcamDevice.SelectedIndex=0;
			}
			
			if(videoDevices.Count>1)
			{
				comboBoxWebcamDevice.SelectedIndex = 1;
			}
			else
			{
				comboBoxWebcamDevice.SelectedIndex = 0;
			}
		}

		//登入處理
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

			try
			{
				//將要傳送的值轉換成字串
				HttpContent content = new FormUrlEncodedContent(values);

				//宣告使用 http 傳送資料
				HttpClient client = new HttpClient();
				HttpResponseMessage response = await client.PostAsync(url, content);
				string responseStr = await response.Content.ReadAsStringAsync();
				dynamic responseObj = JsonConvert.DeserializeObject(responseStr);
				if(responseObj.success==true)
				{
					labelLoginMsg.Text = Convert.ToString("登入成功！畫面跳轉中！");
					labelLoginMsg.BackColor = Color.Green;
					labelLoginMsg.ForeColor = Color.White;

					Log(textBoxLoginAccount.Text.Trim()+" 登入成功！");

					textBoxSquadronId.Text = responseObj.squadron_id;
					//textBoxStationId.Text = responseObj.station_id;

					//取得所屬中隊清單
					var stationRequest = WebRequest.Create(apiUrl+"getStationList");
					stationRequest.Method = "POST";
					var stationBoundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					stationRequest.ContentType = "multipart/form-data; boundary=" + stationBoundary;
					stationBoundary = "--" + stationBoundary;

					var stationRequestStream = stationRequest.GetRequestStream();
					
					var stationValues = new Dictionary<string, string>
					{
						{ "action", "search" },
						{ "squadron_id", responseObj.squadron_id.ToString() }
					};

					//寫入表單資料
					foreach(string name in stationValues.Keys)
					{
						var statioBuffers = Encoding.ASCII.GetBytes(stationBoundary + Environment.NewLine);
						stationRequestStream.Write(statioBuffers, 0, statioBuffers.Length);
						statioBuffers = Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
						stationRequestStream.Write(statioBuffers, 0, statioBuffers.Length);
						statioBuffers = Encoding.UTF8.GetBytes(stationValues[name] + Environment.NewLine);
						stationRequestStream.Write(statioBuffers, 0, statioBuffers.Length);
					}

					var stationBoundaryBuffer = Encoding.ASCII.GetBytes(stationBoundary + "--");
					stationRequestStream.Write(stationBoundaryBuffer, 0, stationBoundaryBuffer.Length);

					var stationResponse = stationRequest.GetResponse();
					var stationResponseStream = new StreamReader(stationResponse.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic stationResponseObj = JsonConvert.DeserializeObject(stationResponseStream.ReadToEnd());

					comboBoxStationName.Items.Clear();
					foreach(var item in stationResponseObj.datas)
					{
						comboBoxStationName.Items.Add(item.name);
					}

					//隱藏登入畫面
					panelLogin.Visible = false;
				}
				else
				{
					labelLoginMsg.Text = Convert.ToString(responseObj.msg);
					labelLoginMsg.BackColor = Color.Red;
					labelLoginMsg.ForeColor = Color.White;

					Log(textBoxLoginAccount.Text.Trim()+" 登入失敗！原因："+responseObj.msg);
				}

				buttonLogin.Visible = true;
			}
			catch(Exception ex)
			{
				Log("登入發生錯誤：原因"+ex.ToString());
				labelLoginMsg.Text = "登入發生系統錯誤，請聯絡系統管理員！";
				labelLoginMsg.BackColor = Color.FromArgb(52, 73, 94);
				labelLoginMsg.ForeColor = Color.White;

				buttonLogin.Visible = true;
			}
		}

		public string usbcamera
		{
			get { return _usbcamera; }
			set { _usbcamera = value; }
		}

		private void got_frame(object sender, NewFrameEventArgs eventArgs)
		{
			image_from_cam = (Bitmap)eventArgs.Frame.Clone();
			pictureBoxWebcam.Image = image_from_cam;
		}

		private void buttonOpenCamera_Click(object sender, EventArgs e)
		{
			try
			{
				usbcamera = comboBoxWebcamDevice.SelectedIndex.ToString();
				videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
				videoDevice = new VideoCaptureDevice(videoDevices[Convert.ToInt32(usbcamera)].MonikerString);

				//註冊事件(got_frame)
				videoDevice.NewFrame += new NewFrameEventHandler(got_frame);
				OpenVideoSource(videoDevice);
				//videoSourcePlayerWebCam.VideoSource = videoDevice;
				//videoDevice.Start();

				buttonOpenCamera.Visible = false;
				buttonCloseCamera.Visible = true;
				comboBoxWebcamDevice.Enabled = false;

				ShowSuccessMsg("攝像鏡頭連接埠已開啟");

				Log("攝像鏡頭 " + comboBoxWebcamDevice.Text + " 已開啟連接埠");

				confirmDevices();
			}
			catch(Exception ex)
			{
				ShowErrorMsg("連接攝像鏡頭發生系統錯誤，請聯絡系統管理員！");

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
				videoSourcePlayerWebCam.Stop();
				videoSourcePlayerWebCam.VideoSource = null;

				videoDevice.Stop();

				//開始新的視訊來源
				videoSourcePlayerWebCam.VideoSource = source;
				videoSourcePlayerWebCam.Start();

				this.Cursor = Cursors.Default;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		private void buttonCloseCamera_Click(object sender, EventArgs e)
		{
			closeWebcamDevice();
		}

		private void closeWebcamDevice()
		{
			try
			{
				if(videoSourcePlayerWebCam.VideoSource != null)
				{
					videoSourcePlayerWebCam.SignalToStop();

					for(int i = 0;i < 30;i++)
					{
						if(!videoSourcePlayerWebCam.IsRunning)
						{
							break;
							Thread.Sleep(100);
						}
					}

					if(videoSourcePlayerWebCam.IsRunning)
					{
						videoSourcePlayerWebCam.Stop();
					}

					videoSourcePlayerWebCam.VideoSource = null;

					videoDevice.Stop();
				}

				ShowErrorMsg("攝像鏡頭連接埠已關閉");

				buttonOpenCamera.Visible = true;
				buttonCloseCamera.Visible = false;
				comboBoxWebcamDevice.Enabled = true;
				buttonNext.Visible = false;
				buttonSaveData.Visible = false;
				buttonSelectRecycleItem.Visible = false;
				buttonSaveData.Visible = false;
				panelSelectionExchange.Visible = false;
				panelSelection.Enabled = false;

				clearField();

				Log("攝像鏡頭 "+comboBoxWebcamDevice.Text+" 已關閉連接埠");
			}
			catch(Exception ex)
			{
				ShowErrorMsg("關閉攝像鏡頭發生系統錯誤，請聯絡系統管理員！");

				Log("攝像鏡頭 "+comboBoxWebcamDevice.Text+" 關閉連接埠發生錯誤。原因："+ex.ToString());
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
                    //原始變數
                    receiving = true;

                    //改成 false 讓藍芽不要在初始化就一直讀值影響
                    //receiving = false;

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
				ShowErrorMsg("連接藍芽磅秤發生系統錯誤，請聯絡系統管理員！");

				Log("藍芽磅秤連接失敗。原因：" + ex.ToString());
			}
		}

		private void buttonClosePort_Click(object sender, EventArgs e)
		{
			//關閉連接埠
			CloseComport(blueToothPort);

			buttonOpenPort.Visible = true;
			buttonClosePort.Visible = false;
			comboBoxBlueToothDevice.Enabled = true;
			buttonNext.Visible = false;
			buttonSaveData.Visible = false;
			buttonSelectRecycleItem.Visible = false;
			panelSelection.Enabled = false;

			clearField();

			Log("藍芽磅秤 "+comboBoxBlueToothDevice.Text+" 連接埠已關閉");
		}

		//關閉連接埠
		private void CloseComport(SerialPort port)
		{
			try
			{
				if((port != null) &&(port.IsOpen))
				{
					// John 2021-4-9
					// 將 receiving 移到 close 前面
					receiving = false;
					port.Close();

					//提示錯誤訊息
					ShowErrorMsg("藍芽磅秤連接埠已關閉");

					//原始變數 comment 掉
					//receiving = false;
				}
			}
			catch(Exception ex)
			{
				ShowErrorMsg("關閉藍芽磅秤發生系統錯誤，請聯絡系統管理員！");

				Log("藍芽磅秤關閉連接埠發生問題。原因："+ex.ToString());
			}
		}


		public void DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			string weightString = string.Empty;
			try
			{
                
                while (receiving)
				{
					ReceivedValue += blueToothPort.ReadExisting();
					
					//切割成一行一個陣列內容
					string[] receivedArray = ReceivedValue.Split(new string[]{ System.Environment.NewLine }, StringSplitOptions.None);

					weightString = receivedArray[receivedArray.Length-1];

					//確認有相對應的字串內容
					if((weightString.IndexOf("ST,GS,+")>=0 || weightString.IndexOf("US,GS,+")>=0) && weightString.IndexOf("kg\r\n") > 0)
					{
						Thread.Sleep(500);

						int index = weightString.IndexOf("kg");
						string receivedData = weightString.Replace("ST,GS,+", "").Replace("US,GS,+", "").Replace(System.Environment.NewLine, "").Replace("kg", "").Trim();
						UpdateUI(receivedData, textBoxScaleTotal);
					}
					else
					{
						//blueToothPort.DiscardInBuffer();

						Thread.Sleep(500);

						weightString = receivedArray[receivedArray.Length-2];
						int index = weightString.IndexOf("kg");
						string receivedData = weightString.Replace("ST,GS,+", "").Replace("US,GS,+", "").Replace(System.Environment.NewLine, "").Replace("kg", "").Trim();
						UpdateUI(receivedData, textBoxScaleTotal);
					}
				}
			}
			catch(Exception ex)
			{
				ShowErrorMsg("藍芽磅秤接收資料發生系統錯誤，請聯絡系統管理員！");

				Log("藍芽磅秤接收資料發生錯誤。原因："+ex.ToString());
			}
		}

		private void comboBoxStationName_SelectedIndexChanged(object sender, EventArgs e)
		{
			confirmDevices();
		}

		private void comboBoxStationName_SelectedValueChanged(object sender, EventArgs e)
		{
			//取得資收站序號
			if(comboBoxStationName.SelectedIndex!=-1)
			{
				var stationRequest = WebRequest.Create(apiUrl+"getStationId");
				stationRequest.Method = "POST";
				var stationBoundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
				stationRequest.ContentType = "multipart/form-data; boundary=" + stationBoundary;
				stationBoundary = "--" + stationBoundary;

				var stationRequestStream = stationRequest.GetRequestStream();
					
				var stationValues = new Dictionary<string, string>
				{
					{ "action", "search" },
					{ "squadron_id", textBoxSquadronId.Text },
					{ "squadron_name", comboBoxStationName.SelectedItem.ToString() }
				};

				//寫入表單資料
				foreach(string name in stationValues.Keys)
				{
					var statioBuffers = Encoding.ASCII.GetBytes(stationBoundary + Environment.NewLine);
					stationRequestStream.Write(statioBuffers, 0, statioBuffers.Length);
					statioBuffers = Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
					stationRequestStream.Write(statioBuffers, 0, statioBuffers.Length);
					statioBuffers = Encoding.UTF8.GetBytes(stationValues[name] + Environment.NewLine);
					stationRequestStream.Write(statioBuffers, 0, statioBuffers.Length);
				}

				var stationBoundaryBuffer = Encoding.ASCII.GetBytes(stationBoundary + "--");
				stationRequestStream.Write(stationBoundaryBuffer, 0, stationBoundaryBuffer.Length);

				var stationResponse = stationRequest.GetResponse();
				var stationResponseStream = new StreamReader(stationResponse.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic stationResponseObj = JsonConvert.DeserializeObject(stationResponseStream.ReadToEnd());
				textBoxStationId.Text = stationResponseObj.id;
			}
		}

		//確認藍芽磅秤與視訊設備已連接
		private void confirmDevices()
		{
			//buttonNext.Visible = true;
			if(blueToothPort != null && blueToothPort.IsOpen && videoSourcePlayerWebCam.VideoSource != null && comboBoxStationName.SelectedIndex!=-1)
			//if(videoSourcePlayerWebCam.VideoSource != null && comboBoxStationName.SelectedIndex!=-1)
			{
				buttonNext.Visible = true;
			}
		}

		private void buttonNext_Click(object sender, EventArgs e)
		{
			buttonNext.Visible = false;
			panelSelection.Enabled = true;
			buttonSelectRecycleItem.Visible = true;
			buttonSaveData.Visible = false;

			Log("點擊下一步開啟秤重");
		}

		private void textBoxCityCardNo_KeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode==Keys.Enter)
			{
				var values = new Dictionary<string, string>
				{
					{"keyword", textBoxCityCardNo.Text.Trim()}
				};
				
				//設定要送出的網址
				string url = apiUrl+"confirmMember";
			
				string responseData = "";

				try
				{
					var request = WebRequest.Create(url);
					request.Method = "POST";
					var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					request.ContentType = "multipart/form-data; boundary=" + boundary;
					boundary = "--" + boundary;

					var requestStream = request.GetRequestStream();
			
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

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic responseObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
					if(responseObj.check=="false")
					{
						DialogResult Result = MessageBox.Show("資料庫尚無此卡號，您是否要註冊這個卡號？", "系統訊息", MessageBoxButtons.OKCancel);
						if(Result == DialogResult.OK)
						{
							textBoxRegisterMemberCityCardNo.Text = textBoxCityCardNo.Text;
							panelRegisterMember.Visible = true;
						}
					}
					else
					{
						//確認是否達到今日加值金額
						if(Convert.ToBoolean(responseObj.overExchangeCash))
						{
							ShowErrorMsg("此市民卡已加值超過今日上限500元");
							buttonSelectionEasyCard.Enabled = false;
							textBoxCityCardIdNumber.Text = responseObj.idnumber;
							textBoxCityCardName.Text = responseObj.name;
							hasExchangeEasyCard = true;

							//textBoxSelectionName.Enabled = false;
							//textBoxScaleTotal.Enabled = false;
							//textBoxKeyinQty.Enabled = false;
							//buttonSelectRecycleItem.Visible = false;
						}
						else
						{
							//儲存目前兌換剩餘金額
							//leftCash = responseObj.left_cash;
							textBoxCityCardIdNumber.Text = responseObj.idnumber;
							textBoxCityCardName.Text = responseObj.name;
							hasExchangeEasyCard = false;

							//textBoxSelectionName.Enabled = true;
							//textBoxScaleTotal.Enabled = true;
							//textBoxKeyinQty.Enabled = true;
							//buttonSelectRecycleItem.Visible = true;

							ResetMsg();
						}
					}
				}
				catch(Exception ex)
				{
					ShowErrorMsg("確認會員資料發生系統錯誤，請聯絡系統管理員！");

					Log("確認會員資料發生錯誤！原因："+ex.ToString());
				}
			}
		}

		private void textBoxCityCardIdNumber_KeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode==Keys.Enter)
			{
				var values = new Dictionary<string, string>
				{
					{"keyword", textBoxCityCardIdNumber.Text.Trim()}
				};

				//設定要送出的網址
				string url = apiUrl+"confirmMember";

				string responseData = "";

				try
				{
					var request = WebRequest.Create(url);
					request.Method = "POST";
					var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					request.ContentType = "multipart/form-data; boundary=" + boundary;
					boundary = "--" + boundary;

					var requestStream = request.GetRequestStream();

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

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic responseObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
					if(responseObj.check=="false")
					{
						DialogResult Result = MessageBox.Show("資料庫尚無此身份證字號資料，您是否要註冊這個身份證字號資料？", "系統訊息", MessageBoxButtons.OKCancel);
						if(Result == DialogResult.OK)
						{
							textBoxRegisterMemberIdnumber.Text = textBoxCityCardIdNumber.Text;
							panelRegisterMember.Visible = true;
						}
					}
					else
					{
						//確認是否達到今日加值金額
						if(responseObj.overExchangeCash)
						{
							ShowErrorMsg("此市民卡已加值超過今日上限500元");
							buttonSelectionEasyCard.Enabled = false;
							textBoxCityCardName.Text = responseObj.name;
							textBoxCityCardNo.Text = responseObj.city_card_no;
							textBoxCityCardIdNumber.Text = responseObj.idnumber;
							hasExchangeEasyCard = true;

							//textBoxSelectionName.Enabled = false;
							//textBoxScaleTotal.Enabled = false;
							//textBoxKeyinQty.Enabled = false;
							//buttonSelectRecycleItem.Visible = false;
						}
						else
						{
							//儲存目前兌換剩餘金額
							//leftCash = responseObj.leftCash;
							textBoxCityCardName.Text = responseObj.name;
							textBoxCityCardNo.Text = responseObj.city_card_no;
							textBoxCityCardIdNumber.Text = responseObj.idnumber;
							hasExchangeEasyCard = false;

							//textBoxSelectionName.Enabled = true;
							//textBoxScaleTotal.Enabled = true;
							//textBoxKeyinQty.Enabled = true;
							//buttonSelectRecycleItem.Visible = true;

							ResetMsg();
						}
					}
				}
				catch(Exception ex)
				{
					ShowErrorMsg("確認會員資料發生系統錯誤，請聯絡系統管理員！");

					Log("確認會員資料發生錯誤！原因："+ex.ToString());
				}
			}
		}

		private void textBoxCityCardName_KeyDown(object sender, KeyEventArgs e)
		{
			if(e.KeyCode==Keys.Enter)
			{
				var values = new Dictionary<string, string>
				{
					{"keyword", textBoxCityCardName.Text.Trim()}
				};

				//設定要送出的網址
				string url = apiUrl+"confirmMember";

				string responseData = "";

				try
				{
					var request = WebRequest.Create(url);
					request.Method = "POST";
					var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					request.ContentType = "multipart/form-data; boundary=" + boundary;
					boundary = "--" + boundary;

					var requestStream = request.GetRequestStream();

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

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic responseObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
					if(responseObj.check=="false")
					{
						DialogResult Result = MessageBox.Show("資料庫尚無此姓名資料，您是否要註冊這個姓名資料？", "系統訊息", MessageBoxButtons.OKCancel);
						if(Result == DialogResult.OK)
						{
							textBoxRegisterMemberName.Text = textBoxCityCardName.Text;
							panelRegisterMember.Visible = true;
						}
					}
					else
					{
						//確認是否達到今日加值金額
						if(responseObj.overExchangeCash)
						{
							ShowErrorMsg("此市民卡已加值超過今日上限500元");
							buttonSelectionEasyCard.Enabled = false;
							textBoxCityCardIdNumber.Text = responseObj.idnumber;
							textBoxCityCardNo.Text = responseObj.city_card_no;
							hasExchangeEasyCard = true;

							//textBoxSelectionName.Enabled = false;
							//textBoxScaleTotal.Enabled = false;
							//textBoxKeyinQty.Enabled = false;
							//buttonSelectRecycleItem.Visible = false;
						}
						else
						{
							//儲存目前兌換剩餘金額
							//leftCash = responseObj.leftCash;
							textBoxCityCardIdNumber.Text = responseObj.idnumber;
							textBoxCityCardNo.Text = responseObj.city_card_no;
							hasExchangeEasyCard = false;

							//textBoxSelectionName.Enabled = true;
							//textBoxScaleTotal.Enabled = true;
							//textBoxKeyinQty.Enabled = true;
							//buttonSelectRecycleItem.Visible = true;

							ResetMsg();
						}
					}
				}
				catch(Exception ex)
				{
					ShowErrorMsg("確認會員資料發生系統錯誤，請聯絡系統管理員！");

					Log("確認會員資料發生錯誤！原因："+ex.ToString());
				}
			}
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
			//John 2021-4-9
			//當按下紙類選項時，再開啟藍芽數據接收
			//receiving = true;
			//Thread.Sleep(100);
			//receiving = false;
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

		private void buttonRecycleItem15_Click(object sender, EventArgs e)
		{
			showRecycleItemInfo("15", "鋁箔包");
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

			buttonNext.Visible = false;
			panelRecycleItems.Visible = false;
			buttonSaveData.Visible = false;
			buttonAddDataGridView.Visible = true;

			ResetMsg();

			Log("點擊回收品："+name);
		}

		private void buttonAddDataGridView_Click(object sender, EventArgs e)
		{
			if(textBoxCityCardNo.Text.Trim()=="")
			{
				ShowErrorMsg("請輸入市民卡號！");
				return;
			}
			
			if(textBoxSelectionName.Text.Trim()=="")
			{
				ShowErrorMsg("請選擇回收項目！");
				return;
			}
			
			if(textBoxKeyinQty.Text.Trim()=="" && textBoxScaleTotal.Text.Trim()=="")
			{
				ShowErrorMsg("磅秤資料未輸入或數量未輸入！");
				return;
			}

			nameCapture = "recycleImage_"+DateTime.Now.ToString("yyyyMMddHHmmss")+".png";
			imageFileNamePath = pathFolder+nameCapture;

			image_from_cam.Save(imageFileNamePath, ImageFormat.Png);

			Image img;
			MemoryStream ms = new MemoryStream(File.ReadAllBytes(imageFileNamePath));
			img = Image.FromStream(ms);

			pictureBoxSnapshot.SizeMode = PictureBoxSizeMode.StretchImage;
			pictureBoxSnapshot.Image = img;
			pictureBoxSnapshot.Update();

			labelMsg.Text = "加入至清單中...";
			labelMsg.BackColor = Color.Bisque;
			labelMsg.ForeColor = Color.Black;

			//計算回收品金額
			decimal cash = 0;
			if(textBoxSelectionName.Text=="農藥容器" || textBoxSelectionName.Text=="鍵盤" 
				|| textBoxSelectionName.Text=="廢電風扇" || textBoxSelectionName.Text=="照明光源" 
				|| textBoxSelectionName.Text=="廢筆記型電腦" || textBoxSelectionName.Text=="廢電視機" 
				|| textBoxSelectionName.Text=="廢冷暖氣機" || textBoxSelectionName.Text=="廢平板電腦")
			{
				cash = calcCash(textBoxSelectionName.Text, Convert.ToInt32(textBoxKeyinQty.Text.Trim()));
			}
			else
			{
				cash = calcCash(textBoxSelectionName.Text, Convert.ToDecimal(textBoxScaleTotal.Text.Trim()));
			}

			//if(textBoxScaleTotal.Text.Trim() != "" && textBoxKeyinQty.Text.Trim()=="")
			//{
			//	cash = calcCash(textBoxSelectionName.Text, Convert.ToDecimal(textBoxScaleTotal.Text.Trim()));
			//}

			//if(textBoxKeyinQty.Text.Trim() != "" && textBoxScaleTotal.Text.Trim()=="")
			//{
			//	cash = calcCash(textBoxSelectionName.Text, Convert.ToInt32(textBoxKeyinQty.Text.Trim()));
			//}

			int totalCash = 0;
			//確認目前加總金額
			foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
			{
				totalCash += Convert.ToInt32(item.Cells[2].Value);
			}

			//加上前次剩餘金額
			totalCash += leftCash;

			string itemQty = textBoxKeyinQty.Text.Trim()!="" ? textBoxKeyinQty.Text.Trim() : textBoxScaleTotal.Text.Trim();

			if(totalCash+cash>=500)
			{
				DialogResult Result = MessageBox.Show("加入此回收品項目將會超過500元是否繼續？", "系統訊息", MessageBoxButtons.OKCancel);
				if(Result == DialogResult.OK)
				{
					//寫入 datagridview 顯示
					string[] exchangeHistoryData = {
						textBoxSelectionName.Text,
						itemQty,
						cash.ToString(),
						imageFileNamePath
					};

					dataGridViewExchangeHistory.Rows.Add(exchangeHistoryData);

					//寫入資料到暫存列印 DataTable
					insertPrintData(printDt, exchangeHistoryData);

					ShowSuccessMsg("資料已加入清單！");
				}
			}
			else
			{
				//寫入 datagridview 顯示
				string[] exchangeHistoryData = {
					textBoxSelectionName.Text,
					itemQty,
					cash.ToString(),
					imageFileNamePath
				};

				dataGridViewExchangeHistory.Rows.Add(exchangeHistoryData);

				//寫入資料到暫存列印 DataTable
				insertPrintData(printDt, exchangeHistoryData);

				ShowSuccessMsg("資料已加入清單！");
			}

			//datagridview 加入刪除按鈕
			//Button delButton = new Button();
			//DataGridViewButtonColumn DelButton = new DataGridViewButtonColumn();

			//DelButton.HeaderText = "操作";
			//DelButton.Text = "刪除項目";
			//DelButton.Name = "delButton";
			//DelButton.UseColumnTextForButtonValue = true;
			//delButton.Click += new EventHandler(ButtonDelete_Click);
			//dataGridViewExchangeHistory.Columns.Add(DelButton);

			//DataGridViewColumn DataGridViewDelButton = dataGridViewExchangeHistory.Columns[4];
			//DataGridViewDelButton.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
			//DataGridViewDelButton.HeaderCell.Style.Font = new System.Drawing.Font("Arial", 16F, System.Drawing.FontStyle.Bold);
			//DataGridViewDelButton.DefaultCellStyle.Font = new System.Drawing.Font("Arial", 16F, System.Drawing.FontStyle.Bold);

			image_from_cam = null;

			UpdateUI(null, textBoxSelectionName);
			UpdateUI(null, textBoxScaleTotal);
			UpdateUI(null, textBoxKeyinQty);

			buttonNext.Visible = false;
			buttonAddDataGridView.Visible = false;
			buttonSaveData.Visible = true;
		}

		private void ButtonDelete_Click(object sender, EventArgs e)
		{
			//沒資料時不允許使用此事件
			if(dataGridViewExchangeHistory.RowCount == 0)
			{
				buttonSaveData.Visible = false;
				return;
			}

			DialogResult Result = MessageBox.Show("您確定要刪除這個項目？", "系統訊息", MessageBoxButtons.OKCancel);
			if(Result == DialogResult.OK)
			{
				//所選列資料
				int row = dataGridViewExchangeHistory.CurrentCell.RowIndex;

				//取得圖片檔名
				string picPath = dataGridViewExchangeHistory.Rows[row].Cells[3].Value.ToString();

				//如果檔案存在則刪除
				if(File.Exists(Path.Combine(picPath)))
				{
					File.Delete(Path.Combine(picPath));
				}

				//移除所選列資料
				dataGridViewExchangeHistory.Rows.RemoveAt(row);

				ShowSuccessMsg("資料已從清單刪除！");
			}
		}

		//刪除回收項目
		private void dataGridViewExchangeHistory_DoubleClick(object sender, EventArgs e)
		{
			//沒資料時不允許使用此事件
			if(dataGridViewExchangeHistory.RowCount == 0)
			{
				buttonSaveData.Visible = false;
				return;
			}

			DialogResult Result = MessageBox.Show("您確定要刪除這個項目？", "系統訊息", MessageBoxButtons.OKCancel);
			if(Result == DialogResult.OK)
			{
				//所選列資料
				int row = dataGridViewExchangeHistory.CurrentCell.RowIndex;

				//取得圖片檔名
				string picPath = dataGridViewExchangeHistory.Rows[row].Cells[3].Value.ToString();

				//如果檔案存在則刪除
				if(File.Exists(Path.Combine(picPath)))
				{
					File.Delete(Path.Combine(picPath));
				}

				//刪除列印清單中的項目
				for(int i=0;i<printDt.Rows.Count;i++)
				{
					if(dataGridViewExchangeHistory.Rows[row].Cells[0].Value.ToString()==printDt.Rows[i][0].ToString())
					{
						printDt.Rows.RemoveAt(i);
					}
				}

				//移除所選列資料
				dataGridViewExchangeHistory.Rows.RemoveAt(row);

				ShowSuccessMsg("資料已從清單刪除！");
			}
		}

		//上傳資料到伺服器
		private void buttonSaveData_Click(object sender, EventArgs e)
		{
			DialogResult Result = MessageBox.Show("您確定要開始兌換？", "系統訊息", MessageBoxButtons.OKCancel);
			if(Result == DialogResult.OK)
			{
				labelMsg.Text = "兌換資料上傳中...";
				labelMsg.BackColor = Color.Bisque;
				labelMsg.ForeColor = Color.Black;

				buttonSaveData.Visible = false;

				string itemDatas = string.Empty;
				//設定要傳送的值
				foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
				{
					//string itemValues =
					//	"{\"exchange_method_name\":\""+item.Cells[0].Value.ToString()+"\","+
					//	"\"city_card_no\":\""+textBoxCityCardNo.Text.Trim()+"\"}"+
					//	"\"squadron_id\":\""+textBoxSquadronId.Text.Trim()+"\"}"+
					//	"\"station_id\":\""+textBoxStationId.Text.Trim()+"\"}"+
					//	"\"qty\":\""+item.Cells[1].Value.ToString()+"\"}"+
					//	"\"cash\":\""+textBoxStationId.Text.Trim()+"\"}"+
					//	"\"pic\":\""+cash.ToString()+"\"}";
					string pic = uploadExchangePic(item.Cells[3].Value.ToString());
					var itemValues = new Dictionary<string, string>
					{
						{"exchange_method_name", item.Cells[0].Value.ToString()},
						{"city_card_no", textBoxCityCardNo.Text.Trim()},
						{"squadron_id", textBoxSquadronId.Text.Trim()},
						{"station_id", textBoxStationId.Text.Trim()},
						{"qty", item.Cells[1].Value.ToString()},
						{"cash", item.Cells[2].Value.ToString()},
						{"pic", pic}
					};
					itemDatas += JsonConvert.SerializeObject(itemValues, Formatting.Indented)+",";
				}
				itemDatas = itemDatas.Substring(0, itemDatas.Length-1);

				var values = new Dictionary<string, string>
				{
					{"action", "insert"},
					{"city_card_no", textBoxCityCardNo.Text.Trim()},
					{"squadron_id", textBoxSquadronId.Text.Trim()},
					{"station_id", textBoxStationId.Text.Trim()},
					{"itemData", "["+itemDatas.Trim().ToString()+"]"}
				};

				string responseData = "";
				string url = apiUrl+"saveExchange";
			
				try
				{
					var request = WebRequest.Create(url);
					request.Method = "POST";
					var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					request.ContentType = "multipart/form-data;accept:application/json; boundary=" + boundary;
					boundary = "--" + boundary;

					var requestStream = request.GetRequestStream();
			
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

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					string responseA = responseStream.ReadToEnd();
					dynamic responseObj = JsonConvert.DeserializeObject(responseA);
				
					if(responseObj.success==true)
					{
						//寫入列印暫存用
						textBoxExchangeNo.Text = responseObj.exchangeNo;
						textBoxStationName.Text = responseObj.stationName;
						textBoxUser.Text = responseObj.user;

						ShowSuccessMsg("兌換資料上傳完成！");

						Log("兌換資料上傳完成");

						panelSelectionExchange.Visible = true;
					}
					else
					{
						ShowErrorMsg(Convert.ToString(responseObj));

						Log("兌換資料上傳發生錯誤。原因："+responseObj);
					}

					buttonNext.Visible = false;
					buttonSaveData.Visible = false;
					buttonSelectionEasyCard.Visible = true;
					buttonSelectionPoint.Visible = true;
					buttonExchangeCancel.Visible = false;
				}
				catch(Exception ex)
				{
					ShowErrorMsg("兌換資料上傳發生系統錯誤，請聯絡系統管理員！");

					Log("兌換資料上傳發生錯誤！原因："+ex.ToString());
				}
			}
		}

		//計算兌換的金額
		private int calcCash(string selectionItemName, decimal qty)
		{
			int cash = 0;

			if(selectionItemName=="農藥容器" || selectionItemName=="鍵盤" || selectionItemName=="廢電風扇" 
				|| selectionItemName=="照明光源" || selectionItemName=="廢筆記型電腦" 
				|| selectionItemName=="廢電視機" || selectionItemName == "廢冷暖氣機"
				|| selectionItemName=="廢平板電腦")
			{
				//依不同回收項目計算
				switch(selectionItemName)
				{
					//農藥容器
					case "農藥容器":
						cash = Convert.ToInt32(qty);
					break;
					
					//鍵盤
					case "鍵盤":
						cash = 2 * Convert.ToInt32(qty);
					break;

					//廢電風扇
					case "廢電風扇":
						cash = 20 * Convert.ToInt32(qty);
					break;

					//照明光源
					case "照明光源":
						cash = Convert.ToInt32(qty);
					break;

					//廢筆記型電腦
					case "廢筆記型電腦":
						cash = 50*Convert.ToInt32(qty);
					break;

					//廢電視機
					case "廢電視機":
					//廢冷暖氣機
					case "廢冷暖氣機":
						cash = 70*Convert.ToInt32(qty);
					break;

					//廢平板電腦
					case "廢平板電腦":
						cash = 30*Convert.ToInt32(qty);
					break;

					default:
						cash = 0;
					break;
				}

				Log("回傳"+selectionItemName+"數量兌換金額："+cash+"，數量："+qty);
			}
			else
			{
				//設定要送出的網址
				string url = apiUrl+"getRecycleCash";

				var values = new Dictionary<string, string>
				{
					{"title", selectionItemName}
				};
			
				string responseData = "";

				try
				{
					var request = WebRequest.Create(url);
					request.Method = "POST";
					var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					request.ContentType = "multipart/form-data; boundary=" + boundary;
					boundary = "--" + boundary;

					var requestStream = request.GetRequestStream();
			
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

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
					
					cash = Convert.ToInt32(responseJsonObj.cash)*Convert.ToInt32(qty);

					Log("回傳"+selectionItemName+"秤重兌換金額："+cash+"，數量："+qty);
				}
				catch(Exception ex)
				{
					ShowErrorMsg("取得回收金額發生系統錯誤，請聯絡系統管理員！");

					Log("取得回收金額發生錯誤！原因："+ex.ToString());
				}
			}

			return cash;
		}

		//預先上傳圖片至伺服器的暫存資料夾中
		private string uploadExchangePic(string filePath)
		{
			string file_name = "";

			var values = new Dictionary<string, string>
			{
				{"pic", filePath}
			};

			try
			{
				//設定要送出的網址
				string url = apiUrl+"uploadExchangePic";

				string responseData = "";
				var request = WebRequest.Create(url);
				request.Method = "POST";
				var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
				request.ContentType = "multipart/form-data; boundary=" + boundary;
				boundary = "--" + boundary;

				var requestStream = request.GetRequestStream();

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

				var stream1 = File.Open(filePath, FileMode.Open);
				var buffer = Encoding.ASCII.GetBytes(boundary + Environment.NewLine);
				requestStream.Write(buffer, 0, buffer.Length);
				buffer = Encoding.UTF8.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"{2}", "pic", filePath, Environment.NewLine));
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
				dynamic responseObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());

				file_name = responseObj.file_name;
			}
			catch(Exception ex)
			{
				ShowErrorMsg("上傳圖片發生系統錯誤，請聯絡系統管理員！");

				Log("上傳圖片發生錯誤！原因："+ex.ToString());
			}

			return file_name;
		}

		private void buttonSelectionEasyCard_Click(object sender, EventArgs e)
		{
			DialogResult Result = MessageBox.Show("您確定要進行悠遊卡(一卡通)加值？", "系統訊息", MessageBoxButtons.OKCancel);
			if(Result == DialogResult.OK)
			{
				//計算可加值的兌換金額
				int totalMoney = 0;
				foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
				{
					totalMoney += Convert.ToInt32(item.Cells[2].Value);
				}

				leftCash = totalMoney-500;
				
				totalMoney = totalMoney > 500 ? 500 : totalMoney;
				
				//if(leftCash!=0)
				//{
				//	if(totalMoney>=500)
				//	{
				//		totalMoney = 500;
				//	}
				//	else
				//	{
				//		totalMoney = leftCash;
				//	}
				//}
				//else
				//{
				//	totalMoney = totalMoney > 500 ? 500 : totalMoney;
				//}

				//設定要送出的網址
				string url = apiUrl+"saveEasyCard";

				var values = new Dictionary<string, string>
				{
					{ "action", "insert" },
					{ "city_card_no", textBoxCityCardNo.Text.Trim() },
					{ "squadron_id", textBoxSquadronId.Text.Trim() },
					{ "station_id", textBoxStationId.Text.Trim() },
					{ "cash", totalMoney.ToString() },
					{ "exchangeNo", textBoxExchangeNo.Text.Trim() }
				};
			
				string responseData = "";

				try
				{
					var request = WebRequest.Create(url);
					request.Method = "POST";
					var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					request.ContentType = "multipart/form-data; boundary=" + boundary;
					boundary = "--" + boundary;

					var requestStream = request.GetRequestStream();
			
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

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic responseObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());
			
					if(responseObj.success=="True")
					{
						textBoxExchangeReturnId.Text = responseObj.id;
						textBoxUsedStoredValueCash.Text = totalMoney.ToString();

						ShowSuccessMsg("加值悠遊卡(一卡通)\n資料存檔完成！");

						Log("加值悠遊卡(一卡通)\n資料存檔完成");
						
						PrintDocument printDocument1 = new PrintDocument();

						//列印簽收單
						printDialog1.Document = printDocument1;

						if(printDialog1.ShowDialog()==DialogResult.OK)
						{
							printDocument1.PrintPage += new PrintPageEventHandler(printDocument1_PrintPage);
							printDocument1.Print();

							Log("開始列印加值金簽收單");
						}

						Log("開始列印加值金簽收單");
					}
					else
					{
						ShowErrorMsg(Convert.ToString(responseObj));

						Log("加值悠遊卡(一卡通)資料存檔發生錯誤。原因："+responseObj);
					}

					//鎖定兌換按鈕
					buttonSelectionEasyCard.Enabled = false;
					buttonSelectionPoint.Enabled = true;
					buttonExchangeCancel.Visible = true;
				}
				catch(Exception ex)
				{
					ShowErrorMsg("加值悠遊卡(一卡通)資料存檔發生系統錯誤，請聯絡系統管理員！");

					Log("加值悠遊卡(一卡通)資料存檔發生錯誤！原因："+ex.ToString());
				}
			}
		}

		//選取兌換點數
		private void buttonSelectionPoint_Click(object sender, EventArgs e)
		{
			DialogResult Result = MessageBox.Show("您確定要兌換點數？", "系統訊息", MessageBoxButtons.OKCancel);
			if(Result == DialogResult.OK)
			{
				//計算可加值的兌換金額
				string itemDatas = string.Empty;

				//設定要傳送的值
				//foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
				//{
				//	var itemValues = new Dictionary<string, string>
				//	{
				//		{"name", item.Cells[0].Value.ToString()},
				//		{"qty", item.Cells[1].Value.ToString()}
				//	};
				//	itemDatas += JsonConvert.SerializeObject(itemValues, Formatting.Indented)+",";
				//}
				//itemDatas = itemDatas.Substring(0, itemDatas.Length-1);

				Double totalMoney = 0;
				foreach (DataGridViewRow item in dataGridViewExchangeHistory.Rows)
				{
					totalMoney += Convert.ToInt32(item.Cells[2].Value);
				}

				//原始程式碼
				//int totalMoney = (Convert.ToInt32(textBoxUsedStoredValueCash.Text) - 500)/10;

				totalMoney = Math.Round((totalMoney / 10),1);


				//foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
				//{
				//	totalMoney += Convert.ToInt32(item.Cells[2].Value);
				//}

				//if(leftCash!=0)
				//{
				//	if(totalMoney>=500)
				//	{
				//		totalMoney = 500;
				//	}
				//	else
				//	{
				//		totalMoney = leftCash;
				//	}
				//}

				//設定要送出的網址
				string url = apiUrl+"saveExchangePoint";

				//設定要傳送的值
				var values = new Dictionary<string, string>
				{
					{ "action", "insert" },
					{ "city_card_no", textBoxCityCardNo.Text.Trim() },
					{ "squadron_id", textBoxSquadronId.Text.Trim() },
					{ "station_id", textBoxStationId.Text.Trim() },
					{ "point", totalMoney.ToString() },
					{ "exchangeNo", textBoxExchangeNo.Text.Trim() }
				};

				string responseData = "";

				try
				{
					var request = WebRequest.Create(url);
					request.Method="POST";
					var boundary = "---------------------------"+DateTime.Now.Ticks.ToString("x");
					request.ContentType="multipart/form-data; boundary="+boundary;
					boundary="--"+boundary;

					var requestStream = request.GetRequestStream();

					//寫入表單資料
					foreach(string name in values.Keys)
					{
						var buffers = Encoding.ASCII.GetBytes(boundary+Environment.NewLine);
						requestStream.Write(buffers, 0, buffers.Length);
						buffers=Encoding.ASCII.GetBytes(string.Format("Content-Disposition: form-data; name=\"{0}\"{1}{1}", name, Environment.NewLine));
						requestStream.Write(buffers, 0, buffers.Length);
						buffers=Encoding.UTF8.GetBytes(values[name]+Environment.NewLine);
						requestStream.Write(buffers, 0, buffers.Length);
					}

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary+"--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic responseObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());

					if(responseObj.success=="True")
					{
						textBoxExchangeReturnId.Text = responseObj.id;
						textBoxUsedExchangePoint.Text = totalMoney.ToString();

						ShowSuccessMsg("兌換點數："+leftCash.ToString()+"。已兌換完成！");

						Log("兌換點數："+leftCash.ToString()+"。已兌換完成");
						
						PrintDocument printDocument1 = new PrintDocument();

						//列印簽收單
						printDialog2.Document = printDocument1;

						if(printDialog2.ShowDialog()==DialogResult.OK)
						{
							printDocument1.PrintPage += new PrintPageEventHandler(printDocument2_PrintPage);
							printDocument1.Print();

							Log("開始列印點數簽收單");
						}

						Log("開始列印點數簽收單");
					}
					else
					{
						ShowErrorMsg("兌換點數資料存檔發生錯誤");

						Log("兌換點數資料存檔發生錯誤。原因："+responseObj);
					}

					//鎖定兌換按鈕
					buttonSelectionEasyCard.Enabled = false;
					buttonSelectionPoint.Enabled = false;
					buttonExchangeCancel.Visible = true;
				}
				catch(Exception ex)
				{
					ShowErrorMsg("兌換點數資料存檔發生系統錯誤，請聯絡系統管理員！");

					Log("兌換點數資料存檔發生錯誤！原因："+ex.ToString());
				}
			}
		}

		private void buttonPrint_Click(object sender, EventArgs e)
		{
			PrintDocument printDocument1 = new PrintDocument();

			//列印簽收單
			printDialog1.Document = printDocument1;

			if(printDialog1.ShowDialog()==DialogResult.OK)
			{
				printDocument1.PrintPage += new PrintPageEventHandler(printDocument3_PrintPage);
				printDocument1.Print();

				Log("開始列印簽收單");
			}

			Log("開始列印簽收單");

			buttonNext.Visible = false;
		}

		//簽收單內容
		private void printDocument1_PrintPage(object sender, PrintPageEventArgs e)
		{
			//隱藏中間名
			string userName = "";
			for(int i=0;i<textBoxUser.TextLength;i++)
			{
				if(i==1)
				{
					userName += "O";
				}
				else
				{
					userName += textBoxUser.Text[i];
				}
			}

			try
			{
				e.Graphics.DrawString("單號："+textBoxExchangeNo.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 20);
				e.Graphics.DrawString("市民卡號：", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 40);
				e.Graphics.DrawString(textBoxCityCardNo.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 60);
				e.Graphics.DrawString("簽收人："+userName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 80);
				e.Graphics.DrawString("站別："+textBoxStationName.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 100);
				e.Graphics.DrawString("=======民眾收執=======", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 120);
				e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 140);
				e.Graphics.DrawString("品項", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 160);
				e.Graphics.DrawString("重(數)量", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, 180);
				e.Graphics.DrawString("金額", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, 200);

				int totalMoney = 0;
				int pointLocationY = 220;
				foreach(DataRow item in printDt.Rows)
				{
					string itemName = "";
					if(item[0].ToString().Length>4)
					{
						itemName = item[0].ToString().Substring(0, 4)+"\r\n"+item[0].ToString().Substring(4, item[0].ToString().Length-4);
					}
					else
					{
						itemName = item[0].ToString();
					}

					e.Graphics.DrawString(itemName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY);
					e.Graphics.DrawString(item[1].ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, pointLocationY);
					e.Graphics.DrawString(item[2].ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, pointLocationY);

					if(item[0].ToString().Length>4)
					{
						pointLocationY += 40;
					}
					else
					{
						pointLocationY += 20;
					}

					totalMoney += Convert.ToInt32(item[2]);
				}

				e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+20);
				e.Graphics.DrawString("回收所得金額："+totalMoney, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+40);
				e.Graphics.DrawString("可加值金額："+textBoxUsedStoredValueCash.Text.Trim(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+60);
				e.Graphics.DrawString("簽收：________________", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+80);
				e.Graphics.DrawString(" ", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+100);

				Log("加值金簽收單列印完成");

				buttonSelectionEasyCard.Enabled = false;
			}
			catch(Exception ex)
			{
				ResetMsg();

				ShowErrorMsg("加值金簽收單列印發生系統錯誤，請聯絡系統管理員！");

				Log("加值金簽收單列印失敗。原因："+ex.ToString());
			}
		}
		
		//列印兌換點數單
		private void printDocument2_PrintPage(object sender, PrintPageEventArgs e)
		{
			//隱藏中間名
			string userName = "";
			for(int i=0;i<textBoxUser.TextLength;i++)
			{
				if(i==1)
				{
					userName += "O";
				}
				else
				{
					userName += textBoxUser.Text[i];
				}
			}

			try
			{
				e.Graphics.DrawString("單號："+textBoxExchangeNo.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 20);
				e.Graphics.DrawString("市民卡號：", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 40);
				e.Graphics.DrawString(textBoxCityCardNo.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 60);
				e.Graphics.DrawString("簽收人："+userName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 80);
				e.Graphics.DrawString("站別："+textBoxStationName.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 100);
				e.Graphics.DrawString("=======民眾收執=======", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 120);
				e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 140);
				e.Graphics.DrawString("品項", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 160);
				e.Graphics.DrawString("重(數)量", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, 180);
				e.Graphics.DrawString("金額", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, 200);

				//int totalMoney = 0;

				Double totalMoney = 0;

				int pointLocationY = 220;
				foreach(DataRow item in printDt.Rows)
				{
					string itemName = "";
					if(item[0].ToString().Length>4)
					{
						itemName = item[0].ToString().Substring(0, 4)+"\r\n"+item[0].ToString().Substring(4, item[0].ToString().Length-4);
					}
					else
					{
						itemName = item[0].ToString();
					}

					e.Graphics.DrawString(itemName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY);
					e.Graphics.DrawString(item[1].ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, pointLocationY);
					e.Graphics.DrawString(item[2].ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, pointLocationY);

					if(item[0].ToString().Length>4)
					{
						pointLocationY += 40;
					}
					else
					{
						pointLocationY += 20;
					}

					totalMoney += Convert.ToInt32(item[2]);
				}
				//int totalPoint = 0;
				Double totalPoint = 0;
				if(hasExchangeEasyCard==true)
				{
					totalPoint = totalMoney/10;
				}
				else
				{
					totalPoint = totalMoney/10;
				}

				e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+20);
				e.Graphics.DrawString("回收所得金額："+totalMoney, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+40);
				e.Graphics.DrawString("本次新增點數："+totalPoint, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+60);
				e.Graphics.DrawString("簽收：________________", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+80);
				e.Graphics.DrawString(" ", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+100);

				buttonSelectionEasyCard.Enabled = false;
				buttonSelectionPoint.Enabled = false;
				buttonExchangeCancel.Visible = false;

				Log("兌換點數簽收單列印完成");
			}
			catch(Exception ex)
			{
				ResetMsg();

				ShowErrorMsg("兌換點數簽收單列印發生系統錯誤，請聯絡系統管理員！");

				Log("兌換點數簽收單列印失敗。原因："+ex.ToString());
			}
		}
		
		//列印留存用簽收單
		private void printDocument3_PrintPage(object sender, PrintPageEventArgs e)
		{
			//隱藏中間名
			string userName = "";
			for(int i=0;i<textBoxUser.TextLength;i++)
			{
				if(i==1)
				{
					userName += "O";
				}
				else
				{
					userName += textBoxUser.Text[i];
				}
			}

			try
			{
				e.Graphics.DrawString("單號："+textBoxExchangeNo.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 20);
				e.Graphics.DrawString("市民卡號：", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 40);
				e.Graphics.DrawString(textBoxCityCardNo.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 60);
				e.Graphics.DrawString("簽收人："+userName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 80);
				e.Graphics.DrawString("站別："+textBoxStationName.Text, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 100);
				e.Graphics.DrawString("=======收執存根=======", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 120);
				e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 140);
				e.Graphics.DrawString("品項", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, 160);
				e.Graphics.DrawString("重(數)量", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, 180);
				e.Graphics.DrawString("金額", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, 200);

				int totalMoney = 0;
				int pointLocationY = 220;
				foreach(DataRow item in printDt.Rows)
				{
					string itemName = "";
					if(item[0].ToString().Length>4)
					{
						itemName = item[0].ToString().Substring(0, 4)+"\r\n"+item[0].ToString().Substring(4, item[0].ToString().Length-4);
					}
					else
					{
						itemName = item[0].ToString();
					}

					e.Graphics.DrawString(itemName, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY);
					e.Graphics.DrawString(item[1].ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 80, pointLocationY);
					e.Graphics.DrawString(item[2].ToString(), new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 150, pointLocationY);

					if(item[0].ToString().Length>4)
					{
						pointLocationY += 40;
					}
					else
					{
						pointLocationY += 20;
					}

					totalMoney += Convert.ToInt32(item[2]);
				}

				int StoredValueCash = 0;
				int totalPoint = 0;
				if(hasExchangeEasyCard==true)
				{
					StoredValueCash = 0;
					totalPoint = totalMoney/10;
				}
				else
				{
					if(totalMoney>=500)
					{
						StoredValueCash = 500;
						totalPoint = (totalMoney-500)/10;
					}
					else
					{
						StoredValueCash = 0;
						totalPoint = totalMoney/10;
					}
				}

				e.Graphics.DrawString("======================", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+20);
				e.Graphics.DrawString("回收所得金額："+totalMoney, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+40);
				e.Graphics.DrawString("可加值金額："+StoredValueCash, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+60);
				e.Graphics.DrawString("本次新增點數："+totalPoint, new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+80);
				e.Graphics.DrawString("簽收：________________", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+100);
				e.Graphics.DrawString(" ", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 10, pointLocationY+120);

				clearField();

				buttonSaveData.Visible = false;
				panelSelectionExchange.Visible = false;

				//解除兌換按鈕
				buttonSelectionEasyCard.Enabled = true;
				buttonSelectionPoint.Enabled = true;
				buttonExchangeCancel.Visible = false;

				Log("留存用簽收單列印完成");
			}
			catch(Exception ex)
			{
				ResetMsg();

				ShowErrorMsg("留存用簽收單列印發生系統錯誤，請聯絡系統管理員！");

				Log("留存用簽收單列印失敗。原因：" + ex.ToString());
			}
		}

		private void buttonExchangeCancel_Click(object sender, EventArgs e)
		{
			DialogResult Result = MessageBox.Show("您確定刪除兌換悠遊卡(一卡通、點數)紀錄？", "系統訊息", MessageBoxButtons.OKCancel);
			if(Result == DialogResult.OK)
			{
				textBoxUsedExchangePoint.Text = null;
				textBoxUsedStoredValueCash.Text = null;

				//設定要送出的網址
				string url = apiUrl+"delExchange";

				var values = new Dictionary<string, string>
				{
					{"id", textBoxExchangeReturnId.Text},
					{"action", "delete"}
				};
			
				string responseData = "";

				try
				{
					var request = WebRequest.Create(url);
					request.Method = "POST";
					var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
					request.ContentType = "multipart/form-data; boundary=" + boundary;
					boundary = "--" + boundary;

					var requestStream = request.GetRequestStream();
			
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

					var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
					requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

					var response = request.GetResponse();
					var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
					dynamic responseObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());

					if(responseObj.success==true)
					{
						ShowSuccessMsg("刪除兌換紀錄完成！");

						Log("刪除兌換紀錄完成");

						textBoxExchangeReturnId.Text = null;
						textBoxExchangeStoredValue.Text = "0";
						textBoxUsedExchangePoint.Text = "0";
						buttonSelectionEasyCard.Enabled = true;
						buttonSelectionPoint.Enabled = true;
						buttonExchangeCancel.Visible = false;
					}
					else
					{
						ShowErrorMsg("刪除兌換紀錄發生錯誤");

						Log("刪除兌換紀錄發生錯誤。原因："+responseObj);
					}
				}
				catch(Exception ex)
				{
					ShowErrorMsg("刪除兌換紀錄發生系統錯誤，請聯絡系統管理員！");

					Log("刪除兌換紀錄發生錯誤！原因："+ex.ToString());
				}
			}
		}

		//關閉兌換畫面
		private void buttonCloseSelectionExchange_Click(object sender, EventArgs e)
		{
			panelSelectionExchange.Visible = false;
		}

		//初始化欄位所有資料

		private void buttonInit_Click(object sender, EventArgs e)
		{
			DialogResult Result = MessageBox.Show("您確定要重新秤重？", "系統訊息", MessageBoxButtons.OKCancel);
			if(Result == DialogResult.OK)
			{
				clearField();
			}
		}

		//儲存註冊會員資料
		private void buttonRegisterMemberSave_Click(object sender, EventArgs e)
		{
			//確認輸入是否為空白
			if(textBoxRegisterMemberCityCardNo.Text.Trim()=="")
			{
				ShowErrorMsg("請輸入市民卡號！");
				return;
			}
			//if(textBoxRegisterMemberIdnumber.Text.Trim()=="")
			//{
			//	ShowErrorMsg("請輸入身份證字號！");
			//	return;
			//}
			if(textBoxRegisterMemberName.Text.Trim()=="")
			{
				ShowErrorMsg("請輸入姓名！");
				return;
			}

			//設定要送出的網址
			string url = apiUrl+"registerMember";

			var values = new Dictionary<string, string>
			{
				{"action", "insert"},
				{"city_card_no", textBoxRegisterMemberCityCardNo.Text},
				{"idnumber", textBoxRegisterMemberIdnumber.Text},
				{"name", textBoxRegisterMemberName.Text}
			};

			dynamic responseObj = sendWebRequest(url, values);
			
			if(responseObj=="True")
			{
				ShowSuccessMsg("會員註冊資料存檔完成！");

				Log("會員註冊資料存檔完成");

				textBoxCityCardName.Text = textBoxRegisterMemberName.Text;
				textBoxCityCardIdNumber.Text = textBoxRegisterMemberIdnumber.Text;
				textBoxCityCardNo.Text = textBoxRegisterMemberCityCardNo.Text;

				textBoxRegisterMemberCityCardNo.Text = null;
				textBoxRegisterMemberIdnumber.Text = null;
				textBoxRegisterMemberName.Text = null;
				panelRegisterMember.Visible = false;
			}
			else
			{
				ShowErrorMsg(Convert.ToString(responseObj));

				Log("會員註冊資料存檔發生錯誤。原因："+responseObj);
			}
		}

		//取消註冊會員
		private void buttonRegisterMemberCancel_Click(object sender, EventArgs e)
		{
			ResetMsg();
			textBoxRegisterMemberCityCardNo.Text = null;
			textBoxRegisterMemberIdnumber.Text = null;
			textBoxRegisterMemberName.Text = null;
			panelRegisterMember.Visible = false;
		}

		//新增變更列印項目
		private void insertPrintData(DataTable printDtTemp, string[] value)
		{
			DataRow[] tempData = printDtTemp.Select("item_name='"+value[0]+"'");
			if(tempData.Length>0)
			{
				decimal qty = 0;
				int cash = 0;
				for(int i=0;i<printDtTemp.Rows.Count;i++)
				{
					if(printDtTemp.Rows[i][0].ToString()==value[0] && (i+1!=printDtTemp.Rows.Count || i+2!=printDtTemp.Rows.Count))
					{
						qty = Convert.ToDecimal(printDtTemp.Rows[i][1])+Convert.ToDecimal(value[1]);
						cash = Convert.ToInt32(printDtTemp.Rows[i][2])+Convert.ToInt32(value[2]);

						string[] data = {
							value[0],
							qty.ToString(),
							cash.ToString()
						};
						printDtTemp.Rows.Add(data);
						printDtTemp.Rows.RemoveAt(i);
						i++;
					}
				}
			}
			else
			{
				string[] data = {
					value[0],
					value[1],
					value[2]
				};
				printDtTemp.Rows.Add(data);
			}
		}

		//清除輸入框資料
		private void clearField()
		{
			//刪除暫存的圖片檔
			foreach(DataGridViewRow item in dataGridViewExchangeHistory.Rows)
			{
				//取得圖片檔名
				string picPath = item.Cells[3].Value.ToString();

				//如果檔案存在則刪除
				if(File.Exists(Path.Combine(picPath)))
				{
					File.Delete(Path.Combine(picPath));
				}
			}

			textBoxCityCardNo.Text = null;
			textBoxCityCardIdNumber.Text = null;
			textBoxCityCardName.Text = null;
			textBoxExchangeStoredValue.Text = null;
			textBoxKeyinQty.Text = null;
			textBoxRecycleSelectionItem.Text = null;
			textBoxScaleTotal.Text = null;
			textBoxSelectionName.Text = null;
			textBoxTotalExchangePoint.Text = null;
			textBoxUsedExchangePoint.Text = null;
			textBoxUsedStoredValueCash.Text = null;
			dataGridViewExchangeHistory.Rows.Clear();
			textBoxExchangeNo.Text = null;
			textBoxStationName.Text = null;
			textBoxUser.Text = null;
			textBoxExchangeReturnId.Text = null;
			buttonAddDataGridView.Visible = false;
			buttonSaveData.Visible = false;
			pictureBoxSnapshot.Image = null;

			buttonSelectionEasyCard.Enabled = true;
			buttonSelectionPoint.Enabled = true;
			buttonExchangeCancel.Visible = false;

			printDt.Rows.Clear();

			leftCash = 0;
			hasExchangeEasyCard = false;

			ResetMsg();
		}

		//跨執行序處理
		private void UpdateUI(string value, Control ctl)
		{
			//比對磅秤舊值是否跟新值一樣，若一樣，便不更新UI 直接結束 function
			if(value == textBoxScaleTotal.Text)
            {
				return;
            }

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

		//傳送資料到 API 程式
		private string sendWebRequest(string url, Dictionary<string, string> values)
		{
			string responseData = "";

			var request = WebRequest.Create(url);
			request.Method = "POST";
			var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			boundary = "--" + boundary;

			var requestStream = request.GetRequestStream();
			
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
				if(values.ContainsKey("pic"))
				{
					var stream1 = File.Open(imageFileNamePath, FileMode.Open);
					stream1.CopyTo(requestStream);
				}

				var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
				requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

				var response = request.GetResponse();
				var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());

				if(responseJsonObj.success==true)
				{
					responseData = responseJsonObj.success;
				}
				else
				{
					responseData = responseJsonObj.msg;
				}
			}
			catch (Exception ex)
			{
				ResetMsg();
				responseData = ex.ToString();

				Log("httpRequest錯誤，原因："+ex.ToString());
			}

			request = null;

			return responseData;
		}

		//傳送資料到 API 程式並回傳指定欄位資料
		private string sendWebRequestReturnData(string url, Dictionary<string, string> values, dynamic responseField)
		{
			string responseData = "";

			var request = WebRequest.Create(url);
			request.Method = "POST";
			var boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
			request.ContentType = "multipart/form-data; boundary=" + boundary;
			boundary = "--" + boundary;

			var requestStream = request.GetRequestStream();
			
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
				var boundaryBuffer = Encoding.ASCII.GetBytes(boundary + "--");
				requestStream.Write(boundaryBuffer, 0, boundaryBuffer.Length);

				var response = request.GetResponse();
				var responseStream = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8"));
				dynamic responseJsonObj = JsonConvert.DeserializeObject(responseStream.ReadToEnd());

				responseData = responseJsonObj.responseField;
			}
			catch(Exception ex)
			{
				ResetMsg();
				ShowErrorMsg(ex.ToString());

				Log("httpRequest錯誤，原因："+ex.ToString());
			}

			request = null;

			return responseData;
		}

		//顯示成功訊息在提示框
		private void ShowSuccessMsg(String Msg)
		{
			labelMsg.Text = Msg;
			labelMsg.BackColor = Color.Green;
			labelMsg.ForeColor = Color.White;
		}

		//顯示錯誤訊息在提示框
		private void ShowErrorMsg(String Msg)
		{
			labelMsg.Text = Msg;
			labelMsg.BackColor = Color.Red;
			labelMsg.ForeColor = Color.White;
		}

		//重置提示框
		private void ResetMsg()
		{
			labelMsg.Text = "";
			labelMsg.BackColor = Color.Bisque;
			labelMsg.ForeColor = Color.White;
		}

		//寫入操作Log
		private void Log(string describe)
		{
			//設定要送出的網址
			string url = apiUrl+"saveLog";
			
			//設定要傳送的值
			var values = new Dictionary<string, string>
			{
				{ "action", "insert" },
				{ "describe", describe },
				{ "squadron_id", textBoxSquadronId.Text.Trim() },
				{ "station_id", textBoxStationId.Text.Trim() }
			};

			dynamic responseObj = sendWebRequest(url, values);
		}
	}
}
