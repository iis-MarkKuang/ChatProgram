namespace ChatProgram
{
    partial class ConnectUI
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.send = new System.Windows.Forms.Button();
            this.history = new System.Windows.Forms.TextBox();
            this.messageBox = new System.Windows.Forms.TextBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // send
            // 
            this.send.Location = new System.Drawing.Point(449, 254);
            this.send.Name = "send";
            this.send.Size = new System.Drawing.Size(153, 74);
            this.send.TabIndex = 0;
            this.send.Text = "sendMessage";
            this.send.UseVisualStyleBackColor = true;
            this.send.Click += new System.EventHandler(this.button1_Click);
            // 
            // history
            // 
            this.history.Location = new System.Drawing.Point(46, 26);
            this.history.Multiline = true;
            this.history.Name = "history";
            this.history.ReadOnly = true;
            this.history.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.history.Size = new System.Drawing.Size(331, 177);
            this.history.TabIndex = 1;
            // 
            // messageBox
            // 
            this.messageBox.Location = new System.Drawing.Point(46, 242);
            this.messageBox.Multiline = true;
            this.messageBox.Name = "messageBox";
            this.messageBox.Size = new System.Drawing.Size(331, 100);
            this.messageBox.TabIndex = 2;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // ConnectUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(651, 379);
            this.Controls.Add(this.messageBox);
            this.Controls.Add(this.history);
            this.Controls.Add(this.send);
            this.KeyPreview = true;
            this.Name = "ConnectUI";
            this.Text = "Form1";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.ConnectUI_FormClosed);
            this.Load += new System.EventHandler(this.ConnectUI_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button send;
        private System.Windows.Forms.TextBox history;
        private System.Windows.Forms.TextBox messageBox;
        private System.Windows.Forms.Timer timer1;
    }
}

