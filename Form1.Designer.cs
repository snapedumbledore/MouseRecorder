using System.Windows.Forms;
using System;

namespace MouseRecorder
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.groupSchedule = new System.Windows.Forms.GroupBox();
            this.btnSchedule = new System.Windows.Forms.Button();
            this.btnStopSchedule = new System.Windows.Forms.Button();
            this.numInterval = new System.Windows.Forms.NumericUpDown();
            this.btnClearLog = new System.Windows.Forms.Button();
            this.lblPlaybackCount = new System.Windows.Forms.Label();
            this.numPlaybackCount = new System.Windows.Forms.NumericUpDown();
            this.groupSchedule.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numInterval)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPlaybackCount)).BeginInit();
            this.SuspendLayout();
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(562, 356);
            this.Controls.Add(this.numPlaybackCount);
            this.Controls.Add(this.lblPlaybackCount);
            this.Controls.Add(this.btnClearLog);
            this.Controls.Add(this.groupSchedule);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.btnPlay);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Name = "Form1";
            this.Text = "鼠标键盘录制回放器";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupSchedule.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numInterval)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPlaybackCount)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(12, 12);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(100, 30);
            this.btnStart.TabIndex = 3;
            this.btnStart.Text = "开始录制";
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(118, 12);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(100, 30);
            this.btnStop.TabIndex = 2;
            this.btnStop.Text = "停止录制";
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnPlay
            // 
            this.btnPlay.Location = new System.Drawing.Point(12, 59);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(100, 30);
            this.btnPlay.TabIndex = 1;
            this.btnPlay.Text = "回放";
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);
            // 
            // txtLog
            // 
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.Location = new System.Drawing.Point(12, 144);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(538, 200);
            this.txtLog.TabIndex = 0;
            // 
            // groupSchedule
            // 
            this.groupSchedule.Controls.Add(this.btnSchedule);
            this.groupSchedule.Controls.Add(this.btnStopSchedule);
            this.groupSchedule.Controls.Add(this.numInterval);
            this.groupSchedule.Location = new System.Drawing.Point(12, 95);
            this.groupSchedule.Name = "groupSchedule";
            this.groupSchedule.Size = new System.Drawing.Size(247, 43);
            this.groupSchedule.TabIndex = 4;
            this.groupSchedule.TabStop = false;
            this.groupSchedule.Text = "定时回放";
            // 
            // btnSchedule
            // 
            this.btnSchedule.Location = new System.Drawing.Point(73, 13);
            this.btnSchedule.Name = "btnSchedule";
            this.btnSchedule.Size = new System.Drawing.Size(80, 23);
            this.btnSchedule.TabIndex = 6;
            this.btnSchedule.Text = "启动定时";
            this.btnSchedule.Click += new System.EventHandler(this.BtnSchedule_Click);
            // 
            // btnStopSchedule
            // 
            this.btnStopSchedule.Location = new System.Drawing.Point(159, 13);
            this.btnStopSchedule.Name = "btnStopSchedule";
            this.btnStopSchedule.Size = new System.Drawing.Size(80, 23);
            this.btnStopSchedule.TabIndex = 7;
            this.btnStopSchedule.Text = "立即停止";
            this.btnStopSchedule.Click += new System.EventHandler(this.BtnStopSchedule_Click);
            // 
            // numInterval
            // 
            this.numInterval.Location = new System.Drawing.Point(7, 16);
            this.numInterval.Maximum = new decimal(new int[] {
            1440,
            0,
            0,
            0});
            this.numInterval.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numInterval.Name = "numInterval";
            this.numInterval.Size = new System.Drawing.Size(60, 20);
            this.numInterval.TabIndex = 5;
            this.numInterval.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.numInterval.ValueChanged += new System.EventHandler(this.numInterval_ValueChanged);
            // 
            // btnClearLog
            // 
            this.btnClearLog.Location = new System.Drawing.Point(118, 59);
            this.btnClearLog.Name = "btnClearLog";
            this.btnClearLog.Size = new System.Drawing.Size(100, 30);
            this.btnClearLog.TabIndex = 7;
            this.btnClearLog.Text = "清空日志";
            this.btnClearLog.Click += new System.EventHandler(this.BtnClearLog_Click);
            // 
            // lblPlaybackCount
            // 
            this.lblPlaybackCount.AutoSize = true;
            this.lblPlaybackCount.Location = new System.Drawing.Point(234, 21);
            this.lblPlaybackCount.Name = "lblPlaybackCount";
            this.lblPlaybackCount.Size = new System.Drawing.Size(58, 13);
            this.lblPlaybackCount.TabIndex = 10;
            this.lblPlaybackCount.Text = "回放次数:";
            this.lblPlaybackCount.Click += new System.EventHandler(this.lblPlaybackCount_Click);
            // 
            // numPlaybackCount
            // 
            this.numPlaybackCount.Location = new System.Drawing.Point(298, 19);
            this.numPlaybackCount.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numPlaybackCount.Name = "numPlaybackCount";
            this.numPlaybackCount.Size = new System.Drawing.Size(60, 20);
            this.numPlaybackCount.TabIndex = 11;
            this.numPlaybackCount.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numPlaybackCount.ValueChanged += new System.EventHandler(this.numPlaybackCount_ValueChanged);
            // 
            

        }

        #endregion

        private Button btnStart;
        private Button btnStop;
        private Button btnPlay;
        private TextBox txtLog;
        private GroupBox groupSchedule;
        private NumericUpDown numInterval;
        private Button btnSchedule;
        private Button btnClearLog;
        private Button btnStopSchedule;
        private Label lblPlaybackCount;
        private NumericUpDown numPlaybackCount;
    }
}
