namespace jiazhua
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            formTheme1 = new ReaLTaiizor.Forms.FormTheme();
            tableLayoutPanel1 = new TableLayoutPanel();
            cyberGroupBox1 = new ReaLTaiizor.Controls.CyberGroupBox();
            hopeTabPage1 = new ReaLTaiizor.Controls.HopeTabPage();
            tabPage4 = new TabPage();
            tabPage5 = new TabPage();
            panel1 = new Panel();
            label_save = new Label();
            label_receive = new Label();
            label_port = new Label();
            nightControlBox1 = new ReaLTaiizor.Controls.NightControlBox();
            splitContainer1 = new SplitContainer();
            tableLayoutPanel2 = new TableLayoutPanel();
            formsPlot1 = new ScottPlot.WinForms.FormsPlot();
            flowLayoutPanel1 = new FlowLayoutPanel();
            foreverLabel1 = new ReaLTaiizor.Controls.ForeverLabel();
            formTheme1.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            hopeTabPage1.SuspendLayout();
            tabPage4.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.SuspendLayout();
            tableLayoutPanel2.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // formTheme1
            // 
            formTheme1.BackColor = Color.FromArgb(32, 41, 50);
            formTheme1.Controls.Add(tableLayoutPanel1);
            formTheme1.Controls.Add(nightControlBox1);
            formTheme1.Dock = DockStyle.Fill;
            formTheme1.Font = new Font("Segoe UI", 8F);
            formTheme1.ForeColor = Color.FromArgb(142, 142, 142);
            formTheme1.Location = new Point(0, 0);
            formTheme1.Name = "formTheme1";
            formTheme1.Padding = new Padding(3, 28, 3, 28);
            formTheme1.Sizable = true;
            formTheme1.Size = new Size(1262, 848);
            formTheme1.SmartBounds = false;
            formTheme1.StartPosition = FormStartPosition.CenterScreen;
            formTheme1.TabIndex = 0;
            formTheme1.Text = "夹爪传感采集";
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 2;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50.92251F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 49.07749F));
            tableLayoutPanel1.Controls.Add(cyberGroupBox1, 0, 0);
            tableLayoutPanel1.Controls.Add(hopeTabPage1, 0, 1);
            tableLayoutPanel1.Controls.Add(panel1, 0, 2);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(3, 28);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 3;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 16.0608578F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 79.17724F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 4.761905F));
            tableLayoutPanel1.Size = new Size(1256, 792);
            tableLayoutPanel1.TabIndex = 1;
            // 
            // cyberGroupBox1
            // 
            cyberGroupBox1.Alpha = 20;
            cyberGroupBox1.BackColor = Color.Transparent;
            cyberGroupBox1.Background = true;
            cyberGroupBox1.Background_WidthPen = 3F;
            cyberGroupBox1.BackgroundPen = true;
            cyberGroupBox1.ColorBackground = Color.FromArgb(37, 52, 68);
            cyberGroupBox1.ColorBackground_1 = Color.FromArgb(37, 52, 68);
            cyberGroupBox1.ColorBackground_2 = Color.FromArgb(41, 63, 86);
            cyberGroupBox1.ColorBackground_Pen = Color.FromArgb(29, 200, 238);
            cyberGroupBox1.ColorLighting = Color.FromArgb(29, 200, 238);
            cyberGroupBox1.ColorPen_1 = Color.FromArgb(37, 52, 68);
            cyberGroupBox1.ColorPen_2 = Color.FromArgb(41, 63, 86);
            tableLayoutPanel1.SetColumnSpan(cyberGroupBox1, 2);
            cyberGroupBox1.CyberGroupBoxStyle = ReaLTaiizor.Enum.Cyber.StateStyle.Custom;
            cyberGroupBox1.Dock = DockStyle.Fill;
            cyberGroupBox1.ForeColor = Color.FromArgb(245, 245, 245);
            cyberGroupBox1.Lighting = false;
            cyberGroupBox1.LinearGradient_Background = false;
            cyberGroupBox1.LinearGradientPen = false;
            cyberGroupBox1.Location = new Point(3, 3);
            cyberGroupBox1.Name = "cyberGroupBox1";
            cyberGroupBox1.PenWidth = 15;
            cyberGroupBox1.RGB = false;
            cyberGroupBox1.Rounding = true;
            cyberGroupBox1.RoundingInt = 10;
            cyberGroupBox1.Size = new Size(1250, 121);
            cyberGroupBox1.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            cyberGroupBox1.TabIndex = 0;
            cyberGroupBox1.Tag = "Cyber";
            cyberGroupBox1.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            cyberGroupBox1.Timer_RGB = 300;
            // 
            // hopeTabPage1
            // 
            hopeTabPage1.BaseColor = Color.FromArgb(54, 57, 64);
            tableLayoutPanel1.SetColumnSpan(hopeTabPage1, 2);
            hopeTabPage1.Controls.Add(tabPage4);
            hopeTabPage1.Controls.Add(tabPage5);
            hopeTabPage1.Dock = DockStyle.Fill;
            hopeTabPage1.Font = new Font("Segoe UI", 12F);
            hopeTabPage1.ForeColorA = Color.Silver;
            hopeTabPage1.ForeColorB = Color.Gray;
            hopeTabPage1.ForeColorC = Color.FromArgb(150, 255, 255, 255);
            hopeTabPage1.ItemSize = new Size(120, 40);
            hopeTabPage1.Location = new Point(3, 130);
            hopeTabPage1.Name = "hopeTabPage1";
            hopeTabPage1.PixelOffsetType = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            hopeTabPage1.SelectedIndex = 0;
            hopeTabPage1.Size = new Size(1250, 621);
            hopeTabPage1.SizeMode = TabSizeMode.Fixed;
            hopeTabPage1.SmoothingType = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            hopeTabPage1.TabIndex = 2;
            hopeTabPage1.TextRenderingType = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            hopeTabPage1.ThemeColorA = Color.FromArgb(29, 200, 238);
            hopeTabPage1.ThemeColorB = Color.FromArgb(150, 64, 158, 255);
            hopeTabPage1.TitleTextState = ReaLTaiizor.Controls.HopeTabPage.TextState.Normal;
            // 
            // tabPage4
            // 
            tabPage4.BackColor = Color.FromArgb(50, 63, 74);
            tabPage4.Controls.Add(splitContainer1);
            tabPage4.Location = new Point(0, 40);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(3);
            tabPage4.Size = new Size(1250, 581);
            tabPage4.TabIndex = 0;
            tabPage4.Text = "tabPage4";
            // 
            // tabPage5
            // 
            tabPage5.BackColor = Color.FromArgb(50, 63, 74);
            tabPage5.Location = new Point(0, 40);
            tabPage5.Name = "tabPage5";
            tabPage5.Padding = new Padding(3);
            tabPage5.Size = new Size(1250, 581);
            tabPage5.TabIndex = 1;
            tabPage5.Text = "tabPage5";
            // 
            // panel1
            // 
            panel1.Controls.Add(label_save);
            panel1.Controls.Add(label_receive);
            panel1.Controls.Add(label_port);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(3, 757);
            panel1.Name = "panel1";
            panel1.Size = new Size(633, 32);
            panel1.TabIndex = 3;
            // 
            // label_save
            // 
            label_save.AutoSize = true;
            label_save.ForeColor = Color.White;
            label_save.Location = new Point(271, 10);
            label_save.Name = "label_save";
            label_save.Size = new Size(72, 13);
            label_save.TabIndex = 2;
            label_save.Text = "已存包数：";
            // 
            // label_receive
            // 
            label_receive.AutoSize = true;
            label_receive.ForeColor = Color.White;
            label_receive.Location = new Point(92, 11);
            label_receive.Name = "label_receive";
            label_receive.Size = new Size(72, 13);
            label_receive.TabIndex = 1;
            label_receive.Text = "接收包数：";
            // 
            // label_port
            // 
            label_port.AutoSize = true;
            label_port.ForeColor = Color.White;
            label_port.Location = new Point(9, 10);
            label_port.Name = "label_port";
            label_port.Size = new Size(46, 13);
            label_port.TabIndex = 0;
            label_port.Text = "未连接";
            // 
            // nightControlBox1
            // 
            nightControlBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            nightControlBox1.BackColor = Color.Transparent;
            nightControlBox1.CloseHoverColor = Color.FromArgb(199, 80, 80);
            nightControlBox1.CloseHoverForeColor = Color.White;
            nightControlBox1.DefaultLocation = true;
            nightControlBox1.DisableMaximizeColor = Color.FromArgb(105, 105, 105);
            nightControlBox1.DisableMinimizeColor = Color.FromArgb(105, 105, 105);
            nightControlBox1.EnableCloseColor = Color.FromArgb(160, 160, 160);
            nightControlBox1.EnableMaximizeButton = true;
            nightControlBox1.EnableMaximizeColor = Color.FromArgb(160, 160, 160);
            nightControlBox1.EnableMinimizeButton = true;
            nightControlBox1.EnableMinimizeColor = Color.FromArgb(160, 160, 160);
            nightControlBox1.Location = new Point(1123, 0);
            nightControlBox1.MaximizeHoverColor = Color.FromArgb(15, 255, 255, 255);
            nightControlBox1.MaximizeHoverForeColor = Color.White;
            nightControlBox1.MinimizeHoverColor = Color.FromArgb(15, 255, 255, 255);
            nightControlBox1.MinimizeHoverForeColor = Color.White;
            nightControlBox1.Name = "nightControlBox1";
            nightControlBox1.Size = new Size(139, 31);
            nightControlBox1.TabIndex = 0;
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(3, 3);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(tableLayoutPanel2);
            splitContainer1.Size = new Size(1244, 575);
            splitContainer1.SplitterDistance = 628;
            splitContainer1.TabIndex = 0;
            // 
            // tableLayoutPanel2
            // 
            tableLayoutPanel2.ColumnCount = 1;
            tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel2.Controls.Add(formsPlot1, 0, 1);
            tableLayoutPanel2.Controls.Add(flowLayoutPanel1, 0, 0);
            tableLayoutPanel2.Dock = DockStyle.Fill;
            tableLayoutPanel2.Location = new Point(0, 0);
            tableLayoutPanel2.Name = "tableLayoutPanel2";
            tableLayoutPanel2.RowCount = 2;
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 8.869565F));
            tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 91.13043F));
            tableLayoutPanel2.Size = new Size(628, 575);
            tableLayoutPanel2.TabIndex = 0;
            // 
            // formsPlot1
            // 
            formsPlot1.DisplayScale = 1F;
            formsPlot1.Dock = DockStyle.Fill;
            formsPlot1.Location = new Point(3, 54);
            formsPlot1.Name = "formsPlot1";
            formsPlot1.Size = new Size(622, 518);
            formsPlot1.TabIndex = 0;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(foreverLabel1);
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.Location = new Point(3, 3);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(622, 45);
            flowLayoutPanel1.TabIndex = 1;
            // 
            // foreverLabel1
            // 
            foreverLabel1.AutoSize = true;
            foreverLabel1.BackColor = Color.Transparent;
            foreverLabel1.Font = new Font("Segoe UI", 15F, FontStyle.Regular, GraphicsUnit.Point, 0);
            foreverLabel1.ForeColor = Color.LightGray;
            foreverLabel1.Location = new Point(3, 0);
            foreverLabel1.Name = "foreverLabel1";
            foreverLabel1.Size = new Size(72, 28);
            foreverLabel1.TabIndex = 0;
            foreverLabel1.Text = "通道：";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1262, 848);
            Controls.Add(formTheme1);
            FormBorderStyle = FormBorderStyle.None;
            MaximumSize = new Size(1920, 1040);
            MinimumSize = new Size(126, 50);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "夹爪传感采集";
            TransparencyKey = Color.Fuchsia;
            formTheme1.ResumeLayout(false);
            tableLayoutPanel1.ResumeLayout(false);
            hopeTabPage1.ResumeLayout(false);
            tabPage4.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            tableLayoutPanel2.ResumeLayout(false);
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private ReaLTaiizor.Forms.FormTheme formTheme1;
        private ReaLTaiizor.Controls.NightControlBox nightControlBox1;
        private TableLayoutPanel tableLayoutPanel1;
        private ReaLTaiizor.Controls.CyberGroupBox cyberGroupBox1;
        private ReaLTaiizor.Controls.HopeTabPage hopeTabPage1;
        private TabPage tabPage4;
        private TabPage tabPage5;
        private Panel panel1;
        private Label label_port;
        private Label label_save;
        private Label label_receive;
        private SplitContainer splitContainer1;
        private TableLayoutPanel tableLayoutPanel2;
        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private FlowLayoutPanel flowLayoutPanel1;
        private ReaLTaiizor.Controls.ForeverLabel foreverLabel1;
    }
}
