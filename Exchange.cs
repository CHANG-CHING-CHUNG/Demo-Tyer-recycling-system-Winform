using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TyerRecycle
{
	public partial class Exchange : Form
	{
		public Exchange()
		{
			InitializeComponent();
		}

		private void Exchange_Load(object sender, EventArgs e)
		{
			webBrowser1.Navigate("http://tyer.idwteam.tk/manage");
		}
	}
}
